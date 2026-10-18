using System.Data;
using System.Text.Json;
using Dental.Application.Common;
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
public sealed class PrescriptionHandoverRepository(DentalDbContext db, VietnamClock clock) : IPrescriptionHandoverRepository
{
    private static readonly Error Locked = new("RX_HANDOVER_LOCKED", "Không thể bàn giao hoặc hoàn tác theo trạng thái và thời hạn hiện tại. Tải lại để đối chiếu.");
    private static readonly Error Conflict = new("RX_HANDOVER_CONFLICT", "Đơn đã thay đổi. Tải lại để đối chiếu trước khi thao tác.");
    private async Task<Prescription?> LoadAsync(int id, int actorId, string role, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.UserId == actorId && u.IsActive && u.Role.RoleCode == role, ct)) return null;
        var rx = await db.Prescriptions.Include(p => p.Visit).Include(p => p.Invoice).SingleOrDefaultAsync(p => p.Id == id, ct);
        if (rx is null || role == RoleCodes.Dentist && rx.Visit.DentistId != actorId ||
            role == RoleCodes.Patient && !await db.Patients.AnyAsync(p => p.PatientId == rx.Visit.PatientId && p.UserId == actorId, ct)) return null;
        return rx;
    }
    private static bool Active(Prescription p) => !p.IsCancelled && p.Visit.Status != VisitStatuses.Cancelled;
    private async Task<bool> SettledAsync(Prescription p, CancellationToken ct)
    {
        if (p.Invoice is not {} invoice || invoice.VisitId != p.VisitId || invoice.Status is not (InvoiceStatus.Paid or InvoiceStatus.PartiallyRefunded) ||
            invoice.PaidAmount < 0 || invoice.FinalAmount < 0 || invoice.PaidAmount != invoice.FinalAmount) return false;
        // DL-301: a status flag cannot replace refund provenance or the unrefunded medicine-line check.
        if (invoice.Status == InvoiceStatus.PartiallyRefunded && !await db.InvoiceRefunds.AnyAsync(r => r.InvoiceId == invoice.Id, ct)) return false;
        var rxItems = db.PrescriptionItems.Where(x => x.PrescriptionId == p.Id).Select(x => x.Id);
        return !await db.InvoiceRefundItems.AnyAsync(r => r.InvoiceRefund.InvoiceId == invoice.Id && r.InvoiceItem.ItemType == "Medicine" &&
            r.InvoiceItem.SourcePrescriptionItemId.HasValue && rxItems.Contains(r.InvoiceItem.SourcePrescriptionItemId.Value), ct);
    }
    private bool InWindow(Prescription p) => p.DispensedAt is DateTime at && at <= clock.UtcNow && clock.UtcNow - at <= TimeSpan.FromMinutes(30);
    private bool MayUndo(Prescription p, int actorId, string role) => Active(p) && PrescriptionHandoverService.CanWrite(role) && InWindow(p) && (role == RoleCodes.Admin || p.DispensedBy == actorId);
    private async Task<PrescriptionHandoverStatus> StatusAsync(Prescription rx, int actorId, string role, CancellationToken ct)
    {
        var patient = await db.Patients.AsNoTracking().SingleAsync(p => p.PatientId == rx.Visit.PatientId, ct);
        var required = patient.DateOfBirth.AddYears(15) > clock.Today;
        var expires = rx.CreatedAt.AddDays(5); // Q-267: elapsed five days from the saved prescribing timestamp.
        var settled = await SettledAsync(rx, ct);
        var ready = Active(rx) && rx.DispensedAt is null && settled && rx.Version < int.MaxValue;
        var authorized = PrescriptionHandoverService.CanWrite(role);
        var name = rx.DispensedBy is int userId ? await db.Users.Where(u => u.UserId == userId).Select(u => u.FullName).SingleOrDefaultAsync(ct) : null;
        return new(rx.Id, rx.VisitId, rx.Version, rx.PrescriptionCode, patient.FullName, required, expires,
            rx.DispensedAt, name, rx.ReceivedByProxy, rx.ReceiverName, rx.ReceiverPhone,
            ready && authorized && clock.UtcNow <= expires && rx.CreatedAt <= clock.UtcNow,
            MayUndo(rx, actorId, role) && rx.Version < int.MaxValue,
            ready && role == RoleCodes.Admin && clock.UtcNow > expires,
            !Active(rx) ? "Đơn hoặc lần khám đã hủy." : rx.DispensedAt is not null ? "Đơn đã giao thuốc." : !settled ? "Chỉ giao thuốc khi phần còn lại đã thanh toán đủ sau chiết khấu và toàn bộ thuốc cần giao chưa bị hoàn." : clock.UtcNow > expires ? "Đơn quá hạn cấp phát 5 ngày; chỉ Admin được xác nhận ngoại lệ có lý do." : !authorized ? "Bạn chỉ có quyền xem thông tin bàn giao." : null);
    }
    public async Task<Result<PrescriptionHandoverStatus>> ReadAsync(int id, int actorId, string role, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var rx = await LoadAsync(id, actorId, role, ct);
        if (rx is null) return Result<PrescriptionHandoverStatus>.Failure(Error.NotFound);
        var status = await StatusAsync(rx, actorId, role, ct);
        await tx.CommitAsync(ct); return Result<PrescriptionHandoverStatus>.Success(status);
    }
    public Task<Result<PrescriptionHandoverStatus>> DispenseAsync(int id, int actorId, string role, PrescriptionDispenseRequest request, string? ip, CancellationToken ct) =>
        ChangeAsync(id, actorId, role, request, null, ip, ct);
    public Task<Result<PrescriptionHandoverStatus>> UndoAsync(int id, int actorId, string role, PrescriptionUndoDispenseRequest request, string? ip, CancellationToken ct) =>
        ChangeAsync(id, actorId, role, null, request, ip, ct);
    private async Task<Result<PrescriptionHandoverStatus>> ChangeAsync(int id, int actorId, string role, PrescriptionDispenseRequest? dispense, PrescriptionUndoDispenseRequest? undo, string? ip, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var committed = false;
        try
        {
            if (!PrescriptionHandoverService.CanWrite(role)) return Result<PrescriptionHandoverStatus>.Failure(Error.Forbidden);
            var rx = await LoadAsync(id, actorId, role, ct);
            if (rx is null) return Result<PrescriptionHandoverStatus>.Failure(Error.NotFound);
            var expected = dispense?.ExpectedVersion ?? undo!.ExpectedVersion;
            if (rx.Version != expected || rx.Version == int.MaxValue) return Result<PrescriptionHandoverStatus>.Failure(Conflict);
            var status = await StatusAsync(rx, actorId, role, ct);
            var action = undo is null ? "PRESCRIPTION_DISPENSED" : "PRESCRIPTION_DISPENSE_UNDONE";
            if (undo is not null && !status.CanUndo || dispense is not null && !(status.CanDispense || status.CanOverrideExpired && dispense.OverrideExpired))
                return Result<PrescriptionHandoverStatus>.Failure(Locked);
            if (dispense is not null && status.ReceiverRequired && (!dispense.ReceivedByProxy || string.IsNullOrWhiteSpace(dispense.ReceiverName) || string.IsNullOrWhiteSpace(dispense.ReceiverPhone)))
                return Result<PrescriptionHandoverStatus>.Failure(new("RX_RECEIVER_REQUIRED", "Bệnh nhân dưới 15 tuổi cần họ tên và số điện thoại người nhận thay."));
            var record = new PrescriptionDispenseEvent {
                PrescriptionId = rx.Id, PrescriptionVersion = rx.Version + 1, Action = action, PerformedAt = clock.UtcNow, PerformedBy = actorId,
                ReceivedByProxy = dispense?.ReceivedByProxy ?? rx.ReceivedByProxy,
                ReceiverName = dispense is null ? rx.ReceiverName : dispense.ReceivedByProxy ? dispense.ReceiverName?.Trim() : null,
                ReceiverPhone = dispense is null ? rx.ReceiverPhone : dispense.ReceivedByProxy ? dispense.ReceiverPhone?.Trim() : null,
                Reason = undo?.Reason.Trim() ?? (dispense?.OverrideExpired == true ? dispense.OverrideReason?.Trim() : null),
                ExpiryOverridden = dispense?.OverrideExpired == true && status.CanOverrideExpired
            };
            if (dispense is not null)
            {
                rx.DispensedAt = clock.UtcNow; rx.DispensedBy = actorId; rx.ReceivedByProxy = record.ReceivedByProxy;
                rx.ReceiverName = string.IsNullOrWhiteSpace(record.ReceiverName) ? null : record.ReceiverName;
                rx.ReceiverPhone = string.IsNullOrWhiteSpace(record.ReceiverPhone) ? null : record.ReceiverPhone;
                rx.DispenseNote = record.ReceivedByProxy ? $"Người nhận thay: {rx.ReceiverName ?? "Chưa ghi nhận"}; SĐT: {rx.ReceiverPhone ?? "Chưa ghi nhận"}" : null;
            }
            else { rx.DispensedAt = null; rx.DispensedBy = null; rx.ReceivedByProxy = false; rx.ReceiverName = null; rx.ReceiverPhone = null; rx.DispenseNote = null; }
            rx.Version++; rx.UpdatedAt = clock.UtcNow;
            db.PrescriptionDispenseEvents.Add(record);
            // Reason/receiver details remain in protected medical history, not in generic audit logs.
            db.AuditLogs.Add(new AuditLog { UserId = actorId, EntityType = "Prescription", EntityId = rx.Id, Action = action, CreatedAt = clock.UtcNow, IpAddress = ip,
                Detail = JsonSerializer.Serialize(new { rx.Version, previousVersion = expected, record.ExpiryOverridden, trigger = "handover" }) });
            await db.SavePrescriptionHandoverAsync(ct);
            var result = await StatusAsync(rx, actorId, role, ct);
            await tx.CommitAsync(ct); committed = true;
            return Result<PrescriptionHandoverStatus>.Success(result);
        }
        catch (Exception error) when (IsConflict(error)) { return Result<PrescriptionHandoverStatus>.Failure(Conflict); }
        finally { if (!committed) db.ChangeTracker.Clear(); }
    }
    private static bool IsConflict(Exception error)
    {
        for (Exception? e = error; e is not null; e = e.InnerException)
            if (e is DbUpdateConcurrencyException || e is PostgresException { SqlState: "40001" or "40P01" or "23505" }) return true;
        return false;
    }
}
