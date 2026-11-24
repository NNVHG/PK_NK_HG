using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Data.Seeders;

public static partial class ClinicalDemoDataSeeder
{
    private static async Task<(Appointment Appt1, Appointment Appt2, Visit V1, Visit V2, Visit V3, Visit V4)> SeedVisitsAndQueueAsync(
        DentalDbContext db, Patient p1, Patient p2, Patient p3, Patient p4, Patient p5,
        User dentist1, User dentist2, User receptionist1, DateOnly today, DateTime now, CancellationToken ct)
    {
        // Yêu cầu đặt lịch trực tuyến (MOD_APP - chờ Lễ tân/Admin duyệt)
        db.BookingRequests.AddRange(
            new BookingRequest
            {
                ClientRequestId = Guid.NewGuid(),
                IntentDigest = "DEMO_BOOKING_REQUEST_ADULT_01",
                FullName = "Võ Minh Tuấn",
                Phone = "0900000031",
                DateOfBirth = new DateOnly(1992, 4, 18),
                Gender = "Nam",
                Notes = "Đau nhức răng hàm dưới bên phải, muốn kiểm tra tổng quát",
                AppointmentDate = today.AddDays(1),
                SlotTime = new TimeOnly(10, 30),
                SlotDurationMinutes = 30,
                Status = "Pending",
                Version = 1,
                CreatedAt = now.AddHours(-2),
            },
            new BookingRequest
            {
                ClientRequestId = Guid.NewGuid(),
                IntentDigest = "DEMO_BOOKING_REQUEST_CHILD_02",
                FullName = "Trần Gia Hân",
                Phone = "0900000032",
                DateOfBirth = new DateOnly(2019, 11, 5),
                Gender = "Nữ",
                Notes = "Trẻ dưới 14 tuổi - khám sâu răng sữa và kiểm tra thay răng",
                AppointmentDate = today.AddDays(2),
                SlotTime = new TimeOnly(15, 0),
                SlotDurationMinutes = 30,
                Status = "Pending",
                Version = 1,
                CreatedAt = now.AddHours(-1),
            }
        );

        var appt1 = new Appointment
        {
            PatientId = p1.PatientId, AppointmentDate = today,
            SlotTime = new TimeOnly(8, 30), SlotDurationMinutes = 30, Status = AppointmentStatuses.Completed,
            Notes = "Khám răng định kỳ và cạo vôi răng", CreatedByUserId = receptionist1.UserId, CreatedAt = now.AddDays(-3),
        };
        var appt2 = new Appointment
        {
            PatientId = p2.PatientId, AppointmentDate = today,
            SlotTime = new TimeOnly(9, 30), SlotDurationMinutes = 30, Status = AppointmentStatuses.CheckedIn,
            Notes = "Khám mẻ góc cắn răng cửa sau va chạm", CreatedByUserId = receptionist1.UserId, CreatedAt = now.AddDays(-1),
        };
        var appt3 = new Appointment
        {
            PatientId = p3.PatientId, AppointmentDate = today,
            SlotTime = new TimeOnly(14, 0), SlotDurationMinutes = 30, Status = AppointmentStatuses.CheckedIn,
            Notes = "Khám răng trẻ em (răng sữa lung lay)", CreatedByUserId = receptionist1.UserId, CreatedAt = now.AddDays(-1),
        };
        var appt4 = new Appointment
        {
            PatientId = p4.PatientId, AppointmentDate = today.AddDays(1),
            SlotTime = new TimeOnly(9, 0), SlotDurationMinutes = 30, Status = AppointmentStatuses.Scheduled,
            Notes = "Tư vấn cấy ghép Implant và thu tiếp đợt thanh toán còn lại", CreatedByUserId = receptionist1.UserId, CreatedAt = now,
        };
        var appt5 = new Appointment
        {
            PatientId = p4.PatientId, AppointmentDate = today.AddDays(-2),
            SlotTime = new TimeOnly(10, 0), SlotDurationMinutes = 30, Status = AppointmentStatuses.Completed,
            Notes = "Điều trị tủy răng 46 (đợt 1)", CreatedByUserId = receptionist1.UserId, CreatedAt = now.AddDays(-5),
        };
        var appt6 = new Appointment
        {
            PatientId = p1.PatientId, AppointmentDate = today.AddDays(-5),
            SlotTime = new TimeOnly(15, 0), SlotDurationMinutes = 30, Status = AppointmentStatuses.Cancelled,
            Notes = "Khách xin hủy do bận công tác", CreatedByUserId = receptionist1.UserId, CreatedAt = now.AddDays(-7),
        };
        db.Appointments.AddRange(appt1, appt2, appt3, appt4, appt5, appt6);
        await db.SaveChangesAsync(ct);

        var rooms = await db.ClinicRooms.OrderBy(r => r.RoomCode).ToListAsync(ct);
        var room1 = rooms.FirstOrDefault(r => r.RoomCode == "G01");
        var room2 = rooms.FirstOrDefault(r => r.RoomCode == "G02");

        var v1 = new Visit
        {
            PatientId = p1.PatientId, DentistId = dentist1.UserId, RoomId = room1?.Id,
            Status = VisitStatuses.Completed, StartedAt = now.Date.AddHours(8).AddMinutes(35), EndedAt = now.Date.AddHours(9).AddMinutes(15),
            Diagnosis = "Sâu ngà nông răng 36 mặt nhai; Viêm lợi mạn tính do vôi răng",
            ClinicalNotes = "Đã làm sạch xoang sâu R36, trám Composite thẩm mỹ xoang I. Cạo vôi răng siêu âm và đánh bóng 2 hàm hoàn tất.",
            IsLocked = true, LockedAt = now.Date.AddHours(9).AddMinutes(15), LockedBy = dentist1.UserId,
            CreatedByUserId = dentist1.UserId, CreatedAt = now.Date.AddHours(8).AddMinutes(35),
        };
        var v2 = new Visit
        {
            PatientId = p2.PatientId, DentistId = dentist1.UserId, RoomId = room1?.Id,
            Status = VisitStatuses.InProgress, StartedAt = now.Date.AddHours(9).AddMinutes(25),
            Diagnosis = "Mẻ góc cắn răng 21 do chấn thương",
            ClinicalNotes = "Răng 21 mẻ góc cắn 1/3 thân răng, không lộ tủy, thử tủy dương tính. Đang tiến hành trám tái tạo Composite.",
            IsLocked = false, CreatedByUserId = dentist1.UserId, CreatedAt = now.Date.AddHours(9).AddMinutes(25),
        };
        var v3 = new Visit
        {
            PatientId = p4.PatientId, DentistId = dentist2.UserId, RoomId = room2?.Id,
            Status = VisitStatuses.Completed, StartedAt = now.Date.AddDays(-2).AddHours(10).AddMinutes(5), EndedAt = now.Date.AddDays(-2).AddHours(10).AddMinutes(50),
            Diagnosis = "Viêm tủy không hồi phục răng 46",
            ClinicalNotes = "Đã mở tủy, làm sạch ống tủy và đặt thuốc băng tủy răng 46.",
            IsLocked = true, LockedAt = now.Date.AddDays(-2).AddHours(10).AddMinutes(50), LockedBy = dentist2.UserId,
            CreatedByUserId = dentist2.UserId, CreatedAt = now.Date.AddDays(-2).AddHours(10).AddMinutes(5),
        };
        // Ca tạm dừng bàn giao (Paused -> nhả RoomId để nha sĩ khác tiếp nhận)
        var v4 = new Visit
        {
            PatientId = p5.PatientId, DentistId = dentist1.UserId, RoomId = null,
            Status = VisitStatuses.InProgress, StartedAt = now.Date.AddHours(9).AddMinutes(50),
            Diagnosis = "Đau khớp thái dương hàm nhẹ và kiểm tra răng 16",
            ClinicalNotes = "Đã khám sơ bộ, tạm dừng để bàn giao sang nha sĩ ca chiều tiếp tục.",
            IsLocked = false, CreatedByUserId = dentist1.UserId, CreatedAt = now.Date.AddHours(9).AddMinutes(50),
        };
        var v5 = new Visit
        {
            PatientId = p3.PatientId, Status = VisitStatuses.Created,
            CreatedByUserId = receptionist1.UserId, CreatedAt = now.Date.AddHours(10).AddMinutes(10),
        };
        if (room1 is not null) room1.Status = DentalChairStatus.Occupied;
        db.Visits.AddRange(v1, v2, v3, v4, v5);
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
            Status = QueueStatus.InConsultation, Notes = "Chấn thương răng cửa", CreatedAt = now.Date.AddHours(9).AddMinutes(20),
            StatusHistories =
            [
                new QueueStatusHistory { FromStatus = null, ToStatus = QueueStatus.Waiting, ChangedByUserId = receptionist1.UserId, ChangedAt = now.Date.AddHours(9).AddMinutes(20), Reason = "Tiếp đón check-in" },
                new QueueStatusHistory { FromStatus = QueueStatus.Waiting, ToStatus = QueueStatus.InConsultation, ChangedByUserId = dentist1.UserId, ChangedAt = now.Date.AddHours(9).AddMinutes(25), Reason = "Mời vào phòng điều trị" }
            ]
        };
        var q3 = new QueueEntry
        {
            PatientId = p4.PatientId, AppointmentId = appt5.AppointmentId, DentistId = dentist2.UserId, VisitId = v3.VisitId,
            QueueDate = today.AddDays(-2), QueueNumber = 1, IsPriority = false, CheckInTime = now.Date.AddDays(-2).AddHours(9).AddMinutes(55),
            Status = QueueStatus.Completed, Notes = "Khám điều trị tủy theo lịch hẹn", CreatedAt = now.Date.AddDays(-2).AddHours(9).AddMinutes(55),
            StatusHistories =
            [
                new QueueStatusHistory { FromStatus = null, ToStatus = QueueStatus.Waiting, ChangedByUserId = receptionist1.UserId, ChangedAt = now.Date.AddDays(-2).AddHours(9).AddMinutes(55), Reason = "Tiếp đón check-in" },
                new QueueStatusHistory { FromStatus = QueueStatus.Waiting, ToStatus = QueueStatus.InConsultation, ChangedByUserId = dentist2.UserId, ChangedAt = now.Date.AddDays(-2).AddHours(10).AddMinutes(5), Reason = "Bắt đầu khám" },
                new QueueStatusHistory { FromStatus = QueueStatus.InConsultation, ToStatus = QueueStatus.Completed, ChangedByUserId = dentist2.UserId, ChangedAt = now.Date.AddDays(-2).AddHours(10).AddMinutes(50), Reason = "Hoàn tất ca khám" }
            ]
        };
        var q4 = new QueueEntry
        {
            PatientId = p5.PatientId, DentistId = dentist1.UserId, VisitId = v4.VisitId,
            QueueDate = today, QueueNumber = 3, IsPriority = false, CheckInTime = now.Date.AddHours(9).AddMinutes(40),
            Status = QueueStatus.Paused, Notes = "Ca tạm dừng để bàn giao bác sĩ", CreatedAt = now.Date.AddHours(9).AddMinutes(40),
            StatusHistories =
            [
                new QueueStatusHistory { FromStatus = null, ToStatus = QueueStatus.Waiting, ChangedByUserId = receptionist1.UserId, ChangedAt = now.Date.AddHours(9).AddMinutes(40), Reason = "Tiếp đón tại quầy" },
                new QueueStatusHistory { FromStatus = QueueStatus.Waiting, ToStatus = QueueStatus.InConsultation, ChangedByUserId = dentist1.UserId, ChangedAt = now.Date.AddHours(9).AddMinutes(50), Reason = "Bắt đầu khám" },
                new QueueStatusHistory { FromStatus = QueueStatus.InConsultation, ToStatus = QueueStatus.Paused, ChangedByUserId = dentist1.UserId, ChangedAt = now.Date.AddHours(10).AddMinutes(5), Reason = "Tạm dừng bàn giao ca" }
            ]
        };
        var q5 = new QueueEntry
        {
            PatientId = p3.PatientId, AppointmentId = appt3.AppointmentId, DentistId = null, VisitId = v5.VisitId,
            QueueDate = today, QueueNumber = 4, IsPriority = false, CheckInTime = now.Date.AddHours(10).AddMinutes(10),
            Status = QueueStatus.Waiting, Notes = "Khách đang chờ gọi số vào phòng khám", CreatedAt = now.Date.AddHours(10).AddMinutes(10),
            StatusHistories =
            [
                new QueueStatusHistory { FromStatus = null, ToStatus = QueueStatus.Waiting, ChangedByUserId = receptionist1.UserId, ChangedAt = now.Date.AddHours(10).AddMinutes(10), Reason = "Tiếp đón tại quầy" }
            ]
        };
        db.QueueEntries.AddRange(q1, q2, q3, q4, q5);

