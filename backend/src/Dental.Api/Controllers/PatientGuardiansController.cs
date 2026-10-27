using Dental.Application.Common;
using Dental.Application.Features.Patients.DTOs;
using Dental.Application.Features.Patients.Services;
using Dental.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppPolicies = Dental.Api.Policies.Policies;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api/patient-guardians")]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PatientGuardiansController(
    PatientGuardianService guardianService,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Gán quyền giám hộ cho bệnh nhân (Admin, Receptionist tại quầy).</summary>
    [HttpPost("assign")]
    [Authorize(Policy = AppPolicies.PatientManage)]
    public async Task<IActionResult> AssignGuardian([FromBody] AssignPatientGuardianRequest request, CancellationToken ct)
    {
        if (currentUser.UserId is not int actorUserId)
            return Unauthorized();

        var result = await guardianService.AssignGuardianAsync(
            request,
            actorUserId,
            currentUser.RoleCode,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            ct);

        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>Thu hồi quyền giám hộ (Admin, Receptionist tại quầy).</summary>
    [HttpPost("wards/{wardPatientId:int}/revoke")]
    [Authorize(Policy = AppPolicies.PatientManage)]
    public async Task<IActionResult> RevokeGuardian(
        int wardPatientId,
        [FromBody] RevokePatientGuardianRequest request,
        CancellationToken ct)
    {
        if (currentUser.UserId is not int actorUserId)
            return Unauthorized();

        var result = await guardianService.RevokeGuardianAsync(
            wardPatientId,
            request,
            actorUserId,
            currentUser.RoleCode,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            ct);

        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>Tách tài khoản độc lập cho trẻ khi có CCCD, tự động chấm dứt giám hộ cùng transaction (DL-359).</summary>
    [HttpPost("wards/{wardPatientId:int}/promote-independent")]
    [Authorize(Policy = AppPolicies.PatientManage)]
    public async Task<IActionResult> PromoteIndependent(
        int wardPatientId,
        [FromBody] PromoteWardRequest request,
        CancellationToken ct)
    {
        if (currentUser.UserId is not int actorUserId)
            return Unauthorized();

        var result = await guardianService.PromoteWardToIndependentPatientAsync(
            wardPatientId,
            request,
            actorUserId,
            currentUser.RoleCode,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            ct);

        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>Lấy thông tin giám hộ hiệu lực của trẻ (Nhân viên hoặc Giám hộ/Chính bệnh nhân).</summary>
    [HttpGet("wards/{wardPatientId:int}/active")]
    [Authorize(Policy = AppPolicies.PatientView)]
    public async Task<IActionResult> GetActiveGuardian(int wardPatientId, CancellationToken ct)
    {
        if (currentUser.UserId is not int actorUserId)
            return Unauthorized();

        var result = await guardianService.GetActiveGuardianForWardAsync(
            wardPatientId,
            actorUserId,
            currentUser.RoleCode,
            ct);

        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>Lịch sử thay đổi người giám hộ của trẻ (Admin, Receptionist).</summary>
    [HttpGet("wards/{wardPatientId:int}/history")]
    [Authorize(Policy = AppPolicies.PatientManage)]
    public async Task<IActionResult> GetWardGuardianHistory(int wardPatientId, CancellationToken ct)
    {
        if (currentUser.UserId is not int actorUserId)
            return Unauthorized();

        var result = await guardianService.GetWardGuardianHistoryAsync(
            wardPatientId,
            actorUserId,
            currentUser.RoleCode,
            ct);

        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    /// <summary>Bệnh nhân lấy danh sách các trẻ mình đang đại diện hợp pháp.</summary>
    [HttpGet("my-wards")]
    [Authorize(Policy = AppPolicies.PatientView)]
    public async Task<IActionResult> GetMyWards(CancellationToken ct)
    {
        if (currentUser.UserId is not int actorUserId)
            return Unauthorized();

        var result = await guardianService.GetMyWardsAsync(
            actorUserId,
            currentUser.RoleCode,
            ct);

        return result.IsSuccess ? Ok(result.Value) : Failure(result.Error);
    }

    private IActionResult Failure(Error error)
    {
        var statusCode = error.Code switch
        {
            "GEN_001" or "PAT_GUARDIAN_NOT_FOUND" or "PAT_WARD_NOT_FOUND" => StatusCodes.Status404NotFound,
            "GEN_002" => StatusCodes.Status403Forbidden,
            "PAT_GUARDIAN_CONFLICT" or "PAT_PROMOTE_CONFLICT" or "PAT_WARD_ALREADY_HAS_GUARDIAN" or "PAT_CITIZEN_ID_DUPLICATE" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };

        return StatusCode(statusCode, new { code = error.Code, message = error.Message });
    }
}
