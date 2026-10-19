using System.Data;
using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_RX.DTOs;
using Dental.Application.Features.MOD_RX.Services;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
namespace Dental.Infrastructure.Repositories;
public sealed partial class PrescriptionRepository
{
    private async Task<bool> CanReadAsync(Visit visit, int actorId, string role, CancellationToken ct) =>
        role is RoleCodes.Admin or RoleCodes.Dentist or RoleCodes.Receptionist or RoleCodes.Assistant or RoleCodes.Patient &&
        await db.Users.AnyAsync(u => u.UserId == actorId && u.IsActive && u.Role.RoleCode == role, ct) &&
        (role != RoleCodes.Dentist || visit.DentistId == actorId) &&
        (role != RoleCodes.Patient || await db.Patients.AnyAsync(p => p.PatientId == visit.PatientId && p.UserId == actorId, ct));
    public async Task<Result<PagedResult<PrescriptionResponse>>> HistoryAsync(int visitId, int actorId, string role, int page, int pageSize, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var visit = await db.Visits.AsNoTracking().SingleOrDefaultAsync(v => v.VisitId == visitId, ct);
        if (visit is null || !await CanReadAsync(visit, actorId, role, ct)) return Result<PagedResult<PrescriptionResponse>>.Failure(Error.NotFound);
        var rows = db.Prescriptions.AsNoTracking().Where(p => p.VisitId == visitId);
        var count = await rows.CountAsync(ct);
        var orders = await rows.OrderByDescending(p => p.Id).Skip((page - 1) * pageSize).Take(pageSize).Include(p => p.Items).ToArrayAsync(ct);
        var ids = orders.SelectMany(p => p.Items).Select(i => i.MedicineId).Distinct().ToArray();
        var medicines = await db.Medicines.AsNoTracking().Where(m => ids.Contains(m.Id)).ToArrayAsync(ct);
        var result = PagedResult<PrescriptionResponse>.Create(orders.Select(p => Map(p, medicines, role == RoleCodes.Dentist)).ToArray(), count, page, pageSize);
        await tx.CommitAsync(ct);
        return Result<PagedResult<PrescriptionResponse>>.Success(result);
    }
    public async Task<Result<PrescriptionCancellationResult>> CancelAsync(int visitId, int prescriptionId, int actorId,
        PrescriptionCancellationRequest request, string? ip, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); var committed = false;
        try
        {
            var state = await LoadAsync(visitId, true, ct);
            if (state is null || !await IsResponsibleDoctorAsync(state, actorId, ct)) return Result<PrescriptionCancellationResult>.Failure(Error.NotFound);
            if (state.Prescription?.Id != prescriptionId || state.Prescription.Version != request.ExpectedVersion)
                return Result<PrescriptionCancellationResult>.Failure(Conflict);
            if (!PrescriptionRules.CanCancel(state)) return Result<PrescriptionCancellationResult>.Failure(Locked);
            var rx = state.Prescription;
            var replacingColleagueOrder = rx.DoctorId != actorId;
            if (request.Reason?.Length > 500 || replacingColleagueOrder && string.IsNullOrWhiteSpace(request.Reason))
                return Result<PrescriptionCancellationResult>.Failure(Error.Validation);
            var reason = request.Reason?.Trim();
            rx.IsCancelled = true; rx.Version++; rx.UpdatedAt = clock.UtcNow;
            // Persist the flag inside this transaction so the invoice query excludes this prescription.
            await db.SaveChangesAsync(ct);
            var sync = await invoices.SynchronizeDraftAsync(visitId, actorId, clock.Today, ct);
            if (sync.IsFailure) return Result<PrescriptionCancellationResult>.Failure(sync.Error);
            var invoice = sync.Value!.Invoice;
            db.AuditLogs.Add(new AuditLog { UserId = actorId, Action = "MOD_RX_PRESCRIPTION_CANCELLED", EntityType = "Prescription",
                EntityId = rx.Id, IpAddress = ip, CreatedAt = clock.UtcNow,
                Detail = JsonSerializer.Serialize(new { rx.VisitId, rx.Version, originalDoctorId = rx.DoctorId,
                    cancellingDoctorId = actorId, replacingColleagueOrder, reason, invoiceId = invoice.Id, trigger = "prescription_cancelled" }) });
            db.AuditLogs.Add(new AuditLog { UserId = actorId, Action = "MOD_BIL_PRESCRIPTION_SYNCHRONIZED", EntityType = "Invoice",
                EntityId = invoice.Id, IpAddress = ip, CreatedAt = clock.UtcNow,
                Detail = JsonSerializer.Serialize(new { prescriptionId = rx.Id, rx.Version, invoice.TotalAmount, invoice.DiscountAmount, trigger = "prescription_cancelled" }) });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); committed = true;
            return Result<PrescriptionCancellationResult>.Success(new(rx.Id, visitId, rx.Version, invoice.Id, invoice.TotalAmount));
        }
        catch (Exception error) when (IsConflict(error)) { return Result<PrescriptionCancellationResult>.Failure(Conflict); }
        finally { if (!committed) db.ChangeTracker.Clear(); }
    }
}
