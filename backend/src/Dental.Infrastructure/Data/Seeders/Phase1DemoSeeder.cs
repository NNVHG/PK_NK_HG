using System.Security.Cryptography;
using System.Text;
using Dental.Application.Common;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Data.Seeders;

/// <summary>Explicit synthetic Development fixtures for Phase 1; never called by production startup.</summary>
public static class Phase1DemoSeeder
{
    private const string SharedContactPhone = "0900000091";
    private static readonly (string CitizenId, string Name)[] People =
    [
        ("000000000901", "Bệnh nhân Demo Một"), ("000000000902", "Bệnh nhân Demo Hai"),
        ("000000000903", "Bệnh nhân Demo Ba"), ("000000000904", "Bệnh nhân Demo Bốn"),
        ("000000000905", "Bệnh nhân Demo Năm"), ("000000000906", "Bệnh nhân Demo Sáu"),
    ];

    public static async Task SeedAsync(DentalDbContext db)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var admin = await db.Users.FirstAsync(u => u.IsActive && u.Role.RoleCode == RoleCodes.Admin);
        var patientRole = await db.Roles.SingleAsync(r => r.RoleCode == RoleCodes.Patient);
        var now = DateTime.UtcNow;
        var today = new VietnamClock(TimeProvider.System).Today;

        foreach (var (citizenId, name) in People)
        {
            var hash = Hash(citizenId);
            if (await db.Patients.AnyAsync(p => p.CitizenIdHash == hash)) continue;
            var user = new User { Phone = SharedContactPhone, FullName = name, RoleId = patientRole.RoleId,
                LoginByCitizenId = true, RequiresPhoneActivation = true,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(SharedContactPhone, 12), CreatedAt = now };
            db.Patients.Add(new Patient { User = user, FullName = name, Phone = SharedContactPhone,
                DateOfBirth = new DateOnly(1990, 1, 1), Gender = "Other", CitizenIdHash = hash,
                CitizenIdMasked = "********" + citizenId[^4..], CitizenIdVersion = 1, CreatedAt = now });
        }

        var chair = await db.DentalChairs.SingleOrDefaultAsync(c => c.ChairNumber == "DEMO01");
        if (chair is null)
        {
            chair = new DentalChair { ChairNumber = "DEMO01", RoomName = "Phòng Demo",
                Status = DentalChairStatus.Available, IsActive = true, CreatedByUserId = admin.UserId,
                Version = 1, CreatedAt = now };
            db.DentalChairs.Add(chair);
        }
        if (!await db.DentalServices.AnyAsync(s => s.Code == "DEMO01"))
            db.DentalServices.Add(new DentalService { Code = "DEMO01", Name = "Dịch vụ minh họa Phase 1",
                Description = "Dữ liệu demo; không phải bảng giá điều trị thực tế.", DurationMinutes = 30, CreatedAt = now,
                Prices = [new ServicePrice { Amount = 100000, EffectiveFrom = now.AddDays(-1), CreatedByUserId = admin.UserId, CreatedAt = now }] });
        if (!await db.MedicalSupplies.AnyAsync(s => s.Code == "DEMO01"))
            db.MedicalSupplies.Add(new MedicalSupply { Code = "DEMO01", Name = "Vật tư minh họa Phase 1", Unit = "Cái",
                MinimumStockThreshold = 5, CreatedByUserId = admin.UserId, CreatedAt = now,
                SearchText = "demo01 vat tu minh hoa phase 1" });

        await db.SaveChangesAsync();
        var patientHashes = People.Select(x => Hash(x.CitizenId)).ToArray();
        var patients = await db.Patients.Where(p => patientHashes.Contains(p.CitizenIdHash!))
            .OrderBy(p => p.PatientNumber).ToListAsync();
        var dentist = await db.Users.SingleOrDefaultAsync(u => u.IsActive && u.Role.RoleCode == RoleCodes.Dentist && u.Phone == "0900000003");
        var receptionist = await db.Users.SingleOrDefaultAsync(u => u.IsActive && u.Role.RoleCode == RoleCodes.Receptionist && u.Phone == "0900000002");
        var chairId = await EnsureDemoDentistLocationAsync(db, chair, dentist, admin.UserId);

