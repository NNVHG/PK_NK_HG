using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Data.Seeders;

public static partial class ClinicalDemoDataSeeder
{
    private static async Task<(Appointment Appt1, Appointment Appt2, Visit V1, Visit V2)> SeedVisitsAndQueueAsync(
        DentalDbContext db, Patient p1, Patient p2, Patient p3, Patient p4,
        User dentist1, User dentist2, User receptionist1, DateOnly today, DateTime now, CancellationToken ct)
    {
        var appt1 = new Appointment
        {
            PatientId = p1.PatientId, DentistId = dentist1.UserId, AppointmentDate = today,
            SlotTime = new TimeOnly(8, 30), SlotDurationMinutes = 30, Status = AppointmentStatuses.Completed,
            Notes = "Khám răng định kỳ và cạo vôi răng", CreatedByUserId = receptionist1.UserId, CreatedAt = now.AddDays(-3),
        };
        var appt2 = new Appointment
        {
            PatientId = p2.PatientId, DentistId = dentist1.UserId, AppointmentDate = today,
            SlotTime = new TimeOnly(9, 30), SlotDurationMinutes = 30, Status = AppointmentStatuses.CheckedIn,
            Notes = "Khám mẻ góc cắn răng cửa sau va chạm", CreatedByUserId = receptionist1.UserId, CreatedAt = now.AddDays(-1),
        };
        var appt3 = new Appointment
        {
            PatientId = p3.PatientId, DentistId = dentist2.UserId, AppointmentDate = today,
            SlotTime = new TimeOnly(14, 0), SlotDurationMinutes = 30, Status = AppointmentStatuses.Scheduled,
            Notes = "Khám răng trẻ em (răng sữa lung lay)", CreatedByUserId = receptionist1.UserId, CreatedAt = now.AddDays(-1),
        };
        var appt4 = new Appointment
        {
            PatientId = p4.PatientId, DentistId = dentist2.UserId, AppointmentDate = today.AddDays(1),
            SlotTime = new TimeOnly(9, 0), SlotDurationMinutes = 30, Status = AppointmentStatuses.Scheduled,
            Notes = "Tư vấn cấy ghép Implant", CreatedByUserId = receptionist1.UserId, CreatedAt = now,
        };
        db.Appointments.AddRange(appt1, appt2, appt3, appt4);
        await db.SaveChangesAsync(ct);

        var chair1 = await db.DentalChairs.FirstOrDefaultAsync(c => c.ChairNumber == "G01", ct);

        var v1 = new Visit
        {
            PatientId = p1.PatientId, DentistId = dentist1.UserId, ChairId = chair1?.Id,
            Status = VisitStatuses.Completed, StartedAt = now.Date.AddHours(8).AddMinutes(35), EndedAt = now.Date.AddHours(9).AddMinutes(15),
            Diagnosis = "Sâu ngà nông răng 36 mặt nhai; Viêm lợi mạn tính do vôi răng",
            ClinicalNotes = "Đã làm sạch xoang sâu R36, trám Composite thẩm mỹ xoang I. Cạo vôi răng siêu âm và đánh bóng 2 hàm hoàn tất.",
            IsLocked = true, LockedAt = now.Date.AddHours(9).AddMinutes(15), LockedBy = dentist1.UserId,
            CreatedByUserId = dentist1.UserId, CreatedAt = now.Date.AddHours(8).AddMinutes(35),
        };
        var v2 = new Visit
        {
            PatientId = p2.PatientId, DentistId = dentist1.UserId, ChairId = chair1?.Id,
            Status = VisitStatuses.InProgress, StartedAt = now.Date.AddHours(9).AddMinutes(25),
            Diagnosis = "Mẻ góc cắn răng 21 do chấn thương",
            ClinicalNotes = "Răng 21 mẻ góc cắn 1/3 thân răng, không lộ tủy, thử tủy dương tính. Đang tiến hành trám tái tạo Composite.",
            IsLocked = false, CreatedByUserId = dentist1.UserId, CreatedAt = now.Date.AddHours(9).AddMinutes(25),
        };
        db.Visits.AddRange(v1, v2);
        await db.SaveChangesAsync(ct);

        var q1 = new QueueEntry
        {
            PatientId = p1.PatientId, AppointmentId = appt1.AppointmentId, DentistId = dentist1.UserId, VisitId = v1.VisitId,
            QueueDate = today, QueueNumber = 1, IsPriority = false, CheckInTime = now.Date.AddHours(8).AddMinutes(25),
            Status = QueueStatus.Completed, Notes = "Tái khám theo lịch hẹn", CreatedAt = now.Date.AddHours(8).AddMinutes(25),
            StatusHistories =
            [
                new QueueStatusHistory { FromStatus = null, ToStatus = QueueStatus.Waiting, ChangedByUserId = receptionist1.UserId, ChangedAt = now.Date.AddHours(8).AddMinutes(25), Reason = "Tiếp đón check-in" },
                new QueueStatusHistory { FromStatus = QueueStatus.Waiting, ToStatus = QueueStatus.InConsultation, ChangedByUserId = dentist1.UserId, ChangedAt = now.Date.AddHours(8).AddMinutes(35), Reason = "Bắt đầu khám" },
                new QueueStatusHistory { FromStatus = QueueStatus.InConsultation, ToStatus = QueueStatus.Completed, ChangedByUserId = dentist1.UserId, ChangedAt = now.Date.AddHours(9).AddMinutes(15), Reason = "Hoàn tất ca khám" }
            ]
        };
        var q2 = new QueueEntry
        {
            PatientId = p2.PatientId, AppointmentId = appt2.AppointmentId, DentistId = dentist1.UserId, VisitId = v2.VisitId,
            QueueDate = today, QueueNumber = 2, IsPriority = false, CheckInTime = now.Date.AddHours(9).AddMinutes(20),
            Status = QueueStatus.InConsultation, Notes = "Răng mẻ cấp", CreatedAt = now.Date.AddHours(9).AddMinutes(20),
            StatusHistories =
            [
                new QueueStatusHistory { FromStatus = null, ToStatus = QueueStatus.Waiting, ChangedByUserId = receptionist1.UserId, ChangedAt = now.Date.AddHours(9).AddMinutes(20), Reason = "Tiếp đón check-in" },
                new QueueStatusHistory { FromStatus = QueueStatus.Waiting, ToStatus = QueueStatus.InConsultation, ChangedByUserId = dentist1.UserId, ChangedAt = now.Date.AddHours(9).AddMinutes(25), Reason = "Mời vào ghế điều trị" }
            ]
        };
        db.QueueEntries.AddRange(q1, q2);
        await db.SaveChangesAsync(ct);

        return (appt1, appt2, v1, v2);
    }
}
