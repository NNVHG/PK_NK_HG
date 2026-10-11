using Dental.Application.Common;
using Dental.Application.Features.Queue.DTOs;
using Dental.Application.Features.Queue.Services;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Tests.Common;
using NSubstitute;
using Xunit;

namespace Dental.Tests.Queue;

public sealed class QueueServiceTests
{
    private readonly IQueueRepository _queueRepository = Substitute.For<IQueueRepository>();
    private readonly IAppointmentRepository _appointmentRepository = Substitute.For<IAppointmentRepository>();
    private readonly IPatientRepository _patientRepository = Substitute.For<IPatientRepository>();
    private readonly IVisitRepository _visitRepository = Substitute.For<IVisitRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();
    private readonly IInvoiceDraftGenerator _invoiceDraft = Substitute.For<IInvoiceDraftGenerator>();
    private readonly FixedTimeProvider _timeProvider;
    private readonly VietnamClock _vietnamClock;

    public QueueServiceTests()
    {
        // 2026-10-10 02:00:00 UTC (09:00:00 giờ Việt Nam)
        _timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 10, 10, 2, 0, 0, TimeSpan.Zero));
        _vietnamClock = new VietnamClock(_timeProvider);

        _unitOfWork.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task<Result<QueueEntryResponse>>>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var func = call.Arg<Func<CancellationToken, Task<Result<QueueEntryResponse>>>>();
                return func(CancellationToken.None);
            });
    }

    private QueueService CreateService()
        => new(_queueRepository, _appointmentRepository, _patientRepository, _visitRepository, _userRepository, _unitOfWork, _auditLogger, _vietnamClock, _invoiceDraft);

    private static Patient MakePatient(int patientId = 1, bool isActive = true)
        => new()
        {
            PatientId = patientId,
            PatientNumber = 1001,
            FullName = "Trần Thị B",
            Phone = "0987654321",
            IsActive = isActive
        };

    [Fact]
    public async Task CheckInAsync_WalkInPatient_CreatesVisitInCreatedState_IssuesQueueNumber_NotPriority()
    {
        var patient = MakePatient();
        _patientRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(patient);
        _queueRepository.GetOpenQueueEntriesForPatientAsync(1, Arg.Any<CancellationToken>()).Returns(new List<QueueEntry>());
        _visitRepository.HasOpenVisitAsync(1, Arg.Any<CancellationToken>()).Returns(false);
        _queueRepository.GetNextQueueNumberAsync(_vietnamClock.Today, Arg.Any<CancellationToken>()).Returns(1);

        Visit? capturedVisit = null;
        await _visitRepository.AddAsync(Arg.Do<Visit>(v => capturedVisit = v), Arg.Any<CancellationToken>());

        QueueEntry? capturedEntry = null;
        await _queueRepository.AddAsync(Arg.Do<QueueEntry>(q => capturedEntry = q), Arg.Any<CancellationToken>());

        var result = await CreateService().CheckInAsync(
            actorUserId: 3,
            new CheckInRequest(PatientId: 1),
            ipAddress: "127.0.0.1");

        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedVisit);
        Assert.Equal(VisitStatuses.Created, capturedVisit!.Status);
        Assert.Null(capturedVisit.DentistId);

        Assert.NotNull(capturedEntry);
        Assert.Equal(1, capturedEntry!.QueueNumber);
        Assert.False(capturedEntry.IsPriority);
        Assert.Equal(QueueStatus.Waiting, capturedEntry.Status);
    }

    [Fact]
    public async Task CheckInAsync_OnTimeAppointment_InMinus30ToPlus15Window_GetsIsPriorityTrue()
    {
        var patient = MakePatient();
        var appointment = new Appointment
        {
            AppointmentId = 8,
            PatientId = 1,
            AppointmentDate = _vietnamClock.Today,
            SlotTime = new TimeOnly(9, 15),
            Status = AppointmentStatuses.Scheduled
        };

        _patientRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(patient);
        _appointmentRepository.GetByIdAsync(8, Arg.Any<CancellationToken>()).Returns(appointment);
        _queueRepository.GetOpenQueueEntriesForPatientAsync(1, Arg.Any<CancellationToken>()).Returns(new List<QueueEntry>());
        _visitRepository.HasOpenVisitAsync(1, Arg.Any<CancellationToken>()).Returns(false);
        _queueRepository.GetNextQueueNumberAsync(_vietnamClock.Today, Arg.Any<CancellationToken>()).Returns(2);

        QueueEntry? capturedEntry = null;
        await _queueRepository.AddAsync(Arg.Do<QueueEntry>(q => capturedEntry = q), Arg.Any<CancellationToken>());

        var result = await CreateService().CheckInAsync(
            actorUserId: 3,
            new CheckInRequest(PatientId: 1, AppointmentId: 8),
            ipAddress: "127.0.0.1");

        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedEntry);
        Assert.True(capturedEntry!.IsPriority);
        Assert.Equal(AppointmentStatuses.CheckedIn, appointment.Status);
    }

    [Fact]
    public async Task CheckInAsync_AppointmentFromAnotherDate_ReturnsConflictWithoutCreatingQueueEntry()
    {
        var patient = MakePatient();
        var appointment = new Appointment
        {
            AppointmentId = 18,
            PatientId = 1,
            AppointmentDate = _vietnamClock.Today.AddDays(1),
            SlotTime = new TimeOnly(9, 15),
            Status = AppointmentStatuses.Scheduled
        };
        _patientRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(patient);
        _appointmentRepository.GetByIdAsync(18, Arg.Any<CancellationToken>()).Returns(appointment);
        _queueRepository.GetOpenQueueEntriesForPatientAsync(1, Arg.Any<CancellationToken>()).Returns(new List<QueueEntry>());
        _visitRepository.HasOpenVisitAsync(1, Arg.Any<CancellationToken>()).Returns(false);

        var result = await CreateService().CheckInAsync(
            actorUserId: 3,
            new CheckInRequest(PatientId: 1, AppointmentId: 18),
            ipAddress: null);

        Assert.True(result.IsFailure);
        Assert.Equal(Error.AppointmentCannotCheckIn.Code, result.Error.Code);
        Assert.Equal(AppointmentStatuses.Scheduled, appointment.Status);
        await _unitOfWork.DidNotReceive().ExecuteInTransactionAsync(
            Arg.Any<Func<CancellationToken, Task<Result<QueueEntryResponse>>>>(),
            Arg.Any<CancellationToken>());
    }
    [Fact]
    public async Task CheckInAsync_EarlyAppointment_MoreThan30MinutesEarly_GetsIsPriorityFalse()
    {
        var patient = MakePatient();
        var appointment = new Appointment
        {
            AppointmentId = 9,
            PatientId = 1,
            AppointmentDate = _vietnamClock.Today,
            SlotTime = new TimeOnly(10, 0),
            Status = AppointmentStatuses.Scheduled
        };

        _patientRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(patient);
        _appointmentRepository.GetByIdAsync(9, Arg.Any<CancellationToken>()).Returns(appointment);
        _queueRepository.GetOpenQueueEntriesForPatientAsync(1, Arg.Any<CancellationToken>()).Returns(new List<QueueEntry>());
        _visitRepository.HasOpenVisitAsync(1, Arg.Any<CancellationToken>()).Returns(false);
        _queueRepository.GetNextQueueNumberAsync(_vietnamClock.Today, Arg.Any<CancellationToken>()).Returns(3);

        QueueEntry? capturedEntry = null;
        await _queueRepository.AddAsync(Arg.Do<QueueEntry>(q => capturedEntry = q), Arg.Any<CancellationToken>());

        var result = await CreateService().CheckInAsync(
            actorUserId: 3,
            new CheckInRequest(PatientId: 1, AppointmentId: 9),
            ipAddress: "127.0.0.1");

        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedEntry);
        Assert.False(capturedEntry!.IsPriority);
    }

    [Fact]
    public async Task CheckInAsync_LateAppointment_MoreThan15MinutesLate_GetsIsPriorityFalse()
    {
        var patient = MakePatient();
        var appointment = new Appointment
        {
            AppointmentId = 10,
            PatientId = 1,
            AppointmentDate = _vietnamClock.Today,
            SlotTime = new TimeOnly(8, 30),
            Status = AppointmentStatuses.Scheduled
        };

        _patientRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(patient);
        _appointmentRepository.GetByIdAsync(10, Arg.Any<CancellationToken>()).Returns(appointment);
        _queueRepository.GetOpenQueueEntriesForPatientAsync(1, Arg.Any<CancellationToken>()).Returns(new List<QueueEntry>());
        _visitRepository.HasOpenVisitAsync(1, Arg.Any<CancellationToken>()).Returns(false);
        _queueRepository.GetNextQueueNumberAsync(_vietnamClock.Today, Arg.Any<CancellationToken>()).Returns(4);

        QueueEntry? capturedEntry = null;
        await _queueRepository.AddAsync(Arg.Do<QueueEntry>(q => capturedEntry = q), Arg.Any<CancellationToken>());

        var result = await CreateService().CheckInAsync(
            actorUserId: 3,
            new CheckInRequest(PatientId: 1, AppointmentId: 10),
            ipAddress: "127.0.0.1");

        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedEntry);
        Assert.False(capturedEntry!.IsPriority);
    }

    [Fact]
    public async Task CheckInAsync_PatientHasExistingOpenQueueEntryToday_FailsWithAlreadyOpen()
    {
        // Arrange: DL-045 tối đa 1 QueueEntry mở
        var patient = MakePatient();
        var openEntry = new QueueEntry
        {
            QueueEntryId = 1,
            PatientId = 1,
            QueueDate = _vietnamClock.Today,
            Status = QueueStatus.Waiting
        };

        _patientRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(patient);
        _queueRepository.GetOpenQueueEntriesForPatientAsync(1, Arg.Any<CancellationToken>()).Returns(new List<QueueEntry> { openEntry });

        // Act
        var result = await CreateService().CheckInAsync(
            actorUserId: 3,
            new CheckInRequest(PatientId: 1),
            ipAddress: "127.0.0.1");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(Error.QueueEntryAlreadyOpen.Code, result.Error.Code);
    }

    [Fact]
    public async Task CheckInAsync_PatientHasStaleOpenEntryFromYesterday_AutoCancelsAndAllowsCheckIn()
    {
        // Arrange: DL-051 tồn đọng qua ngày tự động hủy
        var yesterday = _vietnamClock.Today.AddDays(-1);
        var patient = MakePatient();
        var staleVisit = new Visit { VisitId = 99, Status = VisitStatuses.Created, CreatedAt = DateTime.UtcNow.AddDays(-1) };
        var staleEntry = new QueueEntry
        {
            QueueEntryId = 50,
            PatientId = 1,
            QueueDate = yesterday,
            Status = QueueStatus.Waiting,
            Visit = staleVisit
        };

        _patientRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(patient);
        _queueRepository.GetOpenQueueEntriesForPatientAsync(1, Arg.Any<CancellationToken>()).Returns(new List<QueueEntry> { staleEntry });
        _visitRepository.HasOpenVisitAsync(1, Arg.Any<CancellationToken>()).Returns(false);
        _queueRepository.GetNextQueueNumberAsync(_vietnamClock.Today, Arg.Any<CancellationToken>()).Returns(1);

        // Act
        var result = await CreateService().CheckInAsync(
            actorUserId: 3,
            new CheckInRequest(PatientId: 1),
            ipAddress: "127.0.0.1");

        // Assert: Lượt cũ bị chuyển sang Cancelled; lượt mới được tạo thành công
        Assert.True(result.IsSuccess);
        Assert.Equal(QueueStatus.Cancelled, staleEntry.Status);
        Assert.Equal(VisitStatuses.Cancelled, staleVisit.Status);
        await _queueRepository.Received(1).AddStatusHistoryAsync(
            Arg.Is<QueueStatusHistory>(h => h.QueueEntryId == 50 && h.ToStatus == QueueStatus.Cancelled),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckInAsync_ConcurrentCheckIn_RetriesOnConflictAndAllocatesNextNumber()
    {
        // Arrange: Giả lập 2 lễ tân check-in đồng thời dẫn đến SaveChanges conflict ở lần 1, retry lần 2 thành công
        var patient = MakePatient();
        _patientRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(patient);
        _queueRepository.GetOpenQueueEntriesForPatientAsync(1, Arg.Any<CancellationToken>()).Returns(new List<QueueEntry>());
        _visitRepository.HasOpenVisitAsync(1, Arg.Any<CancellationToken>()).Returns(false);

        // Lần 1 lấy số 1, lần 2 retry lấy số 2
        _queueRepository.GetNextQueueNumberAsync(_vietnamClock.Today, Arg.Any<CancellationToken>())
            .Returns(1, 2);

        var saveCallCount = 0;
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(call =>
        {
            saveCallCount++;
            // Lần 1 lưu visit OK, lần 2 lưu QueueEntry gặp xung đột concurrency, lần 3 lưu QueueEntry retry OK
            if (saveCallCount == 2)
                throw new InvalidOperationException("Unique constraint violation");
            return Task.FromResult(1);
        });

        // Act
        var result = await CreateService().CheckInAsync(
            actorUserId: 3,
            new CheckInRequest(PatientId: 1),
            ipAddress: "127.0.0.1");

        // Assert: Thử lại thành công và cấp số 2
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.QueueNumber);
    }

    [Fact]
    public async Task UpdateQueueStatusAsync_ToInConsultation_RequiresDentistId_UpdatesVisitToInProgress()
    {
        // Arrange: Chuyển Waiting -> InConsultation phải có DentistId (DL-013)
        var visit = new Visit { VisitId = 10, Status = VisitStatuses.Created };
        var entry = new QueueEntry
        {
            QueueEntryId = 15,
            PatientId = 1,
            DentistId = null,
            Status = QueueStatus.Waiting,
            Visit = visit
        };
        var dentist = new User
        {
            UserId = 5,
            Role = new Role { RoleCode = RoleCodes.Dentist },
            IsActive = true
        };

        _queueRepository.GetByIdAsync(15, Arg.Any<CancellationToken>()).Returns(entry);
        _userRepository.FindByIdAsync(5, Arg.Any<CancellationToken>()).Returns(dentist);

        // Act
        var result = await CreateService().UpdateQueueStatusAsync(
            queueEntryId: 15,
            actorUserId: 5,
            actorRole: RoleCodes.Dentist,
            new UpdateQueueStatusRequest(NewStatus: (int)QueueStatus.InConsultation, DentistId: 5),
            ipAddress: "127.0.0.1");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(QueueStatus.InConsultation, entry.Status);
        Assert.Equal(5, entry.DentistId);
        Assert.Equal(VisitStatuses.InProgress, visit.Status);
        Assert.Equal(5, visit.DentistId);
        Assert.NotNull(visit.StartedAt);
    }

    [Fact]
    public async Task UpdateQueueStatusAsync_FromInImagingToWaiting_SetsIsPriorityTrue()
    {
        // Arrange: DL-053 Phụ tá xác nhận sau chụp ảnh -> Chuyển về Waiting với ưu tiên cao nhất
        var entry = new QueueEntry
        {
            QueueEntryId = 20,
            PatientId = 1,
            DentistId = 5,
            IsPriority = false,
            Status = QueueStatus.InImaging
        };

        _queueRepository.GetByIdAsync(20, Arg.Any<CancellationToken>()).Returns(entry);

        // Act
        var result = await CreateService().UpdateQueueStatusAsync(
            queueEntryId: 20,
            actorUserId: 4,
            actorRole: RoleCodes.Assistant,
            new UpdateQueueStatusRequest(NewStatus: (int)QueueStatus.Waiting),
            ipAddress: "127.0.0.1");

        // Assert: DL-053 IsPriority = true và giữ nguyên DentistId = 5
        Assert.True(result.IsSuccess);
        Assert.Equal(QueueStatus.Waiting, entry.Status);
        Assert.True(entry.IsPriority);
        Assert.Equal(5, entry.DentistId);
    }

}
