using System.Data;
using Dental.Application.Common;
using Dental.Application.Features.MOD_RX.DTOs;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
namespace Dental.Infrastructure.Repositories;
public sealed partial class PrescriptionRepository
{
    private Task<bool> HasCurrentHistoryVisitAsync(int patientId, int actorId, CancellationToken ct) =>
        db.Visits.AnyAsync(v => v.PatientId == patientId && v.DentistId == actorId &&
            (v.Status == VisitStatuses.InProgress || v.Status == VisitStatuses.Correcting), ct);

    // DL-299 is confined to prescription archive read/print. General clinical context remains unchanged.
    private async Task<bool> CanReadPrescriptionHistoryAsync(Visit visit, int actorId, string role, CancellationToken ct) =>
        await CanReadAsync(visit, actorId, role, ct) ||
        role == RoleCodes.Dentist &&
        await db.Users.AnyAsync(u => u.UserId == actorId && u.IsActive && u.Role.RoleCode == role, ct) &&
        await HasCurrentHistoryVisitAsync(visit.PatientId, actorId, ct);

    public async Task<Result<PagedResult<PatientPrescriptionSummary>>> PatientHistoryAsync(int patientId, int actorId,
        string role, PatientPrescriptionQuery query, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        if (role is not (RoleCodes.Admin or RoleCodes.Dentist or RoleCodes.Receptionist or RoleCodes.Assistant or RoleCodes.Patient) ||
            !await db.Users.AnyAsync(u => u.UserId == actorId && u.IsActive && u.Role.RoleCode == role, ct) ||
            !await db.Patients.AnyAsync(p => p.PatientId == patientId && (role != RoleCodes.Patient || p.UserId == actorId), ct))
            return Result<PagedResult<PatientPrescriptionSummary>>.Failure(Error.NotFound);
        var all = role != RoleCodes.Dentist || await HasCurrentHistoryVisitAsync(patientId, actorId, ct);
        var rows = db.Prescriptions.AsNoTracking().Where(p => p.Visit.PatientId == patientId && (all || p.Visit.DentistId == actorId));
        var count = await rows.CountAsync(ct);
        var items = await rows.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(p => new PatientPrescriptionSummary(p.Id, patientId, p.VisitId, p.Version, p.PrescriptionCode, p.CreatedAt,
                db.Users.Where(u => u.UserId == p.DoctorId).Select(u => u.FullName).FirstOrDefault() ?? "Chưa ghi nhận",
                p.Visit.Diagnosis ?? "", p.Items.Count, p.IsCancelled, p.DispensedAt,
                !p.IsCancelled && p.Visit.Status != VisitStatuses.Cancelled)).ToArrayAsync(ct);
        await tx.CommitAsync(ct);
        return Result<PagedResult<PatientPrescriptionSummary>>.Success(PagedResult<PatientPrescriptionSummary>.Create(items, count, query.Page, query.PageSize));
    }
    public async Task<Result<PatientPrescriptionDetail>> PatientHistoryDetailAsync(int patientId, int prescriptionId, int actorId,
        string role, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var rx = await db.Prescriptions.AsNoTracking().Include(p => p.Visit).Include(p => p.Items)
            .SingleOrDefaultAsync(p => p.Id == prescriptionId && p.Visit.PatientId == patientId, ct);
        if (rx is null || !await CanReadPrescriptionHistoryAsync(rx.Visit, actorId, role, ct))
            return Result<PatientPrescriptionDetail>.Failure(Error.NotFound);
        var doctorName = await db.Users.Where(u => u.UserId == rx.DoctorId).Select(u => u.FullName).SingleAsync(ct);
        var result = new PatientPrescriptionDetail(rx.Id, patientId, rx.VisitId, rx.Version, rx.PrescriptionCode, rx.CreatedAt,
            doctorName, rx.Visit.Diagnosis ?? "", rx.Diagnosis, rx.DoctorAdvice, rx.RevisitDate, rx.IsCancelled, rx.DispensedAt,
            !rx.IsCancelled && rx.Visit.Status != VisitStatuses.Cancelled,
            rx.Items.OrderBy(i => i.Id).Select(i => new PrescriptionPrintLine(i.MedicineName, i.Strength, i.Unit, i.Quantity, i.UsageInstructions, i.TreatmentDays)).ToArray());
        await tx.CommitAsync(ct); return Result<PatientPrescriptionDetail>.Success(result);
    }
}
