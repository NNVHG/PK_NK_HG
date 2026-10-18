using System.Data;
using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.Services;
using Dental.Application.Features.MOD_MST.DTOs;
using Dental.Application.Features.MOD_MST.Services;
using Dental.Application.Features.MOD_RX.DTOs;
using Dental.Application.Features.MOD_RX.Services;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace Dental.Infrastructure.Repositories;
public sealed partial class PrescriptionRepository(DentalDbContext db, IInvoiceRepository invoices, VietnamClock clock) : IPrescriptionRepository
{
    private async Task<PrescriptionSnapshot?> LoadAsync(int visitId, bool tracking, CancellationToken ct, IEnumerable<int>? requested = null)
    {
        var visit = await (tracking ? db.Visits : db.Visits.AsNoTracking()).SingleOrDefaultAsync(v => v.VisitId == visitId, ct);
        if (visit is null) return null;
        var rx = await (tracking ? db.Prescriptions : db.Prescriptions.AsNoTracking()).Include(p => p.Items).SingleOrDefaultAsync(p => p.VisitId == visitId && !p.IsCancelled, ct);
        var generation = await db.Prescriptions.Where(p => p.VisitId == visitId && p.IsCancelled).MaxAsync(p => (int?)p.Id, ct) ?? 0;
        var invoice = await (tracking ? db.Invoices : db.Invoices.AsNoTracking()).Include(i => i.Items).SingleOrDefaultAsync(i => i.VisitId == visitId && i.Status != InvoiceStatus.Cancelled, ct);
        var history = await db.MedicalHistoryRecords.AsNoTracking().Include(h => h.Items).Where(h => h.PatientId == visit.PatientId)
            .OrderByDescending(h => h.CreatedAt).ThenByDescending(h => h.RecordId).FirstOrDefaultAsync(ct);
        var ids = (requested ?? []).Concat(rx?.Items.Select(i => i.MedicineId) ?? []).Distinct().ToArray();
        var medicines = await db.Medicines.AsNoTracking().Where(m => ids.Contains(m.Id)).ToListAsync(ct);
        var services = await db.VisitServices.AsNoTracking().Where(s => s.VisitId == visitId).ToListAsync(ct);
        var paid = await db.Invoices.AnyAsync(i => i.VisitId == visitId && (i.PaidAmount != 0 || i.Status == InvoiceStatus.Paid || i.Status == InvoiceStatus.PartiallyPaid || i.Status == InvoiceStatus.PartiallyRefunded || i.Status == InvoiceStatus.Refunded), ct) ||
            await db.PaymentTransactions.AnyAsync(p => p.Invoice.VisitId == visitId, ct);
        return new(visit, invoice, rx, paid, history?.RecordId, history?.Items.Where(i => i.Type == MedicalHistoryTypes.Allergy).ToArray() ?? [], medicines, services)
            { CancellationGeneration = generation };
    }
    private async Task<bool> IsDoctorAsync(PrescriptionSnapshot s, int actor, CancellationToken ct)
        => s.Visit.DentistId == actor && (s.Prescription is null || s.Prescription.DoctorId == actor) &&
            await db.Users.AnyAsync(u => u.UserId == actor && u.IsActive && u.Role.RoleCode == RoleCodes.Dentist, ct);
    public async Task<Result<PrescriptionContext>> GetContextAsync(int visitId, int actorId, string role, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var s = await LoadAsync(visitId, false, ct);
        if (s is null || !await CanReadAsync(s.Visit, actorId, role, ct))
            return Result<PrescriptionContext>.Failure(Error.NotFound);
        var ownedDoctor = role == RoleCodes.Dentist && await IsDoctorAsync(s, actorId, ct);
        var canEdit = ownedDoctor && PrescriptionRules.CanEdit(s);
        var response = new PrescriptionContext(visitId, s.Visit.PatientId, s.Visit.DentistId, s.Visit.Status, s.Visit.IsLocked,
            s.Visit.Diagnosis ?? "", canEdit, s.Invoice?.Id, s.Invoice?.InvoiceCode, s.Invoice?.Status.ToString(), role == RoleCodes.Dentist ? s.AllergyRecordId : null,
            role == RoleCodes.Dentist ? s.Allergies.Select(a => new PrescriptionAllergy(a.ItemId, a.Name, a.Detail)).ToArray() : [],
            s.Prescription is null ? null : Map(s.Prescription, s.Medicines, role == RoleCodes.Dentist))
            { CanCancel = ownedDoctor && PrescriptionRules.CanCancel(s) };
        await tx.CommitAsync(ct);
        return Result<PrescriptionContext>.Success(response);
    }
    public async Task<Result<PagedResult<PrescriptionMedicine>>> GetMedicinesAsync(int visitId, int actorId, MedicineQuery query, CancellationToken ct)
    {
        var s = await LoadAsync(visitId, false, ct);
        if (s is null || !await IsDoctorAsync(s, actorId, ct)) return Result<PagedResult<PrescriptionMedicine>>.Failure(Error.NotFound);
        if (!PrescriptionRules.CanEdit(s)) return Result<PagedResult<PrescriptionMedicine>>.Failure(Locked);
        var rows = db.Medicines.AsNoTracking().Where(m => m.IsActive && m.UnitPrice > 0);
        if (!string.IsNullOrWhiteSpace(query.Keyword)) { var term = DiagnosisSearch.Normalize(query.Keyword); rows = rows.Where(m => m.SearchText.Contains(term)); }
        var count = await rows.CountAsync(ct);
        var data = await rows.OrderBy(m => m.Code).ThenBy(m => m.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArrayAsync(ct);
        return Result<PagedResult<PrescriptionMedicine>>.Success(PagedResult<PrescriptionMedicine>.Create(data.Select(Medicine).ToArray(), count, query.Page, query.PageSize));
    }
    public async Task<Result<RepeatPrescription>> GetLatestAsync(int patientId, int targetVisitId, int actorId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var s = await LoadAsync(targetVisitId, false, ct);
        if (s is null || s.Visit.PatientId != patientId || !await IsDoctorAsync(s, actorId, ct)) return Result<RepeatPrescription>.Failure(Error.NotFound);
        if (!PrescriptionRules.CanEdit(s)) return Result<RepeatPrescription>.Failure(Locked);
        var source = await db.Prescriptions.AsNoTracking().Include(p => p.Items).Where(p => p.Visit.PatientId == patientId &&
            p.VisitId != targetVisitId && p.Visit.Status == VisitStatuses.Completed && !p.IsCancelled)
            .OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id).FirstOrDefaultAsync(ct);
        if (source is null) return Result<RepeatPrescription>.Failure(Error.NotFound);
        var sourceIds = source.Items.Select(i => i.MedicineId).ToArray();
        var medicines = await db.Medicines.AsNoTracking().Where(m => sourceIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, ct);
        var result = new RepeatPrescription(source.Id, source.PrescriptionCode, source.Items.OrderBy(i => i.Id)
            .Select(i => new RepeatPrescriptionLine(i.MedicineId, i.Quantity, i.UsageInstructions, i.TreatmentDays,
                medicines.TryGetValue(i.MedicineId, out var m) && m.IsActive && m.UnitPrice > 0 ? Medicine(m) : null)).ToArray());
        await tx.CommitAsync(ct);
        return Result<RepeatPrescription>.Success(result);
    }
    public async Task<Result<PrescriptionPreview>> PreviewAsync(int visitId, int actorId, PrescriptionWriteRequest request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var s = await LoadAsync(visitId, false, ct, request.Items.Select(i => i.MedicineId));
        if (s is null || !await IsDoctorAsync(s, actorId, ct)) return Result<PrescriptionPreview>.Failure(Error.NotFound);
        var plan = PrescriptionRules.Build(s, request, false);
        await tx.CommitAsync(ct);
        return plan.IsFailure ? Result<PrescriptionPreview>.Failure(plan.Error) : Result<PrescriptionPreview>.Success(plan.Value!.Preview);
    }
    public async Task<Result<PrescriptionResponse>> SaveAsync(int visitId, int actorId, bool creating, PrescriptionWriteRequest request, string? ip, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var committed = false;
        var today = clock.Today;
        try
        {
            var s = await LoadAsync(visitId, true, ct, request.Items.Select(i => i.MedicineId));
            if (s is null || !await IsDoctorAsync(s, actorId, ct)) return Fail(Error.NotFound);
            if (creating != (s.Prescription is null)) return Fail(Conflict);
            var plan = PrescriptionRules.Build(s, request, true);
            if (plan.IsFailure) return Fail(plan.Error);
            var rx = s.Prescription ?? new Prescription { VisitId = visitId, DoctorId = actorId, CreatedByUserId = actorId, CreatedAt = clock.UtcNow };
            if (creating)
            {
                var counter = await db.PrescriptionNumberCounters.FindAsync([today], ct);
                if (counter is null) { counter = new PrescriptionNumberCounter { PrescriptionDate = today }; db.PrescriptionNumberCounters.Add(counter); }
                if (counter.LastNumber >= 9999) return Fail(new Error("RX_009", "Đã hết số đơn thuốc trong ngày."));
                counter.LastNumber++; rx.PrescriptionCode = $"RX-{today:yyyyMMdd}-{counter.LastNumber:D4}";
                db.Prescriptions.Add(rx);
            }
            else { rx.Version++; rx.UpdatedAt = clock.UtcNow; }
            rx.Diagnosis = request.Diagnosis.Trim(); rx.DoctorAdvice = string.IsNullOrWhiteSpace(request.DoctorAdvice) ? null : request.DoctorAdvice.Trim();
            rx.RevisitDate = request.RevisitDate; rx.AllergyRecordId = s.AllergyRecordId;
            var wanted = plan.Value!.Items.ToDictionary(i => i.MedicineId);
            foreach (var old in rx.Items.ToArray())
            {
                if (!wanted.TryGetValue(old.MedicineId, out var next)) { rx.Items.Remove(old); db.PrescriptionItems.Remove(old); continue; }
                old.Quantity = next.Quantity; old.UsageInstructions = next.UsageInstructions; old.TreatmentDays = next.TreatmentDays; old.AllergyOverrideReason = next.AllergyOverrideReason;
                wanted.Remove(old.MedicineId);
            }
            foreach (var line in wanted.Values) rx.Items.Add(line);
            await db.SaveChangesAsync(ct); // Item IDs exist before immutable invoice snapshots are made.
            Invoice invoice;
            if (s.Visit.Status == VisitStatuses.Completed)
            {
                invoice = s.Invoice!;
                var oldItems = invoice.Items.ToArray();
                var synchronized = InvoiceDraftSynchronizer.ApplyPendingPrescription(invoice, s.Services, rx.Items);
                if (synchronized.IsFailure) return Fail(synchronized.Error);
                if (synchronized.Value) db.InvoiceItems.RemoveRange(oldItems);
                rx.Invoice = invoice;
                // DL-135: zero payable invoices are settled without a fabricated zero payment.
                if (invoice.FinalAmount == 0) invoice.Status = InvoiceStatus.Paid;
                await db.SaveChangesAsync(ct);
            }
            else
            {
                var synced = await invoices.SynchronizeDraftAsync(visitId, actorId, today, ct);
                if (synced.IsFailure) return Fail(synced.Error);
                invoice = synced.Value!.Invoice; rx.Invoice = invoice;
            }
            db.AuditLogs.Add(new AuditLog { UserId = actorId, Action = creating ? "MOD_RX_PRESCRIPTION_CREATED" : "MOD_RX_PRESCRIPTION_UPDATED",
                EntityType = "Prescription", EntityId = rx.Id, IpAddress = ip, CreatedAt = clock.UtcNow,
                Detail = JsonSerializer.Serialize(new { rx.VisitId, rx.Version, count = rx.Items.Count, invoiceId = invoice.Id, copiedTemplate = false }) });
            if (plan.Value.Preview.AllergyWarnings.Count > 0 || plan.Value.Preview.DuplicateIngredients.Count > 0)
                db.AuditLogs.Add(new AuditLog { UserId = actorId, Action = "MOD_RX_SAFETY_OVERRIDE_CONFIRMED", EntityType = "Prescription", EntityId = rx.Id,
                    IpAddress = ip, CreatedAt = clock.UtcNow, Detail = JsonSerializer.Serialize(new {
                        rx.Version, allergyMedicineIds = plan.Value.Preview.AllergyWarnings.Select(w => w.MedicineId),
                        duplicateIngredientCount = plan.Value.Preview.DuplicateIngredients.Count, rx.AllergyRecordId }) });
            db.AuditLogs.Add(new AuditLog { UserId = actorId, Action = "MOD_BIL_PRESCRIPTION_SYNCHRONIZED", EntityType = "Invoice", EntityId = invoice.Id,
                IpAddress = ip, CreatedAt = clock.UtcNow, Detail = JsonSerializer.Serialize(new { prescriptionId = rx.Id, rx.Version, invoice.TotalAmount, invoice.DiscountAmount, invoice.Status }) });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); committed = true;
            return Result<PrescriptionResponse>.Success(Map(rx, s.Medicines));
        }
        catch (Exception error) when (IsConflict(error))
        {
            try { await tx.RollbackAsync(CancellationToken.None); } catch (InvalidOperationException) { }
            return Fail(Conflict);
        }
        finally { if (!committed) db.ChangeTracker.Clear(); }
    }
    private static PrescriptionResponse Map(Prescription p, IReadOnlyList<Medicine> medicines, bool includeOverrideReason = true)
    {
        var lookup = medicines.ToDictionary(m => m.Id);
        return new(p.Id, p.VisitId, p.DoctorId, p.InvoiceId, p.PrescriptionCode, p.Diagnosis, p.DoctorAdvice, p.RevisitDate, p.IsCancelled, p.DispensedAt,
            p.CreatedAt, p.UpdatedAt, p.Version, p.Items.OrderBy(i => i.Id).Select(i => new PrescriptionLineResponse(i.Id, i.MedicineId,
                lookup.GetValueOrDefault(i.MedicineId)?.Version ?? 0, i.MedicineCode, i.MedicineName, i.ActiveIngredient, i.Strength, i.Unit, i.Quantity,
                i.UnitPrice, i.TotalPrice, i.UsageInstructions, i.TreatmentDays, includeOverrideReason ? i.AllergyOverrideReason : null,
                lookup.TryGetValue(i.MedicineId, out var m) && m.IsActive && m.UnitPrice > 0)).ToArray());
    }
    private static PrescriptionMedicine Medicine(Medicine m) => new(m.Id, m.Version, m.Code, m.Name, m.ActiveIngredient, m.Strength, m.Unit, m.UnitPrice, m.DefaultUsageInstruction);
    private static readonly Error Locked = new("RX_002", "Đơn đã khóa hoặc ca khám chưa cho phép kê đơn.");
    private static readonly Error Conflict = new("RX_003", "Có thay đổi đồng thời; tải lại đơn và kiểm tra lại trước khi lưu.");
    private static Result<PrescriptionResponse> Fail(Error error) => Result<PrescriptionResponse>.Failure(error);
    private static bool IsConflict(Exception error)
    {
        for (Exception? e = error; e is not null; e = e.InnerException)
            if (e is DbUpdateConcurrencyException || e is PostgresException { SqlState: "40001" or "40P01" or "23505" }) return true;
        return false;
    }
}
