using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.DTOs;
using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Controllers;

[ApiController]
[Route("me")]
public class MeController : ControllerBase
{
    private readonly IAdmissionService _admissionService;

    public MeController(IAdmissionService admissionService)
    {
        _admissionService = admissionService;
    }

    /// <summary>
    /// 8. GET /me/holds - Return all applications belonging to the current student
    /// </summary>
    [HttpGet("holds")]
    [ProducesResponseType(typeof(IEnumerable<HoldResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMyHolds([FromHeader(Name = "X-Student-Id")] string? studentId)
    {
        if (string.IsNullOrWhiteSpace(studentId))
        {
            return BadRequest(new ErrorResponse("X-Student-Id header is required."));
        }

        var holds = await _admissionService.GetHoldsForStudentAsync(studentId.Trim());

        var responseList = holds.Select(h => new HoldResponse
        {
            HoldId = h.Id,
            ClassId = h.ClassId,
            StudentId = h.StudentId,
            Status = h.Status,
            CreatedAt = h.CreatedAt,
            ExpiresAt = h.ExpiresAt
        });

        return Ok(responseList);
    }
}
