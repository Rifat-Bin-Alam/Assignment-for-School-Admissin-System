using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolAdmission.Api.Data;

namespace SchoolAdmission.Tests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string TestConnectionString = "Host=localhost;Port=5433;Database=school_admission_test;Username=postgres;Password=postgres";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Override configuration settings for tests
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DB_CONNECTION_STRING"] = TestConnectionString,
                ["HOLD_DURATION_SECONDS"] = "600",
                ["HOLD_CLEANUP_INTERVAL_SECONDS"] = "1"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Ensure EF Core uses the PostgreSQL test database
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseNpgsql(TestConnectionString);
            });

            // Ensure test database is migrated and ready
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Database.Migrate();
        });
    }

    /// <summary>
    /// Resets the database between test runs to ensure complete test isolation.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await db.AdmissionHolds.ExecuteDeleteAsync();
        await db.Classes.ExecuteDeleteAsync();
    }
}
