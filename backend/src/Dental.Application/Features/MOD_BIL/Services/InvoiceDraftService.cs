using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;

namespace Dental.Application.Features.MOD_BIL.Services;

public sealed class InvoiceDraftService(IInvoiceRepository invoices, IVisitRepository visits,
    IPatientRepository patients, IAuditLogger audit, VietnamClock clock) : IInvoiceDraftGenerator
{
    public Task<Result> CancelDraftAsync(int invoiceId, int userId, string? role,
        CancelInvoiceDraftRequest request, string? ip, CancellationToken ct = default)
    {
        if (role != RoleCodes.Admin) return Task.FromResult(Result.Failure(Error.Forbidden));
        if (invoiceId <= 0 || userId <= 0 || request is null ||
            !new CancelInvoiceDraftRequestValidator().Validate(request).IsValid)
            return Task.FromResult(Result.Failure(Error.Validation));
        return invoices.CancelDraftAsync(invoiceId, userId, request.Reason.Trim(), ip, ct);
    }

    public Task<Result> StageVisitCancellationAsync(Visit visit, int userId, string role, string? ip, CancellationToken ct = default)
    {
        if (role is not (RoleCodes.Admin or RoleCodes.Dentist) || role == RoleCodes.Dentist && visit.DentistId != userId)
            return Task.FromResult(Result.Failure(Error.Forbidden));
        if (visit.Status != VisitStatuses.InProgress || visit.IsLocked)
            return Task.FromResult(Result.Failure(new Error("BIL_012", "Ca khám đã thay đổi; tải lại trước khi hủy.")));
        return invoices.StageVisitCancellationAsync(visit.VisitId, userId, role, ip, ct);
    }

    public async Task<Result> GenerateAsync(Visit completedVisit, int userId, string role, string? ip, CancellationToken ct = default)
    {
        if (role is not (RoleCodes.Admin or RoleCodes.Dentist) ||
            role == RoleCodes.Dentist && completedVisit.DentistId != userId)
            return Result.Failure(Error.Forbidden);
        if (completedVisit.Status != VisitStatuses.Completed || !completedVisit.IsLocked)
            return Result.Failure(new Error("BIL_001", "Lần khám phải được hoàn tất và khóa cùng hóa đơn nháp."));
        var result = await invoices.SaveCompletedVisitDraftAsync(completedVisit, userId, clock.Today, ct);
        if (result.IsFailure) return Result.Failure(result.Error);
        await audit.LogAsync("MOD_BIL_INVOICE_PENDING_PAYMENT", "Invoice", result.Value!.Id, userId,
            "{\"action\":\"visit_completed\"}", ip, ct);
        return Result.Success();
    }

    public async Task<Result<InvoiceResponse>> GetAsync(int visitId, int userId, string? role, CancellationToken ct = default)
    {
        if (role is not (RoleCodes.Admin or RoleCodes.Dentist or RoleCodes.Receptionist or RoleCodes.Patient))
            return Result<InvoiceResponse>.Failure(Error.Forbidden);
        var visit = await visits.GetByIdAsync(visitId, ct);
        if (visit is null) return Result<InvoiceResponse>.Failure(Error.NotFound);
        if (role == RoleCodes.Patient && (await patients.GetByIdAsync(visit.PatientId, ct))?.UserId != userId)
            return Result<InvoiceResponse>.Failure(Error.NotFound);
        Invoice? invoice;
        if (VisitStatuses.IsEditable(visit.Status) && !visit.IsLocked)
        {
            var synced = await invoices.SynchronizeDraftAsync(visitId, userId, clock.Today, ct);
            if (synced.IsFailure) return Result<InvoiceResponse>.Failure(synced.Error);
            invoice = synced.Value!.Invoice;
            if (synced.Value.Changed)
                await audit.LogAsync("MOD_BIL_DRAFT_SYNCHRONIZED", "Invoice", invoice.Id, userId,
                    "{\"action\":\"services_synchronized\"}", null, ct);
        }
        else invoice = await invoices.GetForVisitAsync(visitId, ct);
        if (invoice is null) return Result<InvoiceResponse>.Failure(Error.NotFound);
        return Result<InvoiceResponse>.Success(new(invoice.Id, invoice.VisitId, invoice.InvoiceCode,
            (int)invoice.Status, invoice.TotalAmount, invoice.PaidAmount, invoice.FinalAmount - invoice.PaidAmount,
            invoice.CreatedAt, invoice.Items.OrderBy(x => x.Id).Select(x => new InvoiceItemResponse(x.ItemType,
                x.Code, x.Name, x.ToothNumber, x.Surface, x.Quantity, x.UnitPrice, x.TotalAmount) { Id = x.Id, RefundedQuantity = invoice.Refunds.SelectMany(r => r.Items).Where(r => r.InvoiceItemId == x.Id).Sum(r => r.Quantity) }).ToArray())
            { DiscountAmount = invoice.DiscountAmount, DiscountType = invoice.DiscountType, DiscountValue = invoice.DiscountValue,
                DiscountPresetId = invoice.DiscountPresetId, DiscountReason = invoice.DiscountReason });
    }

    public async Task<Result> CompleteCorrectionAsync(int visitId, int userId, string? role, string? ip, CancellationToken ct = default)
    {
        if (role is not (RoleCodes.Admin or RoleCodes.Dentist)) return Result.Failure(Error.Forbidden);
        if (visitId <= 0 || userId <= 0) return Result.Failure(Error.Validation);
        var result = await invoices.CompleteCorrectionAsync(visitId, userId, role, clock.Today, ip, ct);
        return result.IsFailure ? Result.Failure(result.Error) : Result.Success();
    }
}
