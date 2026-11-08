using System.ComponentModel.DataAnnotations;

namespace SchoolAdmission.Api.DTOs;

public class CreateClassRequest
{
    [Required(ErrorMessage = "Class name is required.")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Class name must be between 1 and 200 characters.")]
    public string Name { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Total seats must be greater than 0.")]
    public int TotalSeats { get; set; }
}
