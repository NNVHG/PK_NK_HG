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
        _unitOfWork.ExecuteSerializableTransactionAsync(Arg.Any<Func<CancellationToken, Task<Result<QueueEntryResponse>>>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task<Result<QueueEntryResponse>>>>()(CancellationToken.None));

        _unitOfWork.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task<Result<QueueEntryResponse>>>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var func = call.Arg<Func<CancellationToken, Task<Result<QueueEntryResponse>>>>();
                return func(CancellationToken.None);
            });
    }

    [Theory]
    [InlineData(RoleCodes.Dentist, 5)]
    [InlineData(RoleCodes.Admin, 99)]
    public async Task Complete_LocksVisitAndGeneratesInvoiceWithoutSeparateSave(string role, int actor)
    {
        var visit = new Visit { VisitId = 8, DentistId = 5, Status = VisitStatuses.InProgress };
        var entry = new QueueEntry { QueueEntryId = 8, DentistId = 5, Visit = visit, Status = QueueStatus.InConsultation };
        _queueRepository.GetByIdAsync(8, Arg.Any<CancellationToken>()).Returns(entry);
        _invoiceDraft.GenerateAsync(visit, actor, role, null, Arg.Any<CancellationToken>()).Returns(Result.Success());
        var result = await CreateService().UpdateQueueStatusAsync(8, actor, role, new UpdateQueueStatusRequest(3), null);
        Assert.True(result.IsSuccess);
        Assert.True(visit.IsLocked);
        Assert.NotNull(visit.LockedAt);
        Assert.Equal(actor, visit.LockedBy);
        Assert.Equal(VisitStatuses.Completed, visit.Status);
        Assert.NotNull(visit.EndedAt);
        await _invoiceDraft.Received(1).GenerateAsync(visit, actor, role, null, Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Complete_OtherDentistCannotLockOrGenerate()
    {
        var visit = new Visit { DentistId = 5, Status = VisitStatuses.InProgress };
        _queueRepository.GetByIdAsync(8, Arg.Any<CancellationToken>()).Returns(new QueueEntry { Visit = visit, Status = QueueStatus.InConsultation });
        Assert.Equal(Error.Forbidden, (await CreateService().UpdateQueueStatusAsync(8, 9, RoleCodes.Dentist, new UpdateQueueStatusRequest(3), null)).Error);
        Assert.False(visit.IsLocked);
        Assert.Equal(VisitStatuses.InProgress, visit.Status);
        Assert.Empty(_invoiceDraft.ReceivedCalls());
    }

    [Fact]
    public async Task Complete_InvoiceFailureDoesNotSaveOrAuditSuccess()
    {
        var visit = new Visit { DentistId = 5, Status = VisitStatuses.InProgress };
        _queueRepository.GetByIdAsync(8, Arg.Any<CancellationToken>()).Returns(new QueueEntry { Visit = visit, Status = QueueStatus.InConsultation });
        _invoiceDraft.GenerateAsync(visit, 5, RoleCodes.Dentist, null, Arg.Any<CancellationToken>()).Returns(Result.Failure(Error.Conflict));
        Assert.True((await CreateService().UpdateQueueStatusAsync(8, 5, RoleCodes.Dentist, new UpdateQueueStatusRequest(3), null)).IsFailure);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        Assert.Empty(_auditLogger.ReceivedCalls());
    }

    [Theory]
    [InlineData(QueueStatus.InConsultation)]
    [InlineData(QueueStatus.InImaging)]
    [InlineData(QueueStatus.Waiting)]
    [InlineData(QueueStatus.Calling)]
    public async Task CancelClinical_StagesDraftBeforeMutatingVisitAndSaving(QueueStatus status)
    {
        var visit = new Visit { VisitId = 10, DentistId = 5, Status = VisitStatuses.InProgress };
        var entry = new QueueEntry { QueueEntryId = 8, VisitId = 10, Visit = visit, Status = status };
        _queueRepository.GetByIdAsync(8, Arg.Any<CancellationToken>()).Returns(entry);
        _invoiceDraft.StageVisitCancellationAsync(visit, 5, RoleCodes.Dentist, null, Arg.Any<CancellationToken>()).Returns(_ => {
            Assert.Equal(VisitStatuses.InProgress, visit.Status); Assert.Equal(status, entry.Status); return Result.Success();
        });
        var result = await CreateService().UpdateQueueStatusAsync(8, 5, RoleCodes.Dentist, new(4, Reason: "Synthetic cancellation reason"), null);
        Assert.True(result.IsSuccess); Assert.Equal(VisitStatuses.Cancelled, visit.Status);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _invoiceDraft.Received(1).StageVisitCancellationAsync(visit, 5, RoleCodes.Dentist, null, Arg.Any<CancellationToken>());
        await _invoiceDraft.DidNotReceive().GenerateAsync(Arg.Any<Visit>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelClinical_FinancialConflictDoesNotChangeOrSaveQueueVisitHistory()
    {
        var visit = new Visit { VisitId = 10, DentistId = 5, Status = VisitStatuses.InProgress };
        var entry = new QueueEntry { QueueEntryId = 8, VisitId = 10, Visit = visit, Status = QueueStatus.InConsultation };
        _queueRepository.GetByIdAsync(8, Arg.Any<CancellationToken>()).Returns(entry);
        _invoiceDraft.StageVisitCancellationAsync(visit, 5, RoleCodes.Dentist, null, Arg.Any<CancellationToken>()).Returns(Result.Failure(new Error("BIL_013", "Paid")));
        Assert.Equal("BIL_013", (await CreateService().UpdateQueueStatusAsync(8, 5, RoleCodes.Dentist, new(4, Reason: "Synthetic cancellation reason"), null)).Error.Code);
        Assert.Equal(VisitStatuses.InProgress, visit.Status); Assert.Equal(QueueStatus.InConsultation, entry.Status);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _queueRepository.DidNotReceive().AddStatusHistoryAsync(Arg.Any<QueueStatusHistory>(), Arg.Any<CancellationToken>());
        Assert.Empty(_auditLogger.ReceivedCalls());
    }

    private QueueService CreateService()
        => new(_queueRepository, _appointmentRepository, _patientRepository, _visitRepository, _userRepository, _unitOfWork, _auditLogger, _vietnamClock, _invoiceDraft, Dental.Tests.Common.TestVisitChairs.Create());

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
            Arg.Is<QueueStatusHistory>(h => h.QueueEntryId == 50 && h.FromStatus == QueueStatus.Waiting && h.ToStatus == QueueStatus.Cancelled),
            Arg.Any<CancellationToken>());
    }

    [Theory] [InlineData(QueueStatus.Calling)] [InlineData(QueueStatus.Skipped)]
    public async Task CheckIn_ExpiresOnlyOldUnstartedQueuesAndExcludesTheirVisitFromOpenCheck(QueueStatus status)
    {
        var old = new QueueEntry { QueueEntryId = 50, QueueDate = _vietnamClock.Today.AddDays(-1), Status = status,
            Visit = new Visit { VisitId = 99, Status = VisitStatuses.Created } };
        _patientRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(MakePatient());
        _queueRepository.GetOpenQueueEntriesForPatientAsync(1, Arg.Any<CancellationToken>()).Returns(new[] { old });
        _visitRepository.HasOpenVisitAsync(1, Arg.Any<CancellationToken>()).Returns(true); // Database still sees old Created Visit.
        _visitRepository.HasOtherOpenVisitAsync(1, Arg.Is<IReadOnlyCollection<int>>(ids => ids.SequenceEqual(new[] { 99 })), Arg.Any<CancellationToken>()).Returns(false);
        Assert.True((await CreateService().CheckInAsync(3, new(1), null)).IsSuccess);
        await _visitRepository.DidNotReceive().HasOpenVisitAsync(1, Arg.Any<CancellationToken>());
        await _queueRepository.Received(1).AddStatusHistoryAsync(Arg.Is<QueueStatusHistory>(h => h.QueueEntryId == 50 && h.FromStatus == status && h.ToStatus == QueueStatus.Cancelled), Arg.Any<CancellationToken>());
    }
    [Fact] public async Task CheckIn_InvalidAppointmentLeavesOldQueueAndVisitUntouched()
    {
        var old = new QueueEntry { QueueEntryId = 50, QueueDate = _vietnamClock.Today.AddDays(-1), Status = QueueStatus.Skipped,
            Visit = new Visit { VisitId = 99, Status = VisitStatuses.Created } };
        _patientRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(MakePatient());
        _queueRepository.GetOpenQueueEntriesForPatientAsync(1, Arg.Any<CancellationToken>()).Returns(new[] { old });
        Assert.True((await CreateService().CheckInAsync(3, new(1, AppointmentId: 999), null)).IsFailure);
        Assert.Equal(QueueStatus.Skipped, old.Status); Assert.Equal(VisitStatuses.Created, old.Visit!.Status);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>()); Assert.Empty(_auditLogger.ReceivedCalls());
    }
    [Fact] public async Task CheckIn_OtherOpenVisitPreventsAnyCleanup()
    {
        var old = new QueueEntry { QueueDate = _vietnamClock.Today.AddDays(-1), Status = QueueStatus.Waiting,
            Visit = new Visit { VisitId = 99, Status = VisitStatuses.Created } };
        _patientRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(MakePatient());
        _queueRepository.GetOpenQueueEntriesForPatientAsync(1, Arg.Any<CancellationToken>()).Returns(new[] { old });
        _visitRepository.HasOtherOpenVisitAsync(1, Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>()).Returns(true);
        Assert.Equal(Error.VisitAlreadyOpen, (await CreateService().CheckInAsync(3, new(1), null)).Error);
        Assert.Equal(QueueStatus.Waiting, old.Status); await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public async Task CheckIn_NeverCancelsOldStartedOrLockedVisits(bool started)
    {
        var old = new QueueEntry { QueueDate = _vietnamClock.Today.AddDays(-1), Status = QueueStatus.Calling,
            Visit = new Visit { Status = started ? VisitStatuses.InProgress : VisitStatuses.Created, IsLocked = !started } };
        _patientRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(MakePatient());
        _queueRepository.GetOpenQueueEntriesForPatientAsync(1, Arg.Any<CancellationToken>()).Returns(new[] { old });
        Assert.Equal(Error.QueueEntryAlreadyOpen, (await CreateService().CheckInAsync(3, new(1), null)).Error);
        Assert.Equal(QueueStatus.Calling, old.Status); Assert.Empty(_auditLogger.ReceivedCalls());
    }
    [Fact] public async Task CheckIn_At2330DoesNotCreateImmediatelyExpiredTicket()
    {
        var lateClock = new VietnamClock(new FixedTimeProvider(new DateTimeOffset(2026, 10, 10, 16, 30, 0, TimeSpan.Zero)));
        var service = new QueueService(_queueRepository, _appointmentRepository, _patientRepository, _visitRepository,
            _userRepository, _unitOfWork, _auditLogger, lateClock, _invoiceDraft, Dental.Tests.Common.TestVisitChairs.Create());
        Assert.Equal(Error.Validation.Code, (await service.CheckInAsync(3, new(1), null)).Error.Code);
        Assert.Empty(_queueRepository.ReceivedCalls());
    }

    [Fact]
    public async Task CheckInAsync_ConcurrentCheckIn_ReturnsReloadConflictWithoutRetryingPartialTransaction()
    {
        _unitOfWork.ExecuteSerializableTransactionAsync(Arg.Any<Func<CancellationToken, Task<Result<QueueEntryResponse>>>>(), Arg.Any<CancellationToken>())
            .Returns<Result<QueueEntryResponse>>(_ => throw new ConcurrentOperationException());
        var result = await CreateService().CheckInAsync(3, new CheckInRequest(1), null);
        Assert.Equal(Error.QueueConcurrent, result.Error);
        await _queueRepository.DidNotReceive().AddAsync(Arg.Any<QueueEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateQueueStatusAsync_ToInConsultation_RequiresDentistId_UpdatesVisitToInProgress()
    {
        // Arrange: Chuyển Waiting -> InConsultation phải có DentistId (DL-013)
        var visit = new Visit { VisitId = 10, Status = VisitStatuses.Created };
        var entry = new QueueEntry
        {
            QueueEntryId = 15,
            QueueDate = _vietnamClock.Today,
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
            QueueDate = _vietnamClock.Today,
            Visit = new Visit { DentistId = 5, Status = VisitStatuses.InProgress },
            Status = QueueStatus.InImaging
        };

        _queueRepository.GetByIdAsync(20, Arg.Any<CancellationToken>()).Returns(entry);
        _userRepository.FindByIdAsync(5, Arg.Any<CancellationToken>()).Returns(new User { IsActive = true, Role = new Role { RoleCode = RoleCodes.Dentist } });

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
