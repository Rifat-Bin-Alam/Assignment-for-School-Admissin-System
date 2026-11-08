using System.Net;
using System.Net.Http.Json;
using SchoolAdmission.Api.DTOs;

namespace SchoolAdmission.Tests;

public class ClassTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public ClassTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateClass_WithValidData_Returns201CreatedAndInitialSeats()
    {
        await _factory.ResetDatabaseAsync();

        var request = new CreateClassRequest
        {
            Name = "Class 6 - Morning Shift",
            TotalSeats = 60
        };

        var response = await _client.PostAsJsonAsync("/classes", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<ClassResponse>();
        Assert.NotNull(content);
        Assert.False(string.IsNullOrWhiteSpace(content.Id));
        Assert.Equal("Class 6 - Morning Shift", content.Name);
        Assert.Equal(60, content.TotalSeats);
        Assert.Equal(0, content.HeldSeats);
        Assert.Equal(0, content.ConfirmedSeats);
        Assert.Equal(60, content.AvailableSeats);
    }

    [Fact]
    public async Task CreateClass_WithInvalidData_Returns400BadRequest()
    {
        await _factory.ResetDatabaseAsync();

        var invalidRequest = new CreateClassRequest
        {
            Name = "", // Invalid: empty name
            TotalSeats = 0 // Invalid: seats must be > 0
        };

        var response = await _client.PostAsJsonAsync("/classes", invalidRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetClassById_WhenClassExists_Returns200WithSeatStatistics()
    {
        await _factory.ResetDatabaseAsync();

        var createResponse = await _client.PostAsJsonAsync("/classes", new CreateClassRequest
        {
            Name = "Class 7 - Day Shift",
            TotalSeats = 40
        });
        var created = await createResponse.Content.ReadFromJsonAsync<ClassResponse>();

        var getResponse = await _client.GetAsync($"/classes/{created!.Id}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = await getResponse.Content.ReadFromJsonAsync<ClassResponse>();
        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal(40, fetched.AvailableSeats);
    }

    [Fact]
    public async Task GetClassById_WhenClassDoesNotExist_Returns404NotFound()
    {
        await _factory.ResetDatabaseAsync();

        var response = await _client.GetAsync("/classes/non_existent_class_id");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
