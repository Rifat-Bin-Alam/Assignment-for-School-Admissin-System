using System.Net;
using System.Net.Http.Json;
using SchoolAdmission.Api.DTOs;

namespace SchoolAdmission.Tests;

public class HoldCancellationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public HoldCancellationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CancelHold_WhenValid_ReleasesReservedSeatAndReturns204()
    {
        await _factory.ResetDatabaseAsync();

        var classRes = await _client.PostAsJsonAsync("/classes", new CreateClassRequest
        {
            Name = "Class 10 - Cancellation Test",
            TotalSeats = 5
        });
        var cls = await classRes.Content.ReadFromJsonAsync<ClassResponse>();

        var holdReq = new HttpRequestMessage(HttpMethod.Post, $"/classes/{cls!.Id}/holds");
        holdReq.Headers.Add("X-Student-Id", "STU_CANCEL_1");
        var holdRes = await _client.SendAsync(holdReq);
        var hold = await holdRes.Content.ReadFromJsonAsync<HoldResponse>();

        // Verify seat was held
        var classCheck1 = await _client.GetFromJsonAsync<ClassResponse>($"/classes/{cls.Id}");
        Assert.Equal(1, classCheck1!.HeldSeats);
        Assert.Equal(4, classCheck1.AvailableSeats);

        // Cancel the hold
        var cancelReq = new HttpRequestMessage(HttpMethod.Delete, $"/holds/{hold!.HoldId}");
        cancelReq.Headers.Add("X-Student-Id", "STU_CANCEL_1");
        var cancelRes = await _client.SendAsync(cancelReq);

        Assert.Equal(HttpStatusCode.NoContent, cancelRes.StatusCode);

        // Verify reserved seat is released immediately
        var classCheck2 = await _client.GetFromJsonAsync<ClassResponse>($"/classes/{cls.Id}");
        Assert.Equal(0, classCheck2!.HeldSeats);
        Assert.Equal(5, classCheck2.AvailableSeats);
    }

    [Fact]
    public async Task CancelHold_ByDifferentStudent_Returns403Forbidden()
    {
        await _factory.ResetDatabaseAsync();

        var classRes = await _client.PostAsJsonAsync("/classes", new CreateClassRequest
        {
            Name = "Class 10 - Cancel Ownership",
            TotalSeats = 5
        });
        var cls = await classRes.Content.ReadFromJsonAsync<ClassResponse>();

        var holdReq = new HttpRequestMessage(HttpMethod.Post, $"/classes/{cls!.Id}/holds");
        holdReq.Headers.Add("X-Student-Id", "STU_REAL_OWNER");
        var holdRes = await _client.SendAsync(holdReq);
        var hold = await holdRes.Content.ReadFromJsonAsync<HoldResponse>();

        // Different student attempts cancellation
        var cancelReq = new HttpRequestMessage(HttpMethod.Delete, $"/holds/{hold!.HoldId}");
        cancelReq.Headers.Add("X-Student-Id", "STU_OTHER");
        var cancelRes = await _client.SendAsync(cancelReq);

        Assert.Equal(HttpStatusCode.Forbidden, cancelRes.StatusCode);
    }

    [Fact]
    public async Task CancelHold_WhenNonExistent_Returns404NotFound()
    {
        await _factory.ResetDatabaseAsync();

        var cancelReq = new HttpRequestMessage(HttpMethod.Delete, "/holds/non_existent_hold");
        cancelReq.Headers.Add("X-Student-Id", "STU_ANY");
        var cancelRes = await _client.SendAsync(cancelReq);

        Assert.Equal(HttpStatusCode.NotFound, cancelRes.StatusCode);
    }
}
