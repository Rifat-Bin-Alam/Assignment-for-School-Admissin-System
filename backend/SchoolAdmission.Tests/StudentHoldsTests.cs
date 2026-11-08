using System.Net;
using System.Net.Http.Json;
using SchoolAdmission.Api.DTOs;

namespace SchoolAdmission.Tests;

public class StudentHoldsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public StudentHoldsTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetMyHolds_WhenStudentHasApplications_Returns200WithHoldList()
    {
        await _factory.ResetDatabaseAsync();

        // Create two distinct classes
        var classRes1 = await _client.PostAsJsonAsync("/classes", new CreateClassRequest
        {
            Name = "Class 6 - Morning",
            TotalSeats = 10
        });
        var cls1 = await classRes1.Content.ReadFromJsonAsync<ClassResponse>();

        var classRes2 = await _client.PostAsJsonAsync("/classes", new CreateClassRequest
        {
            Name = "Class 7 - Morning",
            TotalSeats = 10
        });
        var cls2 = await classRes2.Content.ReadFromJsonAsync<ClassResponse>();

        const string targetStudent = "STU_MY_HOLDS_USER";

        // Student applies to class 1
        var holdReq1 = new HttpRequestMessage(HttpMethod.Post, $"/classes/{cls1!.Id}/holds");
        holdReq1.Headers.Add("X-Student-Id", targetStudent);
        await _client.SendAsync(holdReq1);

        // Student applies to class 2
        var holdReq2 = new HttpRequestMessage(HttpMethod.Post, $"/classes/{cls2!.Id}/holds");
        holdReq2.Headers.Add("X-Student-Id", targetStudent);
        await _client.SendAsync(holdReq2);

        // Call GET /me/holds
        var meReq = new HttpRequestMessage(HttpMethod.Get, "/me/holds");
        meReq.Headers.Add("X-Student-Id", targetStudent);
        var meRes = await _client.SendAsync(meReq);

        Assert.Equal(HttpStatusCode.OK, meRes.StatusCode);
        var holds = await meRes.Content.ReadFromJsonAsync<List<HoldResponse>>();
        Assert.NotNull(holds);
        Assert.Equal(2, holds.Count);
        Assert.All(holds, h => Assert.Equal(targetStudent, h.StudentId));
    }

    [Fact]
    public async Task GetMyHolds_WithoutHeader_Returns400BadRequest()
    {
        await _factory.ResetDatabaseAsync();

        var meReq = new HttpRequestMessage(HttpMethod.Get, "/me/holds");
        // No X-Student-Id header
        var meRes = await _client.SendAsync(meReq);

        Assert.Equal(HttpStatusCode.BadRequest, meRes.StatusCode);
    }
}
