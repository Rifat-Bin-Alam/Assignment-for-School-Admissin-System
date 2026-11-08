namespace SchoolAdmission.Api.Services;

/// <summary>
/// Background worker that periodically scans for expired HELD reservations
/// and updates their status to EXPIRED in the database.
/// </summary>
public class HoldCleanupBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HoldCleanupBackgroundService> _logger;

    public HoldCleanupBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<HoldCleanupBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = GetIntervalSeconds();
        _logger.LogInformation("Hold cleanup background service started with interval of {Interval} seconds.", intervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);

                using var scope = _scopeFactory.CreateScope();
                var admissionService = scope.ServiceProvider.GetRequiredService<IAdmissionService>();

                await admissionService.CleanupExpiredHoldsAsync();
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown, exit loop
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred during expired holds background cleanup.");
            }
        }

        _logger.LogInformation("Hold cleanup background service stopped.");
    }

    private int GetIntervalSeconds()
    {
        var configured = _configuration["HOLD_CLEANUP_INTERVAL_SECONDS"];
        if (int.TryParse(configured, out var seconds) && seconds > 0)
        {
            return seconds;
        }

        return 10;
    }
}