        var appointmentStatuses = new[]
        {
            (0, "08:30", AppointmentStatuses.CheckedIn),
            (1, "09:00", AppointmentStatuses.CheckedIn),
            (2, "09:30", AppointmentStatuses.CheckedIn),
            (3, "10:00", AppointmentStatuses.Scheduled),
            (4, "10:30", AppointmentStatuses.Cancelled),
            (5, "11:00", AppointmentStatuses.Completed),
        };
        foreach (var (index, time, status) in appointmentStatuses)
        {
            var patient = patients.Single(p => p.CitizenIdHash == Hash(People[index].CitizenId));
            var slot = TimeOnly.Parse(time);
            if (await db.Appointments.AnyAsync(a => a.PatientId == patient.PatientId && a.AppointmentDate == today && a.SlotTime == slot)) continue;
            db.Appointments.Add(new Appointment { PatientId = patient.PatientId, AppointmentDate = today, SlotTime = slot,
                SlotDurationMinutes = 30, DentistId = dentist?.UserId, Status = status,
                Notes = "Dữ liệu minh họa Phase 1.", CreatedByUserId = admin.UserId, CreatedAt = now });
        }
        await db.SaveChangesAsync();

        var demoPatient = patients.Single(p => p.CitizenIdHash == Hash(People[0].CitizenId));
        var visit = await db.Visits.Where(v => v.PatientId == demoPatient.PatientId && v.Status == VisitStatuses.InProgress)
            .OrderByDescending(v => v.VisitId).FirstOrDefaultAsync();
        if (visit is null)
        {
            visit = new Visit { PatientId = demoPatient.PatientId, Status = VisitStatuses.InProgress,
                StartedAt = now.AddMinutes(-20), DentistId = dentist?.UserId, ChairId = chairId,
                CreatedByUserId = admin.UserId, ClinicalNotes = "Ca minh họa đang được nha sĩ thăm khám.",
                CreatedAt = now.AddMinutes(-20) };
            db.Visits.Add(visit);
            if (chairId.HasValue)
            {
                chair.Status = DentalChairStatus.Occupied;
                chair.UpdatedByUserId = admin.UserId;
                chair.UpdatedAt = now;
                chair.Version++;
            }
            await db.SaveChangesAsync();
        }

        if (!await db.MedicalHistoryRecords.AnyAsync(h => h.VisitId == visit.VisitId))
        {
            db.MedicalHistoryRecords.Add(new MedicalHistoryRecord { PatientId = demoPatient.PatientId, VisitId = visit.VisitId,
                RecordedByUserId = dentist?.UserId ?? admin.UserId, Note = "Tiền sử minh họa để kiểm thử cảnh báo an toàn.",
                CreatedAt = now.AddMinutes(-18), Items =
                [
                    new MedicalHistoryItem { Type = "Allergy", Name = "Dị ứng Penicillin (demo)", IsCritical = true, Detail = "Dữ liệu tổng hợp; không dùng làm tư vấn điều trị." },
                    new MedicalHistoryItem { Type = "Condition", Name = "Tăng huyết áp (demo)", IsCritical = true, Detail = "Dữ liệu tổng hợp; cần kiểm tra sinh hiệu trước thủ thuật." },
                ] });
        }
        if (!await db.VitalSignRecords.AnyAsync(v => v.VisitId == visit.VisitId))
            db.VitalSignRecords.Add(new VitalSignRecord { PatientId = demoPatient.PatientId, VisitId = visit.VisitId,
                RecordedByUserId = receptionist?.UserId ?? admin.UserId, SystolicBp = 120, DiastolicBp = 80,
                PulseBpm = 72, TemperatureC = 36.6m, Note = "Chỉ số minh họa.", CreatedAt = now.AddMinutes(-17) });
        if (!await db.ToothConditions.AnyAsync(t => t.VisitId == visit.VisitId))
            db.ToothConditions.Add(new ToothCondition { VisitId = visit.VisitId, ToothNumber = 16, Surface = "O",
                ConditionCode = "CARIES", Note = "Tình trạng răng minh họa.", CreatedAt = now.AddMinutes(-16) });

