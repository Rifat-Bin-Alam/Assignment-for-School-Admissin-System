using System.Net;
using System.Net.Http.Json;
using SchoolAdmission.Api.DTOs;

namespace SchoolAdmission.Tests;

public class ConcurrencyTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public ConcurrencyTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    /// <summary>
    /// Mandatory Test 3: Concurrency test.
    /// Sends multiple simultaneous reservation requests when only ONE seat remains
    /// and verifies that EXACTLY ONE reservation succeeds, while all other concurrent
    /// requests receive HTTP 409 Conflict without exceeding capacity.
    /// Runs against a REAL PostgreSQL 18 database with row-level locks and transactions.
    /// </summary>
    [Fact]
    public async Task SimultaneousRequests_WhenOneSeatRemains_OnlyOneReservationSucceeds()
    {
        await _factory.ResetDatabaseAsync();

        // 1. Create a class with exactly 1 available seat
        var classRes = await _client.PostAsJsonAsync("/classes", new CreateClassRequest
        {
            Name = "Class 6 - Ultra Competitive Morning Shift",
            TotalSeats = 1
        });
        var cls = await classRes.Content.ReadFromJsonAsync<ClassResponse>();
        Assert.NotNull(cls);

        // 2. Prepare 15 simultaneous requests from 15 distinct students
        const int concurrentStudents = 15;
        var startBarrier = new TaskCompletionSource();

        var tasks = Enumerable.Range(0, concurrentStudents).Select(async i =>
        {
            var studentId = $"STU_CONCURRENT_{i}";

            // Wait for signal so all requests fire simultaneously
            await startBarrier.Task;

            var req = new HttpRequestMessage(HttpMethod.Post, $"/classes/{cls.Id}/holds");
            req.Headers.Add("X-Student-Id", studentId);

            return await _client.SendAsync(req);
        }).ToList();

        // Release the barrier to fire all requests at the exact same moment
        startBarrier.SetResult();

        var responses = await Task.WhenAll(tasks);

        // 3. Analyze results
        var successCount = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var conflictCount = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);
        var error500Count = responses.Count(r => r.StatusCode == HttpStatusCode.InternalServerError);

        // Assert: Exactly ONE reservation succeeded
        Assert.Equal(1, successCount);

        // Assert: All remaining 14 requests failed with 409 Conflict
        Assert.Equal(concurrentStudents - 1, conflictCount);

        // Assert: Never an unexpected 500 error
        Assert.Equal(0, error500Count);

        // 4. Verify class state in PostgreSQL
        var classCheck = await _client.GetFromJsonAsync<ClassResponse>($"/classes/{cls.Id}");
        Assert.NotNull(classCheck);
        Assert.Equal(1, classCheck.HeldSeats);
        Assert.Equal(0, classCheck.AvailableSeats);
    }

    /// <summary>
    /// Additional Concurrency Test:
    /// Tests a class with 3 seats under a barrage of 20 simultaneous applicants.
    /// Verifies that exactly 3 succeed and capacity is strictly respected.
    /// </summary>
    [Fact]
    public async Task SimultaneousRequests_WithThreeSeats_ExactlyThreeSucceed()
    {
        await _factory.ResetDatabaseAsync();

        const int totalSeats = 3;
        const int totalApplicants = 20;

        var classRes = await _client.PostAsJsonAsync("/classes", new CreateClassRequest
        {
            Name = "Class 7 - Limited 3 Seats",
            TotalSeats = totalSeats
        });
        var cls = await classRes.Content.ReadFromJsonAsync<ClassResponse>();
        Assert.NotNull(cls);

        var startBarrier = new TaskCompletionSource();

        var tasks = Enumerable.Range(0, totalApplicants).Select(async i =>
        {
            var studentId = $"STU_BATCH_{i}";

            await startBarrier.Task;

            var req = new HttpRequestMessage(HttpMethod.Post, $"/classes/{cls.Id}/holds");
            req.Headers.Add("X-Student-Id", studentId);

            return await _client.SendAsync(req);
        }).ToList();

        startBarrier.SetResult();

        var responses = await Task.WhenAll(tasks);

        var successCount = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var conflictCount = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        Assert.Equal(totalSeats, successCount);
        Assert.Equal(totalApplicants - totalSeats, conflictCount);

        var classCheck = await _client.GetFromJsonAsync<ClassResponse>($"/classes/{cls.Id}");
        Assert.NotNull(classCheck);
        Assert.Equal(totalSeats, classCheck.HeldSeats);
        Assert.Equal(0, classCheck.AvailableSeats);
    }
}
