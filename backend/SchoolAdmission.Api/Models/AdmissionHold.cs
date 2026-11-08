namespace SchoolAdmission.Api.Models;

/// <summary>
/// Represents a temporary seat reservation or confirmed admission for a student in a class.
/// </summary>
public class AdmissionHold
{
    public string Id { get; set; } = string.Empty;

    public string ClassId { get; set; } = string.Empty;

    public string StudentId { get; set; } = string.Empty;

    public string Status { get; set; } = HoldStatus.Held;

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    // Navigation property pointing to the associated class
    public ClassEntity? Class { get; set; }
}
