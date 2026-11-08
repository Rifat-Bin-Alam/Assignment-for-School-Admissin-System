using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.DTOs;
using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Controllers;

[ApiController]
[Route("holds")]
public class HoldsController : ControllerBase
{
    private readonly IAdmissionService _admissionService;

    public HoldsController(IAdmissionService admissionService)
    {
        _admissionService = admissionService;
    }

    /// <summary>
    /// 5. GET /holds/{holdId} - Return current reservation information
    /// </summary>
    [HttpGet("{holdId}")]
    [ProducesResponseType(typeof(HoldResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHoldById(string holdId)
    {
        var (hold, error, statusCode) = await _admissionService.GetHoldByIdAsync(holdId);

        if (error != null)
        {
            return StatusCode(statusCode, new ErrorResponse(error));
        }

        var response = new HoldResponse
        {
            HoldId = hold!.Id,
            ClassId = hold.ClassId,
            StudentId = hold.StudentId,
            Status = hold.Status,
            CreatedAt = hold.CreatedAt,
            ExpiresAt = hold.ExpiresAt
        };

        return Ok(response);
    }

    /// <summary>
    /// 6. POST /holds/{holdId}/confirm - Confirm an admission
    /// </summary>
    [HttpPost("{holdId}/confirm")]
    [ProducesResponseType(typeof(HoldResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConfirmHold(
        string holdId,
        [FromHeader(Name = "X-Student-Id")] string? studentId)
    {
        if (string.IsNullOrWhiteSpace(studentId))
        {
            return BadRequest(new ErrorResponse("X-Student-Id header is required."));
        }

        var (hold, error, statusCode) = await _admissionService.ConfirmHoldAsync(holdId, studentId.Trim());

        if (error != null)
        {
            return StatusCode(statusCode, new ErrorResponse(error));
        }

        var response = new HoldResponse
        {
            HoldId = hold!.Id,
            ClassId = hold.ClassId,
            StudentId = hold.StudentId,
            Status = hold.Status,
            CreatedAt = hold.CreatedAt,
            ExpiresAt = hold.ExpiresAt
        };

        return Ok(response);
    }

    /// <summary>
    /// 7. DELETE /holds/{holdId} - Cancel a reservation
    /// </summary>
    [HttpDelete("{holdId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelHold(
        string holdId,
        [FromHeader(Name = "X-Student-Id")] string? studentId)
    {
        if (string.IsNullOrWhiteSpace(studentId))
        {
            return BadRequest(new ErrorResponse("X-Student-Id header is required."));
        }

        var (success, error, statusCode) = await _admissionService.CancelHoldAsync(holdId, studentId.Trim());

        if (!success)
        {
            return StatusCode(statusCode, new ErrorResponse(error ?? "Unable to cancel reservation."));
        }

        return NoContent();
    }
}
