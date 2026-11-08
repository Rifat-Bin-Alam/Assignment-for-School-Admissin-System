using System.Text.Json.Serialization;

namespace SchoolAdmission.Api.DTOs;

public class HoldResponse
{
    public string HoldId { get; set; } = string.Empty;
    public string ClassId { get; set; } = string.Empty;
    public string StudentId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }
}
