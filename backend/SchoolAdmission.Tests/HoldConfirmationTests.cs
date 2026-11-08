using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.DTOs;
using SchoolAdmission.Api.Models;

namespace SchoolAdmission.Tests;

public class HoldConfirmationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public HoldConfirmationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ConfirmHold_WhenValid_ConfirmsAndUpdatesConfirmedSeats()
    {
        await _factory.ResetDatabaseAsync();

        var classRes = await _client.PostAsJsonAsync("/classes", new CreateClassRequest
        {
            Name = "Class 7 - Morning",
            TotalSeats = 20
        });
        var cls = await classRes.Content.ReadFromJsonAsync<ClassResponse>();

        var holdReq = new HttpRequestMessage(HttpMethod.Post, $"/classes/{cls!.Id}/holds");
        holdReq.Headers.Add("X-Student-Id", "STU_CONFIRM_1");
        var holdRes = await _client.SendAsync(holdReq);
        var hold = await holdRes.Content.ReadFromJsonAsync<HoldResponse>();

        // Confirm hold
        var confirmReq = new HttpRequestMessage(HttpMethod.Post, $"/holds/{hold!.HoldId}/confirm");
        confirmReq.Headers.Add("X-Student-Id", "STU_CONFIRM_1");
        var confirmRes = await _client.SendAsync(confirmReq);

        Assert.Equal(HttpStatusCode.OK, confirmRes.StatusCode);
        var confirmed = await confirmRes.Content.ReadFromJsonAsync<HoldResponse>();
        Assert.NotNull(confirmed);
        Assert.Equal(HoldStatus.Confirmed, confirmed.Status);

        // Verify class counts: confirmedSeats = 1, heldSeats = 0, availableSeats = 19
        var classCheck = await _client.GetFromJsonAsync<ClassResponse>($"/classes/{cls.Id}");
        Assert.NotNull(classCheck);
        Assert.Equal(1, classCheck.ConfirmedSeats);
        Assert.Equal(0, classCheck.HeldSeats);
        Assert.Equal(19, classCheck.AvailableSeats);
    }

    /// <summary>
    /// Mandatory Test 2: Confirming the same application multiple times must not increase confirmedSeats more than once.
    /// </summary>
    [Fact]
    public async Task ConfirmHold_MultipleTimes_DoesNotIncreaseConfirmedSeatsMoreThanOnce()
    {
        await _factory.ResetDatabaseAsync();

        var classRes = await _client.PostAsJsonAsync("/classes", new CreateClassRequest
        {
            Name = "Class 7 - Morning Duplicate Test",
            TotalSeats = 10
        });
        var cls = await classRes.Content.ReadFromJsonAsync<ClassResponse>();

        var holdReq = new HttpRequestMessage(HttpMethod.Post, $"/classes/{cls!.Id}/holds");
        holdReq.Headers.Add("X-Student-Id", "STU_IDEMPOTENT");
        var holdRes = await _client.SendAsync(holdReq);
        var hold = await holdRes.Content.ReadFromJsonAsync<HoldResponse>();

        // First confirmation
        var confirm1 = new HttpRequestMessage(HttpMethod.Post, $"/holds/{hold!.HoldId}/confirm");
        confirm1.Headers.Add("X-Student-Id", "STU_IDEMPOTENT");
        var confirmRes1 = await _client.SendAsync(confirm1);
        Assert.Equal(HttpStatusCode.OK, confirmRes1.StatusCode);

        // Second confirmation (duplicate)
        var confirm2 = new HttpRequestMessage(HttpMethod.Post, $"/holds/{hold.HoldId}/confirm");
        confirm2.Headers.Add("X-Student-Id", "STU_IDEMPOTENT");
        var confirmRes2 = await _client.SendAsync(confirm2);
        Assert.Equal(HttpStatusCode.OK, confirmRes2.StatusCode);

        // Third confirmation (duplicate)
        var confirm3 = new HttpRequestMessage(HttpMethod.Post, $"/holds/{hold.HoldId}/confirm");
        confirm3.Headers.Add("X-Student-Id", "STU_IDEMPOTENT");
        var confirmRes3 = await _client.SendAsync(confirm3);
        Assert.Equal(HttpStatusCode.OK, confirmRes3.StatusCode);

        // Verify confirmed seats is still EXACTLY 1
        var classCheck = await _client.GetFromJsonAsync<ClassResponse>($"/classes/{cls.Id}");
        Assert.NotNull(classCheck);
        Assert.Equal(1, classCheck.ConfirmedSeats);
        Assert.Equal(0, classCheck.HeldSeats);
        Assert.Equal(9, classCheck.AvailableSeats);
    }

    /// <summary>
    /// Application ownership test: Student can only confirm their own reservation.
    /// </summary>
    [Fact]
    public async Task ConfirmHold_ByDifferentStudent_Returns403Forbidden()
    {
        await _factory.ResetDatabaseAsync();

        var classRes = await _client.PostAsJsonAsync("/classes", new CreateClassRequest
        {
            Name = "Class 8 - Day Ownership",
            TotalSeats = 15
        });
        var cls = await classRes.Content.ReadFromJsonAsync<ClassResponse>();

        var holdReq = new HttpRequestMessage(HttpMethod.Post, $"/classes/{cls!.Id}/holds");
        holdReq.Headers.Add("X-Student-Id", "STU_ORIGINAL_OWNER");
        var holdRes = await _client.SendAsync(holdReq);
        var hold = await holdRes.Content.ReadFromJsonAsync<HoldResponse>();

        // Another student attempts to confirm STU_ORIGINAL_OWNER's hold
        var impostorReq = new HttpRequestMessage(HttpMethod.Post, $"/holds/{hold!.HoldId}/confirm");
        impostorReq.Headers.Add("X-Student-Id", "STU_IMPOSTOR");
        var impostorRes = await _client.SendAsync(impostorReq);

        Assert.Equal(HttpStatusCode.Forbidden, impostorRes.StatusCode);
    }

    [Fact]
    public async Task ConfirmHold_WhenHoldExpired_Returns400BadRequest()
    {
        await _factory.ResetDatabaseAsync();

        var classRes = await _client.PostAsJsonAsync("/classes", new CreateClassRequest
        {
            Name = "Class 9 - Expired Hold Test",
            TotalSeats = 10
        });
        var cls = await classRes.Content.ReadFromJsonAsync<ClassResponse>();

        // Insert a hold that is already expired
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var expiredHold = new AdmissionHold
            {
                Id = "h_expired_test_1",
                ClassId = cls!.Id,
                StudentId = "STU_EXPIRED_USER",
                Status = HoldStatus.Held,
                CreatedAt = DateTime.UtcNow.AddMinutes(-20),
                ExpiresAt = DateTime.UtcNow.AddMinutes(-10) // Expired 10 mins ago
            };
            db.AdmissionHolds.Add(expiredHold);
            await db.SaveChangesAsync();
        }

        // Attempt to confirm the expired hold
        var confirmReq = new HttpRequestMessage(HttpMethod.Post, "/holds/h_expired_test_1/confirm");
        confirmReq.Headers.Add("X-Student-Id", "STU_EXPIRED_USER");
        var confirmRes = await _client.SendAsync(confirmReq);

        Assert.Equal(HttpStatusCode.BadRequest, confirmRes.StatusCode);
    }
}
