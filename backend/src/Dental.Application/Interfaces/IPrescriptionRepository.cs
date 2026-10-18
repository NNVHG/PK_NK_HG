using Dental.Application.Common;
using Dental.Application.Features.MOD_MST.DTOs;
using Dental.Application.Features.MOD_RX.DTOs;
namespace Dental.Application.Interfaces;
public interface IPrescriptionRepository
{
    Task<Result<PrescriptionCancellationResult>> CancelAsync(int visitId, int prescriptionId, int actorId, PrescriptionCancellationRequest request, string? ip, CancellationToken ct);
    Task<Result<PagedResult<PrescriptionResponse>>> HistoryAsync(int visitId, int actorId, string role, int page, int pageSize, CancellationToken ct);
    Task<Result<PrescriptionContext>> GetContextAsync(int visitId, int actorId, string role, CancellationToken ct);
    Task<Result<PagedResult<PrescriptionMedicine>>> GetMedicinesAsync(int visitId, int actorId, MedicineQuery query, CancellationToken ct);
    Task<Result<RepeatPrescription>> GetLatestAsync(int patientId, int targetVisitId, int actorId, CancellationToken ct);
    Task<Result<PrescriptionPreview>> PreviewAsync(int visitId, int actorId, PrescriptionWriteRequest request, CancellationToken ct);
    Task<Result<PrescriptionResponse>> SaveAsync(int visitId, int actorId, bool creating, PrescriptionWriteRequest request, string? ip, CancellationToken ct);
}
