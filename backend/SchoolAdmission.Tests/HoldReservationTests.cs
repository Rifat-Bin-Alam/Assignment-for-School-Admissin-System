using System.Net;
using System.Net.Http.Json;
using SchoolAdmission.Api.DTOs;
using SchoolAdmission.Api.Models;

namespace SchoolAdmission.Tests;

public class HoldReservationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public HoldReservationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateHold_WithValidData_Returns201CreatedAndHeldStatus()
    {
        await _factory.ResetDatabaseAsync();

        var classResponse = await _client.PostAsJsonAsync("/classes", new CreateClassRequest
        {
            Name = "Class 8 - Morning Shift",
            TotalSeats = 25
        });
        var cls = await classResponse.Content.ReadFromJsonAsync<ClassResponse>();

        var request = new HttpRequestMessage(HttpMethod.Post, $"/classes/{cls!.Id}/holds");
        request.Headers.Add("X-Student-Id", "STU_1001");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var hold = await response.Content.ReadFromJsonAsync<HoldResponse>();
        Assert.NotNull(hold);
        Assert.False(string.IsNullOrWhiteSpace(hold.HoldId));
        Assert.Equal(cls.Id, hold.ClassId);
        Assert.Equal("STU_1001", hold.StudentId);
        Assert.Equal(HoldStatus.Held, hold.Status);
        Assert.True(hold.ExpiresAt > DateTime.UtcNow);

        // Verify class seat count was updated
        var classCheck = await _client.GetFromJsonAsync<ClassResponse>($"/classes/{cls.Id}");
        Assert.NotNull(classCheck);
        Assert.Equal(1, classCheck.HeldSeats);
        Assert.Equal(24, classCheck.AvailableSeats);
    }

    [Fact]
    public async Task CreateHold_WithoutStudentId_Returns400BadRequest()
    {
        await _factory.ResetDatabaseAsync();

        var classResponse = await _client.PostAsJsonAsync("/classes", new CreateClassRequest
        {
            Name = "Class 9 - Day Shift",
            TotalSeats = 30
        });
        var cls = await classResponse.Content.ReadFromJsonAsync<ClassResponse>();

        // No X-Student-Id header
        var request = new HttpRequestMessage(HttpMethod.Post, $"/classes/{cls!.Id}/holds");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateHold_WhenNoSeatsAvailable_Returns409Conflict()
    {
        await _factory.ResetDatabaseAsync();

        // Create a class with only 1 seat
        var classResponse = await _client.PostAsJsonAsync("/classes", new CreateClassRequest
        {
            Name = "Class 10 - Limited Capacity",
            TotalSeats = 1
        });
        var cls = await classResponse.Content.ReadFromJsonAsync<ClassResponse>();

        // Student 1 reserves the only seat
        var req1 = new HttpRequestMessage(HttpMethod.Post, $"/classes/{cls!.Id}/holds");
        req1.Headers.Add("X-Student-Id", "STU_FIRST");
        var res1 = await _client.SendAsync(req1);
        Assert.Equal(HttpStatusCode.Created, res1.StatusCode);

        // Student 2 tries to reserve a seat when none are available
        var req2 = new HttpRequestMessage(HttpMethod.Post, $"/classes/{cls.Id}/holds");
        req2.Headers.Add("X-Student-Id", "STU_SECOND");
        var res2 = await _client.SendAsync(req2);

        Assert.Equal(HttpStatusCode.Conflict, res2.StatusCode);
    }

    [Fact]
    public async Task CreateHold_WhenStudentAlreadyHasActiveHold_Returns409Conflict()
    {
        await _factory.ResetDatabaseAsync();

        var classResponse = await _client.PostAsJsonAsync("/classes", new CreateClassRequest
        {
            Name = "Class 6 - Day Shift",
            TotalSeats = 50
        });
        var cls = await classResponse.Content.ReadFromJsonAsync<ClassResponse>();

        // First hold by STU_DUPLICATE
        var req1 = new HttpRequestMessage(HttpMethod.Post, $"/classes/{cls!.Id}/holds");
        req1.Headers.Add("X-Student-Id", "STU_DUPLICATE");
        var res1 = await _client.SendAsync(req1);
        Assert.Equal(HttpStatusCode.Created, res1.StatusCode);

        // Second hold attempt by the SAME student in the same class
        var req2 = new HttpRequestMessage(HttpMethod.Post, $"/classes/{cls.Id}/holds");
        req2.Headers.Add("X-Student-Id", "STU_DUPLICATE");
        var res2 = await _client.SendAsync(req2);

        Assert.Equal(HttpStatusCode.Conflict, res2.StatusCode);
    }
}
