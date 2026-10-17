using Dental.Application.Common;
using Dental.Application.Features.MOD_MST.DTOs;
using Dental.Application.Features.MOD_MST.Validators;
using Dental.Application.Features.MOD_RX.DTOs;
using Dental.Application.Features.MOD_RX.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
namespace Dental.Application.Features.MOD_RX.Services;
public sealed partial class PrescriptionService(IPrescriptionRepository repository)
{
    public async Task<Result<PrescriptionLookupLink>> LookupLinkAsync(int visitId, int actorId, string? role, CancellationToken ct = default)
    {
        var result = await GetAsync(visitId, actorId, role, ct);
        if (result.IsFailure) return Result<PrescriptionLookupLink>.Failure(result.Error);
        var rx = result.Value!.Prescription;
        if (rx?.IsCancelled == true || result.Value.VisitStatus == Dental.Domain.Constants.VisitStatuses.Cancelled)
            return Result<PrescriptionLookupLink>.Failure(new Error("RX_002", "Đơn đã hủy, không phát hành mã tra cứu."));
        if (rx is null) return Result<PrescriptionLookupLink>.Failure(Error.NotFound);
        if (!Dental.Application.Features.MOD_RX.PublicPrescriptionService.ValidCode(rx.PrescriptionCode))
            return Result<PrescriptionLookupLink>.Failure(Error.Validation);
        return Result<PrescriptionLookupLink>.Success(new(rx.PrescriptionCode, rx.Version,
            "/portal/prescriptions/" + Uri.EscapeDataString(rx.PrescriptionCode)));
    }
    public Task<Result<PrescriptionContext>> GetAsync(int visitId, int actorId, string? role, CancellationToken ct = default)
    {
        if (actorId <= 0 || role is not (RoleCodes.Admin or RoleCodes.Dentist or RoleCodes.Receptionist or RoleCodes.Assistant or RoleCodes.Patient))
            return Task.FromResult(Result<PrescriptionContext>.Failure(Error.Forbidden));
        return visitId <= 0 ? Task.FromResult(Result<PrescriptionContext>.Failure(Error.Validation)) : repository.GetContextAsync(visitId, actorId, role, ct);
    }
    public Task<Result<PagedResult<PrescriptionMedicine>>> MedicinesAsync(int visitId, int actorId, string? role, MedicineQuery query, CancellationToken ct = default)
    {
        if (role != RoleCodes.Dentist || actorId <= 0) return Task.FromResult(Result<PagedResult<PrescriptionMedicine>>.Failure(Error.Forbidden));
        if (visitId <= 0 || query.IncludeInactive || !new MedicineQueryValidator().Validate(query).IsValid) return Task.FromResult(Result<PagedResult<PrescriptionMedicine>>.Failure(Error.Validation));
        return repository.GetMedicinesAsync(visitId, actorId, query, ct);
    }
    public Task<Result<RepeatPrescription>> LatestAsync(int patientId, int visitId, int actorId, string? role, CancellationToken ct = default)
    {
        if (role != RoleCodes.Dentist || actorId <= 0) return Task.FromResult(Result<RepeatPrescription>.Failure(Error.Forbidden));
        return patientId <= 0 || visitId <= 0 ? Task.FromResult(Result<RepeatPrescription>.Failure(Error.Validation)) : repository.GetLatestAsync(patientId, visitId, actorId, ct);
    }
    public async Task<Result<PrescriptionPreview>> PreviewAsync(int visitId, int actorId, string? role, PrescriptionWriteRequest request, CancellationToken ct = default)
    {
        if (role != RoleCodes.Dentist || actorId <= 0) return Result<PrescriptionPreview>.Failure(Error.Forbidden);
        if (visitId <= 0 || !(await new PrescriptionWriteValidator().ValidateAsync(request, ct)).IsValid) return Result<PrescriptionPreview>.Failure(Error.Validation);
        return await repository.PreviewAsync(visitId, actorId, request, ct);
    }
    public async Task<Result<PrescriptionResponse>> SaveAsync(int visitId, int actorId, string? role, bool creating, PrescriptionWriteRequest request, string? ip = null, CancellationToken ct = default)
    {
        if (role != RoleCodes.Dentist || actorId <= 0) return Result<PrescriptionResponse>.Failure(Error.Forbidden);
        if (visitId <= 0 || !(await new PrescriptionWriteValidator().ValidateAsync(request, ct)).IsValid || request.PreviewToken is null || creating != (request.ExpectedVersion == 0)) return Result<PrescriptionResponse>.Failure(Error.Validation);
        return await repository.SaveAsync(visitId, actorId, creating, request, ip, ct);
    }
}
