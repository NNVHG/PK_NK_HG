using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;

namespace Dental.Application.Features.MOD_BIL.Services;

public sealed class InvoiceDraftService(IInvoiceRepository invoices, IVisitRepository visits,
    IPatientRepository patients, IAuditLogger audit, VietnamClock clock) : IInvoiceDraftGenerator
{
    public async Task<Result> GenerateAsync(Visit completedVisit, int userId, string role, string? ip, CancellationToken ct = default)
    {
        if (role is not (RoleCodes.Admin or RoleCodes.Dentist) ||
            role == RoleCodes.Dentist && completedVisit.DentistId != userId)
            return Result.Failure(Error.Forbidden);
        if (completedVisit.Status != VisitStatuses.Completed || !completedVisit.IsLocked)
            return Result.Failure(new Error("BIL_001", "Lần khám phải được hoàn tất và khóa cùng hóa đơn nháp."));
        var result = await invoices.SaveCompletedVisitDraftAsync(completedVisit, userId, clock.Today, ct);
        if (result.IsFailure) return Result.Failure(result.Error);
        await audit.LogAsync("MOD_BIL_DRAFT_CREATED", "Invoice", result.Value!.Id, userId,
            "{\"action\":\"draft_created\"}", ip, ct);
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
        var invoice = await invoices.GetForVisitAsync(visitId, ct);
        if (invoice is null) return Result<InvoiceResponse>.Failure(Error.NotFound);
        return Result<InvoiceResponse>.Success(new(invoice.Id, invoice.VisitId, invoice.InvoiceCode,
            (int)invoice.Status, invoice.TotalAmount, invoice.PaidAmount, invoice.TotalAmount - invoice.PaidAmount,
            invoice.CreatedAt, invoice.Items.OrderBy(x => x.Id).Select(x => new InvoiceItemResponse(x.ItemType,
                x.Code, x.Name, x.ToothNumber, x.Surface, x.Quantity, x.UnitPrice, x.TotalAmount)).ToArray()));
    }
}
