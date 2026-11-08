namespace SchoolAdmission.Api.Models;

/// <summary>
/// Represents a specific class and shift combination (e.g. "Class 6 - Morning Shift").
/// Each class record has its own distinct seat capacity.
/// </summary>
public class ClassEntity
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int TotalSeats { get; set; }

    // Navigation property for related seat reservations
    public ICollection<AdmissionHold> Holds { get; set; } = new List<AdmissionHold>();
}
