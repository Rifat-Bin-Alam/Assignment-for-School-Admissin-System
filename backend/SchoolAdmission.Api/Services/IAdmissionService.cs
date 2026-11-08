using SchoolAdmission.Api.DTOs;
using SchoolAdmission.Api.Models;

namespace SchoolAdmission.Api.Services;

public interface IAdmissionService
{
    Task<ClassResponse> CreateClassAsync(CreateClassRequest request);
    Task<IEnumerable<ClassResponse>> GetAllClassesAsync();
    Task<ClassResponse?> GetClassByIdAsync(string classId);
    Task<(AdmissionHold? Hold, string? Error, int StatusCode)> CreateHoldAsync(string classId, string studentId);
    Task<(AdmissionHold? Hold, string? Error, int StatusCode)> GetHoldByIdAsync(string holdId);
    Task<(AdmissionHold? Hold, string? Error, int StatusCode)> ConfirmHoldAsync(string holdId, string studentId);
    Task<(bool Success, string? Error, int StatusCode)> CancelHoldAsync(string holdId, string studentId);
    Task<IEnumerable<AdmissionHold>> GetHoldsForStudentAsync(string studentId);
    Task<int> CleanupExpiredHoldsAsync();
}