        // Giữ lượt trẻ em trong hàng đợi chung. Ca khác đã điều phối để thử Phụ tá gọi cho Nha sĩ 2.
        if (dentist2.UserId != dentist1.UserId && room2 is { IsActive: true, Status: DentalChairStatus.Available })
        {
            var checkInTime = q5.CheckInTime.AddMinutes(5);
            db.QueueEntries.Add(new QueueEntry
            {
                PatientId = p4.PatientId, DentistId = dentist2.UserId, QueueDate = today,
                QueueNumber = q5.QueueNumber + 1, CheckInTime = checkInTime, CreatedAt = checkInTime,
                Status = QueueStatus.Waiting, Notes = "Lượt demo đã điều phối cho nha sĩ hỗ trợ.",
                Visit = new Visit
                {
                    PatientId = p4.PatientId, Status = VisitStatuses.Created,
                    CreatedByUserId = receptionist1.UserId, CreatedAt = checkInTime,
                },
                StatusHistories =
                [
                    new QueueStatusHistory
                    {
                        ToStatus = QueueStatus.Waiting, ChangedByUserId = receptionist1.UserId,
                        ChangedAt = checkInTime, Reason = "Tiếp đón và điều phối lượt demo",
                    },
                ],
            });
        }
        await db.SaveChangesAsync(ct);

        return (appt1, appt2, v1, v2, v3, v4);
    }
}
