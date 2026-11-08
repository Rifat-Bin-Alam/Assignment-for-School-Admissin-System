using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.DTOs;
using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Controllers;

[ApiController]
[Route("classes")]
public class ClassesController : ControllerBase
{
    private readonly IAdmissionService _admissionService;

    public ClassesController(IAdmissionService admissionService)
    {
        _admissionService = admissionService;
    }

    /// <summary>
    /// 1. POST /classes - Create a new class
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ClassResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateClass([FromBody] CreateClassRequest request)
    {
        if (!ModelState.IsValid)
        {
            var firstError = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .FirstOrDefault() ?? "Invalid class data.";
            return BadRequest(new ErrorResponse(firstError));
        }

        var created = await _admissionService.CreateClassAsync(request);
        return CreatedAtAction(nameof(GetClassById), new { classId = created.Id }, created);
    }

    /// <summary>
    /// 2. GET /classes - Return all classes with seat statistics
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ClassResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllClasses()
    {
        var classes = await _admissionService.GetAllClassesAsync();
        return Ok(classes);
    }

    /// <summary>
    /// 3. GET /classes/{classId} - Return one class with seat information
    /// </summary>
    [HttpGet("{classId}")]
    [ProducesResponseType(typeof(ClassResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetClassById(string classId)
    {
        var classResponse = await _admissionService.GetClassByIdAsync(classId);
        if (classResponse == null)
        {
            return NotFound(new ErrorResponse($"Class '{classId}' was not found."));
        }

        return Ok(classResponse);
    }

    /// <summary>
    /// 4. POST /classes/{classId}/holds - Create a temporary seat reservation
    /// </summary>
    [HttpPost("{classId}/holds")]
    [ProducesResponseType(typeof(HoldResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateHold(
        string classId,
        [FromHeader(Name = "X-Student-Id")] string? studentIdHeader,
        [FromBody] CreateHoldRequest? bodyRequest)
    {
        // Support student ID from header (primary) or optional request body
        var studentId = !string.IsNullOrWhiteSpace(studentIdHeader) 
            ? studentIdHeader.Trim() 
            : bodyRequest?.StudentId?.Trim();

        if (string.IsNullOrWhiteSpace(studentId))
        {
            return BadRequest(new ErrorResponse("X-Student-Id header is required."));
        }

        var (hold, error, statusCode) = await _admissionService.CreateHoldAsync(classId, studentId);

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
            ExpiresAt = hold.ExpiresAt
        };

        return Created($"/holds/{hold.Id}", response);
    }
}
