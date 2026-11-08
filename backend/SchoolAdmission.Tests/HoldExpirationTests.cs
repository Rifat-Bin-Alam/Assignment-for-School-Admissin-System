using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.DTOs;
using SchoolAdmission.Api.Models;
using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Tests;

public class HoldExpirationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public HoldExpirationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    /// <summary>
    /// Mandatory Test 1: Hold expiration test.
    /// Verifies that when a hold expires, its seat automatically becomes available
    /// without requiring any manual API request to release it.
    /// Also verifies that expired holds transition to EXPIRED and cannot be confirmed.
    /// </summary>
    [Fact]
    public async Task ExpiredHold_AutomaticallyReleasesSeat_WithoutManualIntervention()
    {
        await _factory.ResetDatabaseAsync();

        // 1. Create a class with exactly 1 seat
        var classRes = await _client.PostAsJsonAsync("/classes", new CreateClassRequest
        {
            Name = "Class 6 - Strict Capacity 1",
            TotalSeats = 1
        });
        var cls = await classRes.Content.ReadFromJsonAsync<ClassResponse>();
        Assert.NotNull(cls);

        // 2. Insert a hold that expired 5 minutes ago
        string expiredHoldId = "h_expired_seat_test";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hold = new AdmissionHold
            {
                Id = expiredHoldId,
                ClassId = cls.Id,
                StudentId = "STU_EXPIRED_SEAT",
                Status = HoldStatus.Held,
                CreatedAt = DateTime.UtcNow.AddMinutes(-15),
                ExpiresAt = DateTime.UtcNow.AddMinutes(-5) // Expired in the past
            };
            db.AdmissionHolds.Add(hold);
            await db.SaveChangesAsync();
        }

        // 3. Query the class seat information.
        // Rule 4: The seat must become available automatically without any manual release API call.
        var classCheck = await _client.GetFromJsonAsync<ClassResponse>($"/classes/{cls.Id}");
        Assert.NotNull(classCheck);
        Assert.Equal(0, classCheck.HeldSeats); // Expired hold does NOT count as held
        Assert.Equal(0, classCheck.ConfirmedSeats);
        Assert.Equal(1, classCheck.AvailableSeats); // Seat is automatically available!

        // 4. Verify that trying to confirm the expired hold is rejected
        var confirmReq = new HttpRequestMessage(HttpMethod.Post, $"/holds/{expiredHoldId}/confirm");
        confirmReq.Headers.Add("X-Student-Id", "STU_EXPIRED_SEAT");
        var confirmRes = await _client.SendAsync(confirmReq);
        Assert.Equal(HttpStatusCode.BadRequest, confirmRes.StatusCode);

        // 5. Verify the hold status in database is now EXPIRED
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var dbHold = await db.AdmissionHolds.FindAsync(expiredHoldId);
            Assert.NotNull(dbHold);
            Assert.Equal(HoldStatus.Expired, dbHold.Status);
        }

        // 6. Verify that another student can now successfully reserve this freed seat
        var newHoldReq = new HttpRequestMessage(HttpMethod.Post, $"/classes/{cls.Id}/holds");
        newHoldReq.Headers.Add("X-Student-Id", "STU_NEW_APPLICANT");
        var newHoldRes = await _client.SendAsync(newHoldReq);

        Assert.Equal(HttpStatusCode.Created, newHoldRes.StatusCode);
        var newHold = await newHoldRes.Content.ReadFromJsonAsync<HoldResponse>();
        Assert.NotNull(newHold);
        Assert.Equal(HoldStatus.Held, newHold.Status);

        // Verify class seat count reflects 1 held seat, 0 available seats
        var classCheckAfter = await _client.GetFromJsonAsync<ClassResponse>($"/classes/{cls.Id}");
        Assert.NotNull(classCheckAfter);
        Assert.Equal(1, classCheckAfter.HeldSeats);
        Assert.Equal(0, classCheckAfter.AvailableSeats);
    }
}
