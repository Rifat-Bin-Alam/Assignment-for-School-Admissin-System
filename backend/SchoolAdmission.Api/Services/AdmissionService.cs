using System.Data;
using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.DTOs;
using SchoolAdmission.Api.Models;

namespace SchoolAdmission.Api.Services;

public class AdmissionService : IAdmissionService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdmissionService> _logger;

    public AdmissionService(
        ApplicationDbContext context,
        IConfiguration configuration,
        ILogger<AdmissionService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Gets the configured hold reservation duration in seconds. Defaults to 600 seconds (10 minutes).
    /// </summary>
    private int GetHoldDurationSeconds()
    {
        var configured = _configuration["HOLD_DURATION_SECONDS"];
        if (int.TryParse(configured, out var seconds) && seconds > 0)
        {
            return seconds;
        }

        return 600;
    }

    public async Task<ClassResponse> CreateClassAsync(CreateClassRequest request)
    {
        // Generate a concise identifier like c_101 or c_1a2b3c
        var classId = $"c_{Guid.NewGuid():N}"[..7];

        var classEntity = new ClassEntity
        {
            Id = classId,
            Name = request.Name.Trim(),
            TotalSeats = request.TotalSeats
        };

        _context.Classes.Add(classEntity);
        await _context.SaveChangesAsync();

        return new ClassResponse
        {
            Id = classEntity.Id,
            Name = classEntity.Name,
            TotalSeats = classEntity.TotalSeats,
            HeldSeats = 0,
            ConfirmedSeats = 0,
            AvailableSeats = classEntity.TotalSeats
        };
    }

    public async Task<IEnumerable<ClassResponse>> GetAllClassesAsync()
    {
        var now = DateTime.UtcNow;

        var classes = await _context.Classes
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync();

        var classIds = classes.Select(c => c.Id).ToList();

        // Fetch all relevant holds in a single query
        var holds = await _context.AdmissionHolds
            .AsNoTracking()
            .Where(h => classIds.Contains(h.ClassId))
            .ToListAsync();

        var responseList = new List<ClassResponse>(classes.Count);

        foreach (var cls in classes)
        {
            var classHolds = holds.Where(h => h.ClassId == cls.Id).ToList();

            // Only unexpired HELD reservations count toward heldSeats
            var heldSeats = classHolds.Count(h => h.Status == HoldStatus.Held && h.ExpiresAt > now);
            var confirmedSeats = classHolds.Count(h => h.Status == HoldStatus.Confirmed);
            var availableSeats = Math.Max(0, cls.TotalSeats - (heldSeats + confirmedSeats));

            responseList.Add(new ClassResponse
            {
                Id = cls.Id,
                Name = cls.Name,
                TotalSeats = cls.TotalSeats,
                HeldSeats = heldSeats,
                ConfirmedSeats = confirmedSeats,
                AvailableSeats = availableSeats
            });
        }

        return responseList;
    }

    public async Task<ClassResponse?> GetClassByIdAsync(string classId)
    {
        var now = DateTime.UtcNow;

        var classEntity = await _context.Classes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == classId);

        if (classEntity == null)
        {
            return null;
        }

        var classHolds = await _context.AdmissionHolds
            .AsNoTracking()
            .Where(h => h.ClassId == classId)
            .ToListAsync();

        var heldSeats = classHolds.Count(h => h.Status == HoldStatus.Held && h.ExpiresAt > now);
        var confirmedSeats = classHolds.Count(h => h.Status == HoldStatus.Confirmed);
        var availableSeats = Math.Max(0, classEntity.TotalSeats - (heldSeats + confirmedSeats));

        return new ClassResponse
        {
            Id = classEntity.Id,
            Name = classEntity.Name,
            TotalSeats = classEntity.TotalSeats,
            HeldSeats = heldSeats,
            ConfirmedSeats = confirmedSeats,
            AvailableSeats = availableSeats
        };
    }

    public async Task<(AdmissionHold? Hold, string? Error, int StatusCode)> CreateHoldAsync(string classId, string studentId)
    {
        if (string.IsNullOrWhiteSpace(studentId))
        {
            return (null, "X-Student-Id header is required.", 400);
        }

        // We use an explicit database transaction with row-level locking.
        // PostgreSQL's "SELECT ... FOR UPDATE" locks the specific Class row,
        // which serializes all concurrent reservation requests for this class.
        // This ensures two simultaneous requests can never oversell the available seats.
        using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

        try
        {
            var classEntity = await _context.Classes
                .FromSqlInterpolated($"SELECT * FROM \"Classes\" WHERE \"Id\" = {classId} FOR UPDATE")
                .FirstOrDefaultAsync();

            if (classEntity == null)
            {
                await transaction.RollbackAsync();
                return (null, $"Class with ID '{classId}' was not found.", 404);
            }

            var now = DateTime.UtcNow;

            // Mark expired holds for this class to free up seats immediately
            var expiredHolds = await _context.AdmissionHolds
                .Where(h => h.ClassId == classId && h.Status == HoldStatus.Held && h.ExpiresAt <= now)
                .ToListAsync();

            if (expiredHolds.Count > 0)
            {
                foreach (var exp in expiredHolds)
                {
                    exp.Status = HoldStatus.Expired;
                }
                await _context.SaveChangesAsync();
            }

            // Rule 7: A student cannot have multiple active applications or confirmed admissions in the same class
            var existingHold = await _context.AdmissionHolds
                .FirstOrDefaultAsync(h => h.ClassId == classId 
                                       && h.StudentId == studentId 
                                       && (h.Status == HoldStatus.Confirmed || (h.Status == HoldStatus.Held && h.ExpiresAt > now)));

            if (existingHold != null)
            {
                await transaction.RollbackAsync();
                return (null, "Student already has an active reservation or confirmed admission in this class.", 409);
            }

            // Count current held and confirmed seats
            var heldSeats = await _context.AdmissionHolds
                .CountAsync(h => h.ClassId == classId && h.Status == HoldStatus.Held && h.ExpiresAt > now);

            var confirmedSeats = await _context.AdmissionHolds
                .CountAsync(h => h.ClassId == classId && h.Status == HoldStatus.Confirmed);

            // Rule 1: heldSeats + confirmedSeats must never exceed totalSeats
            if (heldSeats + confirmedSeats >= classEntity.TotalSeats)
            {
                await transaction.RollbackAsync();
                return (null, "No seats available in this class.", 409);
            }

            var holdDuration = GetHoldDurationSeconds();
            var newHold = new AdmissionHold
            {
                Id = $"h_{Guid.NewGuid():N}"[..7],
                ClassId = classId,
                StudentId = studentId,
                Status = HoldStatus.Held,
                CreatedAt = now,
                ExpiresAt = now.AddSeconds(holdDuration)
            };

            _context.AdmissionHolds.Add(newHold);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            return (newHold, null, 201);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error creating hold for class {ClassId} and student {StudentId}", classId, studentId);
            throw;
        }
    }

    public async Task<(AdmissionHold? Hold, string? Error, int StatusCode)> GetHoldByIdAsync(string holdId)
    {
        var hold = await _context.AdmissionHolds
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.Id == holdId);

        if (hold == null)
        {
            return (null, $"Hold reservation '{holdId}' was not found.", 404);
        }

        // If hold was HELD but time has passed, treat status as EXPIRED
        if (hold.Status == HoldStatus.Held && hold.ExpiresAt <= DateTime.UtcNow)
        {
            hold.Status = HoldStatus.Expired;
        }

        return (hold, null, 200);
    }

    public async Task<(AdmissionHold? Hold, string? Error, int StatusCode)> ConfirmHoldAsync(string holdId, string studentId)
    {
        if (string.IsNullOrWhiteSpace(studentId))
        {
            return (null, "X-Student-Id header is required.", 400);
        }

        // Use a transaction with row lock on the AdmissionHold to guarantee atomic updates
        using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

        try
        {
            var hold = await _context.AdmissionHolds
                .FromSqlInterpolated($"SELECT * FROM \"AdmissionHolds\" WHERE \"Id\" = {holdId} FOR UPDATE")
                .FirstOrDefaultAsync();

            if (hold == null)
            {
                await transaction.RollbackAsync();
                return (null, $"Hold reservation '{holdId}' was not found.", 404);
            }

            // Rule 8: A student can only confirm their own reservation
            if (hold.StudentId != studentId)
            {
                await transaction.RollbackAsync();
                return (null, "You can only confirm your own reservation.", 403);
            }

            // Rule 6: Confirming the same application multiple times must not increase confirmedSeats more than once
            if (hold.Status == HoldStatus.Confirmed)
            {
                await transaction.CommitAsync();
                return (hold, null, 200);
            }

            var now = DateTime.UtcNow;

            // Rule 5: Only an unexpired HELD application can be confirmed
            if (hold.Status == HoldStatus.Cancelled)
            {
                await transaction.RollbackAsync();
                return (null, "Cancelled reservations cannot be confirmed.", 400);
            }

            if (hold.Status == HoldStatus.Expired || hold.ExpiresAt <= now)
            {
                if (hold.Status != HoldStatus.Expired)
                {
                    hold.Status = HoldStatus.Expired;
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();
                return (null, "Reservation has expired and cannot be confirmed.", 400);
            }

            // Mark as CONFIRMED
            hold.Status = HoldStatus.Confirmed;
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            return (hold, null, 200);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error confirming hold {HoldId}", holdId);
            throw;
        }
    }

    public async Task<(bool Success, string? Error, int StatusCode)> CancelHoldAsync(string holdId, string studentId)
    {
        if (string.IsNullOrWhiteSpace(studentId))
        {
            return (false, "X-Student-Id header is required.", 400);
        }

        using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

        try
        {
            var hold = await _context.AdmissionHolds
                .FromSqlInterpolated($"SELECT * FROM \"AdmissionHolds\" WHERE \"Id\" = {holdId} FOR UPDATE")
                .FirstOrDefaultAsync();

            if (hold == null)
            {
                await transaction.RollbackAsync();
                return (false, $"Hold reservation '{holdId}' was not found.", 404);
            }

            // Rule 8: A student can only cancel their own reservation
            if (hold.StudentId != studentId)
            {
                await transaction.RollbackAsync();
                return (false, "You can only cancel your own reservation.", 403);
            }

            // If already cancelled, cancellation is idempotent
            if (hold.Status == HoldStatus.Cancelled)
            {
                await transaction.CommitAsync();
                return (true, null, 204);
            }

            // Set to CANCELLED to release the seat
            hold.Status = HoldStatus.Cancelled;
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            return (true, null, 204);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error cancelling hold {HoldId}", holdId);
            throw;
        }
    }

    public async Task<IEnumerable<AdmissionHold>> GetHoldsForStudentAsync(string studentId)
    {
        if (string.IsNullOrWhiteSpace(studentId))
        {
            return Enumerable.Empty<AdmissionHold>();
        }

        var now = DateTime.UtcNow;

        var studentHolds = await _context.AdmissionHolds
            .AsNoTracking()
            .Where(h => h.StudentId == studentId)
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync();

        // Ensure in-memory view reflects real-time expiration
        foreach (var hold in studentHolds)
        {
            if (hold.Status == HoldStatus.Held && hold.ExpiresAt <= now)
            {
                hold.Status = HoldStatus.Expired;
            }
        }

        return studentHolds;
    }

    public async Task<int> CleanupExpiredHoldsAsync()
    {
        var now = DateTime.UtcNow;

        var expiredCount = await _context.AdmissionHolds
            .Where(h => h.Status == HoldStatus.Held && h.ExpiresAt <= now)
            .ExecuteUpdateAsync(s => s.SetProperty(h => h.Status, HoldStatus.Expired));

        if (expiredCount > 0)
        {
            _logger.LogInformation("Background cleanup updated {Count} expired reservation(s) to EXPIRED.", expiredCount);
        }

        return expiredCount;
    }
}
