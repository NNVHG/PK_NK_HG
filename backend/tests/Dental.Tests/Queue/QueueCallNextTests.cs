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

public sealed class QueueCallNextTests
{
    private readonly IQueueRepository _queueRepository = Substitute.For<IQueueRepository>();
    private readonly IAppointmentRepository _appointmentRepository = Substitute.For<IAppointmentRepository>();
    private readonly IPatientRepository _patientRepository = Substitute.For<IPatientRepository>();
    private readonly IVisitRepository _visitRepository = Substitute.For<IVisitRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();
    private readonly IInvoiceDraftGenerator _invoiceDraft = Substitute.For<IInvoiceDraftGenerator>();
    private readonly VietnamClock _vietnamClock;

    public QueueCallNextTests()
    {
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 10, 10, 2, 0, 0, TimeSpan.Zero));
        _vietnamClock = new VietnamClock(timeProvider);
    }

    private QueueService CreateService()
        => new(
            _queueRepository,
            _appointmentRepository,
            _patientRepository,
            _visitRepository,
            _userRepository,
            _unitOfWork,
            _auditLogger,
            _vietnamClock,
            _invoiceDraft);

    [Theory]
    [InlineData(RoleCodes.Receptionist)]
    [InlineData(RoleCodes.Assistant)]
    [InlineData(RoleCodes.Patient)]
    public async Task CallNextAsync_UnauthorizedRoles_ReturnsForbidden(string role)
    {
        var result = await CreateService().CallNextAsync(
            actorUserId: 10,
            actorRole: role,
            new CallNextRequest(null),
            null);

        Assert.True(result.IsFailure);
        Assert.Equal(Error.QueueTransitionForbidden.Code, result.Error.Code);
    }

    [Fact]
    public async Task CallNextAsync_DentistRole_HasActiveVisit_ReturnsDentistHasActiveVisit()
    {
        _visitRepository.HasActiveVisitForDentistAsync(5, Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await CreateService().CallNextAsync(
            actorUserId: 5,
            actorRole: RoleCodes.Dentist,
            new CallNextRequest(null),
            null);

        Assert.True(result.IsFailure);
        Assert.Equal(Error.DentistHasActiveVisit.Code, result.Error.Code);
    }

    [Fact]
    public async Task CallNextAsync_DentistRole_NoWaitingPatient_ReturnsNoWaitingPatients()
    {
        _visitRepository.HasActiveVisitForDentistAsync(5, Arg.Any<CancellationToken>())
            .Returns(false);
        _queueRepository.GetNextWaitingEntryAsync(_vietnamClock.Today, 5, Arg.Any<CancellationToken>())
            .Returns((QueueEntry?)null);

        var result = await CreateService().CallNextAsync(
            actorUserId: 5,
            actorRole: RoleCodes.Dentist,
            new CallNextRequest(null),
            null);

        Assert.True(result.IsFailure);
        Assert.Equal(Error.QueueNoWaitingPatients.Code, result.Error.Code);
    }
    [Fact]
    public async Task CallNextAsync_DentistRole_ValidWaitingPatient_TransitionsAndSetsDentist()
    {
        _visitRepository.HasActiveVisitForDentistAsync(5, Arg.Any<CancellationToken>())
            .Returns(false);

        var visit = new Visit { VisitId = 10, Status = VisitStatuses.Created };
        var entry = new QueueEntry
        {
            QueueEntryId = 1,
            QueueNumber = 5,
            QueueDate = _vietnamClock.Today,
            Status = QueueStatus.Waiting,
            Visit = visit,
            Patient = new Patient { FullName = "Nguyễn Văn A", Phone = "0901234567" }
        };

        _queueRepository.GetNextWaitingEntryAsync(_vietnamClock.Today, 5, Arg.Any<CancellationToken>())
            .Returns(entry);
        _queueRepository.GetByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(entry);

        var result = await CreateService().CallNextAsync(
            actorUserId: 5,
            actorRole: RoleCodes.Dentist,
            new CallNextRequest(null),
            "127.0.0.1");

        Assert.True(result.IsSuccess);
        Assert.Equal(QueueStatus.InConsultation, entry.Status);
        Assert.Equal(5, entry.DentistId);
        Assert.Equal(VisitStatuses.InProgress, visit.Status);
        Assert.Equal(5, visit.DentistId);
        Assert.NotNull(visit.StartedAt);

        await _queueRepository.Received(1).AddStatusHistoryAsync(
            Arg.Is<QueueStatusHistory>(h =>
                h.QueueEntryId == 1 &&
                h.FromStatus == QueueStatus.Waiting &&
                h.ToStatus == QueueStatus.InConsultation &&
                h.ChangedByUserId == 5),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CallNextAsync_AdminRole_WithoutDentistId_ReturnsDentistRequired()
    {
        var result = await CreateService().CallNextAsync(
            actorUserId: 1,
            actorRole: RoleCodes.Admin,
            new CallNextRequest(null),
            null);

        Assert.True(result.IsFailure);
        Assert.Equal(Error.QueueDentistRequired.Code, result.Error.Code);
    }

    [Fact]
    public async Task CallNextAsync_AdminRole_WithInvalidDentistId_ReturnsValidationError()
    {
        _userRepository.FindByIdAsync(99, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var result = await CreateService().CallNextAsync(
            actorUserId: 1,
            actorRole: RoleCodes.Admin,
            new CallNextRequest(99),
            null);

        Assert.True(result.IsFailure);
        Assert.Equal(Error.Validation.Code, result.Error.Code);
    }

    [Fact]
    public async Task CallNextAsync_AdminRole_WithValidDentistId_Succeeds()
    {
        var dentist = new User
        {
            UserId = 5,
            Role = new Role { RoleCode = RoleCodes.Dentist },
            IsActive = true
        };
        _userRepository.FindByIdAsync(5, Arg.Any<CancellationToken>()).Returns(dentist);
        _visitRepository.HasActiveVisitForDentistAsync(5, Arg.Any<CancellationToken>()).Returns(false);

        var visit = new Visit { VisitId = 20, Status = VisitStatuses.Created };
        var entry = new QueueEntry
        {
            QueueEntryId = 2,
            QueueNumber = 7,
            QueueDate = _vietnamClock.Today,
            Status = QueueStatus.Waiting,
            Visit = visit
        };
        _queueRepository.GetNextWaitingEntryAsync(_vietnamClock.Today, 5, Arg.Any<CancellationToken>()).Returns(entry);
        _queueRepository.GetByIdAsync(2, Arg.Any<CancellationToken>()).Returns(entry);

        var result = await CreateService().CallNextAsync(
            actorUserId: 1,
            actorRole: RoleCodes.Admin,
            new CallNextRequest(5),
            null);

        Assert.True(result.IsSuccess);
        Assert.Equal(QueueStatus.InConsultation, entry.Status);
        Assert.Equal(5, entry.DentistId);
        Assert.Equal(VisitStatuses.InProgress, visit.Status);
    }

    [Fact]
    public async Task GetPublicQueueDisplayAsync_ReturnsMaskedNamesAndSeparatesStatuses()
    {
        var entries = new List<QueueEntry>
        {
            new()
            {
                QueueEntryId = 1,
                QueueNumber = 1,
                Status = QueueStatus.InConsultation,
                Patient = new Patient { FullName = "Nguyễn Văn An" },
                Dentist = new User { FullName = "BS. Lê Văn B" },
                IsPriority = false
            },
            new()
            {
                QueueEntryId = 2,
                QueueNumber = 2,
                Status = QueueStatus.Waiting,
                Patient = new Patient { FullName = "Trần Thị Bé" },
                IsPriority = true
            }
        };

        _queueRepository.GetPublicDisplayQueueAsync(_vietnamClock.Today, Arg.Any<CancellationToken>())
            .Returns(entries);

        var result = await CreateService().GetPublicQueueDisplayAsync();

        Assert.True(result.IsSuccess);
        var monitor = result.Value!;

        Assert.Single(monitor.InConsultation);
        Assert.Equal("A-001", monitor.InConsultation[0].QueueNumberDisplay);
        Assert.Equal("Nguyễn V*** An", monitor.InConsultation[0].MaskedPatientName);
        Assert.Equal("BS. Lê Văn B", monitor.InConsultation[0].DentistName);

        Assert.Single(monitor.Waiting);
        Assert.Equal("A-002", monitor.Waiting[0].QueueNumberDisplay);
        Assert.Equal("Trần T*** Bé", monitor.Waiting[0].MaskedPatientName);
        Assert.True(monitor.Waiting[0].IsPriority);
    }

    [Theory]
    [InlineData("Nguyễn Văn An", "Nguyễn V*** An")]
    [InlineData("Trần Thị Bé", "Trần T*** Bé")]
    [InlineData("Lê An", "Lê *** An")]
    [InlineData("An", "An")]
    [InlineData("Huy", "H***y")]
    [InlineData("", "Bệnh nhân")]
    [InlineData(null, "Bệnh nhân")]
    public void MaskPatientName_Tests(string? input, string expected)
    {
        var actual = QueueService.MaskPatientName(input);
        Assert.Equal(expected, actual);
    }

}
