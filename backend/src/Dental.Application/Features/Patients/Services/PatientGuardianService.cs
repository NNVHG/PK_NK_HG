using Dental.Application.Common;
using Dental.Application.Features.Patients.DTOs;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using FluentValidation;

namespace Dental.Application.Features.Patients.Services;

public sealed class PatientGuardianService(
    IPatientGuardianRepository repository,
    IValidator<AssignPatientGuardianRequest> assignValidator,
    IValidator<RevokePatientGuardianRequest> revokeValidator,
    IValidator<PromoteWardRequest> promoteValidator)
{
    public async Task<Result<PatientGuardianResponse>> AssignGuardianAsync(
        AssignPatientGuardianRequest request,
        int actorUserId,
        string? actorRoleCode,
        string? ipAddress,
        CancellationToken ct = default)
    {
        if (actorRoleCode is not (RoleCodes.Admin or RoleCodes.Receptionist) || actorUserId <= 0)
            return Result<PatientGuardianResponse>.Failure(Error.Forbidden);

        var validation = await assignValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return Result<PatientGuardianResponse>.Failure(Error.Validation.Code, validation.Errors[0].ErrorMessage);

        return await repository.AssignGuardianAsync(request, actorUserId, ipAddress, ct);
    }

    public async Task<Result<PatientGuardianResponse>> RevokeGuardianAsync(
        int wardPatientId,
        RevokePatientGuardianRequest request,
        int actorUserId,
        string? actorRoleCode,
        string? ipAddress,
        CancellationToken ct = default)
    {
        if (actorRoleCode is not (RoleCodes.Admin or RoleCodes.Receptionist) || actorUserId <= 0)
            return Result<PatientGuardianResponse>.Failure(Error.Forbidden);

        if (wardPatientId <= 0)
            return Result<PatientGuardianResponse>.Failure(Error.Validation);

        var validation = await revokeValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return Result<PatientGuardianResponse>.Failure(Error.Validation.Code, validation.Errors[0].ErrorMessage);

        return await repository.RevokeGuardianAsync(wardPatientId, actorUserId, request, ipAddress, ct);
    }

    public async Task<Result<PatientResponse>> PromoteWardToIndependentPatientAsync(
        int wardPatientId,
        PromoteWardRequest request,
        int actorUserId,
        string? actorRoleCode,
        string? ipAddress,
        CancellationToken ct = default)
    {
        if (actorRoleCode is not (RoleCodes.Admin or RoleCodes.Receptionist) || actorUserId <= 0)
            return Result<PatientResponse>.Failure(Error.Forbidden);

        if (wardPatientId <= 0)
            return Result<PatientResponse>.Failure(Error.Validation);

        var validation = await promoteValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return Result<PatientResponse>.Failure(Error.Validation.Code, validation.Errors[0].ErrorMessage);

        return await repository.PromoteWardToIndependentPatientAsync(wardPatientId, request, actorUserId, ipAddress, ct);
    }

    public async Task<Result<PatientGuardianResponse>> GetActiveGuardianForWardAsync(
        int wardPatientId,
        int actorUserId,
        string? actorRoleCode,
        CancellationToken ct = default)
    {
        if (wardPatientId <= 0 || actorUserId <= 0)
            return Result<PatientGuardianResponse>.Failure(Error.Validation);

        var record = await repository.GetActiveByWardPatientIdAsync(wardPatientId, ct);
        if (record is null)
            return Result<PatientGuardianResponse>.Failure(Error.NotFound);

        if (actorRoleCode == RoleCodes.Patient)
        {
            var isGuardian = record.GuardianPatient?.UserId == actorUserId;
            var isSelfWard = record.WardPatient?.UserId == actorUserId;
            if (!isGuardian && !isSelfWard)
                return Result<PatientGuardianResponse>.Failure(Error.NotFound);
        }

        return Result<PatientGuardianResponse>.Success(new PatientGuardianResponse(
            record.PatientGuardianId,
            record.GuardianPatientId,
            record.GuardianPatient?.PatientCode ?? "",
            record.GuardianPatient?.FullName ?? "",
            record.WardPatientId,
            record.WardPatient?.PatientCode ?? "",
            record.WardPatient?.FullName ?? "",
            record.Relationship,
            record.IsActive,
            record.AssignedAt,
            record.AssignedByUserId,
            record.AssignedByUser?.FullName ?? "",
            record.RevokedAt,
            record.RevokedByUserId,
            null,
            record.Notes
        ));
    }

    public async Task<Result<IReadOnlyList<WardListItemResponse>>> GetMyWardsAsync(
        int guardianUserId,
        string? actorRoleCode,
        CancellationToken ct = default)
    {
        if (actorRoleCode != RoleCodes.Patient || guardianUserId <= 0)
            return Result<IReadOnlyList<WardListItemResponse>>.Failure(Error.Forbidden);

        var records = await repository.GetActiveByGuardianUserIdAsync(guardianUserId, ct);
        var wards = records
            .Where(r => r.WardPatient is not null && r.WardPatient.IsActive)
            .Select(r => new WardListItemResponse(
                r.WardPatientId,
                r.WardPatient!.PatientCode,
                r.WardPatient.FullName,
                r.WardPatient.DateOfBirth,
                r.WardPatient.Gender,
                r.Relationship,
                r.AssignedAt
            ))
            .ToList();

        return Result<IReadOnlyList<WardListItemResponse>>.Success(wards);
    }

    public async Task<Result<IReadOnlyList<PatientGuardianResponse>>> GetWardGuardianHistoryAsync(
        int wardPatientId,
        int actorUserId,
        string? actorRoleCode,
        CancellationToken ct = default)
    {
        if (actorRoleCode is not (RoleCodes.Admin or RoleCodes.Receptionist) || actorUserId <= 0)
            return Result<IReadOnlyList<PatientGuardianResponse>>.Failure(Error.Forbidden);

        if (wardPatientId <= 0)
            return Result<IReadOnlyList<PatientGuardianResponse>>.Failure(Error.Validation);

        var list = await repository.GetHistoryByWardPatientIdAsync(wardPatientId, ct);
        var result = list.Select(record => new PatientGuardianResponse(
            record.PatientGuardianId,
            record.GuardianPatientId,
            record.GuardianPatient?.PatientCode ?? "",
            record.GuardianPatient?.FullName ?? "",
            record.WardPatientId,
            record.WardPatient?.PatientCode ?? "",
            record.WardPatient?.FullName ?? "",
            record.Relationship,
            record.IsActive,
            record.AssignedAt,
            record.AssignedByUserId,
            record.AssignedByUser?.FullName ?? "",
            record.RevokedAt,
            record.RevokedByUserId,
            record.RevokedByUser?.FullName,
            record.Notes
        )).ToList();

        return Result<IReadOnlyList<PatientGuardianResponse>>.Success(result);
    }
}
