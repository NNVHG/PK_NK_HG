using System.Data;
using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_AUTH;
using Dental.Application.Features.MOD_RX;
using Dental.Application.Features.MOD_RX.DTOs;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;
namespace Dental.Infrastructure.Repositories;
public sealed partial class PrescriptionRepository
{
    public async Task<Result<PrescriptionPrintDocument>> PreparePrintAsync(int visitId, int prescriptionId, int actorId, string role,
        PrescriptionPrintRequest request, string? ip, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct); var committed = false;
        try
        {
            var visit = await db.Visits.AsNoTracking().SingleOrDefaultAsync(v => v.VisitId == visitId, ct);
            if (visit is null || !await CanReadAsync(visit, actorId, role, ct)) return Result<PrescriptionPrintDocument>.Failure(Error.NotFound);
            var rx = await db.Prescriptions.AsNoTracking().Include(p => p.Items).SingleOrDefaultAsync(p => p.Id == prescriptionId && p.VisitId == visitId, ct);
            if (rx is null) return Result<PrescriptionPrintDocument>.Failure(Error.NotFound);
            if (rx.IsCancelled || visit.Status == VisitStatuses.Cancelled) return Result<PrescriptionPrintDocument>.Failure(new("RX_002", "Đơn đã hủy, không được chuẩn bị bản in."));
            if (rx.Version != request.ExpectedVersion) return Result<PrescriptionPrintDocument>.Failure(Conflict);
            var patient = await db.Patients.AsNoTracking().SingleAsync(p => p.PatientId == visit.PatientId, ct);
            var doctor = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.UserId == rx.DoctorId, ct);
            // DL-297: a new preparation always queries the latest saved image; no pinned signature on Prescription.
            var signature = await db.DoctorSignatures.AsNoTracking().Where(s => s.DoctorId == rx.DoctorId).OrderByDescending(s => s.Version).FirstOrDefaultAsync(ct);
            var settings = await db.SystemConfigs.AsNoTracking().Where(s => s.Key == "Clinic:Address" || s.Key == "Clinic:Phone").ToDictionaryAsync(s => s.Key, s => s.Value, ct);
            var address = settings.GetValueOrDefault("Clinic:Address"); var phone = settings.GetValueOrDefault("Clinic:Phone");
            if (doctor is null || string.IsNullOrWhiteSpace(doctor.AcademicTitle) || signature is null || !SignaturePng.Valid(signature.ImagePng) ||
                string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(phone))
                return Result<PrescriptionPrintDocument>.Failure(new("RX_PRINT_SETUP", "Admin cần khai báo địa chỉ, hotline, học hàm/học vị và ảnh chữ ký bác sĩ trước khi in."));
            if (!PublicPrescriptionService.ValidCode(rx.PrescriptionCode) || rx.Items.Count == 0) return Result<PrescriptionPrintDocument>.Failure(Error.Validation);
            var document = new PrescriptionPrintDocument(rx.Id, visitId, rx.Version, rx.PrescriptionCode, "Nha khoa Hoàng Gia", address, phone,
                patient.FullName, patient.DateOfBirth, patient.Gender, patient.Address, patient.Phone, rx.Diagnosis,
                rx.Items.OrderBy(i => i.Id).Select(i => new PrescriptionPrintLine(i.MedicineName, i.Strength, i.Unit, i.Quantity, i.UsageInstructions, i.TreatmentDays)).ToArray(),
                rx.DoctorAdvice, rx.RevisitDate, DateOnly.FromDateTime(clock.ToVietnamTime(rx.CreatedAt)), doctor.FullName, doctor.AcademicTitle,
                signature.Version, Convert.ToBase64String(signature.ImagePng), "/portal/prescriptions/" + rx.PrescriptionCode, rx.ReceiverName, rx.ReceiverPhone);
            db.AuditLogs.Add(new AuditLog { UserId = actorId, Action = "MOD_RX_PRINT_PREPARED", EntityType = "Prescription", EntityId = rx.Id,
                CreatedAt = clock.UtcNow, IpAddress = ip, Detail = JsonSerializer.Serialize(new { rx.Version, signatureId = signature.Id, signatureVersion = signature.Version, trigger = "print_preparation" }) });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); committed = true;
            return Result<PrescriptionPrintDocument>.Success(document);
        }
        catch (Exception error) when (IsConflict(error)) { return Result<PrescriptionPrintDocument>.Failure(Conflict); }
        finally { if (!committed) db.ChangeTracker.Clear(); }
    }
}