        var queuePatients = patients.Take(3).ToArray();
        for (var i = 0; i < queuePatients.Length; i++)
        {
            var queuePatient = queuePatients[i];
            if (await db.QueueEntries.AnyAsync(q => q.PatientId == queuePatient.PatientId && q.QueueDate == today)) continue;
            var linkedVisit = i == 0 ? visit : await db.Visits.Where(v => v.PatientId == queuePatient.PatientId && v.Status == VisitStatuses.Created)
                .OrderByDescending(v => v.VisitId).FirstOrDefaultAsync();
            if (i != 0 && linkedVisit is null)
            {
                linkedVisit = new Visit { PatientId = queuePatient.PatientId, Status = VisitStatuses.Created,
                    CreatedByUserId = admin.UserId, CreatedAt = now };
                db.Visits.Add(linkedVisit);
                await db.SaveChangesAsync();
            }
            var appointment = await db.Appointments.Where(a => a.PatientId == queuePatient.PatientId && a.AppointmentDate == today &&
                a.Status == AppointmentStatuses.CheckedIn).OrderBy(a => a.SlotTime).FirstOrDefaultAsync();
            var max = await db.QueueEntries.Where(q => q.QueueDate == today).MaxAsync(q => (int?)q.QueueNumber) ?? 0;
            db.QueueEntries.Add(new QueueEntry { PatientId = queuePatient.PatientId, AppointmentId = appointment?.AppointmentId,
                DentistId = i == 0 ? dentist?.UserId : null, VisitId = linkedVisit?.VisitId, QueueDate = today,
                QueueNumber = max + 1, IsPriority = appointment is not null, CheckInTime = now.AddMinutes(-10 + i),
                Status = i == 0 ? QueueStatus.InConsultation : QueueStatus.Waiting,
                Notes = i == 0 ? "Đang khám; dữ liệu minh họa." : "Lượt chờ minh họa." });
        }
        await db.SaveChangesAsync();

        foreach (var entry in await db.QueueEntries.Where(q => queuePatients.Select(p => p.PatientId).Contains(q.PatientId) && q.QueueDate == today)
            .Include(q => q.StatusHistories).ToListAsync())
        {
            if (entry.StatusHistories.Count != 0) continue;
            entry.StatusHistories.Add(new QueueStatusHistory { FromStatus = null, ToStatus = QueueStatus.Waiting,
                ChangedByUserId = receptionist?.UserId ?? admin.UserId, ChangedAt = entry.CheckInTime,
                Reason = "Tiếp đón minh họa Phase 1." });
            if (entry.Status == QueueStatus.InConsultation)
                entry.StatusHistories.Add(new QueueStatusHistory { FromStatus = QueueStatus.Waiting, ToStatus = QueueStatus.Calling,
                    ChangedByUserId = receptionist?.UserId ?? admin.UserId, ChangedAt = entry.CheckInTime.AddMinutes(1) });
            if (entry.Status == QueueStatus.InConsultation)
                entry.StatusHistories.Add(new QueueStatusHistory { FromStatus = QueueStatus.Calling, ToStatus = QueueStatus.InConsultation,
                    ChangedByUserId = dentist?.UserId ?? admin.UserId, ChangedAt = entry.CheckInTime.AddMinutes(2) });
        }

        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    private static async Task<int?> EnsureDemoDentistLocationAsync(DentalDbContext db, DentalChair chair, User? dentist, int adminId)
    {
        if (dentist is null || !chair.IsActive || chair.Status is DentalChairStatus.Maintenance or DentalChairStatus.Inactive) return null;
        var workspace = await db.StaffWorkspaces.SingleOrDefaultAsync(w => w.UserId == dentist.UserId);
        if (workspace is not null) return workspace.ChairId;
        if (await db.StaffWorkspaces.AnyAsync(w => w.ChairId == chair.Id) ||
            await db.Visits.AnyAsync(v => v.ChairId == chair.Id && v.Status == VisitStatuses.InProgress)) return null;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "SE Asia Standard Time"));
        db.StaffWorkspaces.Add(new StaffWorkspace { UserId = dentist.UserId, ChairId = chair.Id, RoomId = chair.RoomId, WorkDate = today,
            DentistId = dentist.UserId, UpdatedByUserId = adminId, Version = 1, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        return chair.Id;
    }

    private static string Hash(string citizenId)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(citizenId)));
}
