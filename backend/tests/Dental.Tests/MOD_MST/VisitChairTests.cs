using Dental.Application.Common;
using Dental.Application.Features.MOD_MST.Services;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using NSubstitute;
using Xunit;
namespace Dental.Tests.MOD_MST;
public sealed class VisitChairTests
{
    private readonly IStaffWorkspaceRepository workspaces = Substitute.For<IStaffWorkspaceRepository>();
    private readonly IDentalChairRepository chairs = Substitute.For<IDentalChairRepository>();
    private readonly IVisitRepository visits = Substitute.For<IVisitRepository>();
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly DentalChair chair = new() { Id = 3, RoomId = 10, Status = DentalChairStatus.Available, IsActive = true };
    private readonly DentalChair targetChair = new() { Id = 4, RoomId = 11, Status = DentalChairStatus.Available, IsActive = true };
    private readonly Visit visit = new() { VisitId = 7, Status = VisitStatuses.Created };
    private readonly VietnamClock clock = new(TimeProvider.System);
    private VisitChairService Service() => new(workspaces, chairs, visits, users, clock);
    public VisitChairTests()
    {
        users.FindByIdAsync(2, Arg.Any<CancellationToken>()).Returns(new User { UserId = 2, IsActive = true, Role = new() { RoleCode = RoleCodes.Dentist } });
        workspaces.FindAsync(2, Arg.Any<CancellationToken>()).Returns(new StaffWorkspace { UserId = 2, ChairId = 3, WorkDate = clock.Today });
        chairs.FindAsync(3, Arg.Any<CancellationToken>()).Returns(chair);
        users.FindByIdAsync(6, Arg.Any<CancellationToken>()).Returns(new User { UserId = 6, IsActive = true, Role = new() { RoleCode = RoleCodes.Dentist } });
        workspaces.FindAsync(6, Arg.Any<CancellationToken>()).Returns(new StaffWorkspace { UserId = 6, ChairId = 4, WorkDate = clock.Today });
        chairs.FindAsync(4, Arg.Any<CancellationToken>()).Returns(targetChair);
    }
    [Fact] public async Task StartClaimsCurrentDentistChairAndAuditableVersion()
    {
        Assert.True((await Service().AcquireAsync(visit, 2, 99)).IsSuccess);
        Assert.Equal(3, visit.ChairId); Assert.Equal(10, visit.RoomId); Assert.Equal(DentalChairStatus.Occupied, chair.Status);
        Assert.Equal(2, chair.Version); Assert.Equal(99, chair.UpdatedByUserId); Assert.NotNull(chair.UpdatedAt);
    }
    [Fact] public async Task CallingChecksReadinessWithoutOccupyingOrAssigningVisitChair()
    {
        Assert.True((await Service().CheckReadyAsync(visit, 2)).IsSuccess);
        Assert.Null(visit.ChairId); Assert.Equal(DentalChairStatus.Available, chair.Status); Assert.Equal(1, chair.Version);
    }
    [Theory] [InlineData(DentalChairStatus.Occupied)] [InlineData(DentalChairStatus.Maintenance)] [InlineData(DentalChairStatus.Inactive)]
    public async Task StartRejectsUnavailableChairWithoutMutation(DentalChairStatus status)
    {
        chair.Status = status;
        Assert.Equal(VisitChairService.Unavailable, (await Service().AcquireAsync(visit, 2, 99)).Error);
        Assert.Null(visit.ChairId); Assert.Equal(1, chair.Version);
    }
    [Fact] public async Task StartRequiresWorkspace()
    {
        workspaces.FindAsync(2, Arg.Any<CancellationToken>()).Returns((StaffWorkspace?)null);
        Assert.Equal(VisitChairService.Unavailable, (await Service().AcquireAsync(visit, 2, 99)).Error);
    }
    [Theory] [InlineData(false, RoleCodes.Dentist)] [InlineData(true, RoleCodes.Assistant)]
    public async Task StartRequiresActiveDentistRole(bool active, string role)
    {
        users.FindByIdAsync(2, Arg.Any<CancellationToken>()).Returns(new User { UserId = 2, IsActive = active, Role = new() { RoleCode = role } });
        Assert.Equal(VisitChairService.Unavailable, (await Service().AcquireAsync(visit, 2, 99)).Error);
        Assert.Null(visit.ChairId); Assert.Equal(1, chair.Version);
    }
    [Fact] public async Task OtherActiveVisitBlocksEvenIfChairStatusIsStaleAvailable()
    {
        visits.HasActiveVisitForChairAsync(3, 7, Arg.Any<CancellationToken>()).Returns(true);
        Assert.Equal(VisitChairService.Unavailable, (await Service().AcquireAsync(visit, 2, 99)).Error); Assert.Null(visit.ChairId);
    }
    [Fact] public async Task DirectCoordinatorAlsoEnforcesOneActiveVisitPerDentist()
    {
        visits.HasActiveVisitForDentistAsync(2, Arg.Any<CancellationToken>(), 7).Returns(true);
        Assert.Equal(Error.DentistHasActiveVisit, (await Service().AcquireAsync(visit, 2, 99)).Error);
        Assert.Null(visit.ChairId); Assert.Equal(1, chair.Version);
    }
    [Theory] [InlineData(VisitStatuses.Correcting)] [InlineData(VisitStatuses.Completed)] [InlineData(VisitStatuses.Cancelled)]
    public async Task OnlyPhysicalOpenVisitCanAcquire(string status)
    {
        visit.Status = status;
        Assert.True((await Service().AcquireAsync(visit, 2, 99)).IsFailure); Assert.Equal(DentalChairStatus.Available, chair.Status);
    }
    [Fact] public async Task ImagingReturnKeepsSameOccupiedChairWithoutSecondClaim()
    {
        visit.Status = VisitStatuses.InProgress; visit.ChairId = 3; chair.Status = DentalChairStatus.Occupied;
        Assert.True((await Service().AcquireAsync(visit, 2, 99)).IsSuccess); Assert.Equal(1, chair.Version);
    }
    [Fact] public async Task CannotSilentlyMoveVisitToAnotherChairOnImagingReturn()
    {
        visit.Status = VisitStatuses.InProgress; visit.ChairId = 4; chair.Status = DentalChairStatus.Occupied;
        Assert.Equal(VisitChairService.Inconsistent, (await Service().AcquireAsync(visit, 2, 99)).Error);
        Assert.Equal(4, visit.ChairId); Assert.Equal(1, chair.Version);
    }
    [Fact] public async Task PhysicalFinishReleasesChairButPreservesVisitReference()
    {
        visit.Status = VisitStatuses.InProgress; visit.ChairId = 3; chair.Status = DentalChairStatus.Occupied;
        Assert.True((await Service().ReleaseAsync(visit, 99)).IsSuccess);
        Assert.Equal(DentalChairStatus.Available, chair.Status); Assert.Equal(3, visit.ChairId); Assert.Equal(2, chair.Version);
    }
    [Fact] public async Task CorrectionNeverReleasesHistoricalChairNowUsedBySomeoneElse()
    {
        visit.Status = VisitStatuses.Correcting; visit.ChairId = 3; chair.Status = DentalChairStatus.Occupied;
        Assert.True((await Service().ReleaseAsync(visit, 99)).IsSuccess); Assert.Equal(DentalChairStatus.Occupied, chair.Status);
        Assert.Empty(chairs.ReceivedCalls()); Assert.Equal(1, chair.Version);
    }
    [Fact] public async Task LegacyVisitWithoutChairDoesNotInventOrReleaseTodaysWorkspace()
    {
        visit.Status = VisitStatuses.InProgress;
        Assert.True((await Service().ReleaseAsync(visit, 99)).IsSuccess); Assert.Empty(chairs.ReceivedCalls());
    }
    [Fact] public async Task ReleaseRefusesChairOwnedByAnotherActiveVisit()
    {
        visit.Status = VisitStatuses.InProgress; visit.ChairId = 3; chair.Status = DentalChairStatus.Occupied;
        visits.HasActiveVisitForChairAsync(3, 7, Arg.Any<CancellationToken>()).Returns(true);
        Assert.Equal(VisitChairService.Inconsistent, (await Service().ReleaseAsync(visit, 99)).Error); Assert.Equal(1, chair.Version);
    }
    [Fact] public async Task TransferMovesVisitAndBothChairStatesWithAuditableVersions()
    {
        visit.Status = VisitStatuses.InProgress; visit.ChairId = 3; chair.Status = DentalChairStatus.Occupied;

        Assert.True((await Service().TransferAsync(visit, 6, 99)).IsSuccess);

        Assert.Equal(4, visit.ChairId);
        Assert.Equal(DentalChairStatus.Available, chair.Status); Assert.Equal(2, chair.Version); Assert.Equal(99, chair.UpdatedByUserId);
        Assert.Equal(DentalChairStatus.Occupied, targetChair.Status); Assert.Equal(2, targetChair.Version); Assert.Equal(99, targetChair.UpdatedByUserId);
        Assert.NotNull(chair.UpdatedAt); Assert.NotNull(targetChair.UpdatedAt);
    }
    [Theory]
    [InlineData(DentalChairStatus.Occupied)] [InlineData(DentalChairStatus.Maintenance)] [InlineData(DentalChairStatus.Inactive)]
    public async Task TransferToUnavailableChairDoesNotMutateSourceOrVisit(DentalChairStatus targetStatus)
    {
        visit.Status = VisitStatuses.InProgress; visit.ChairId = 3; chair.Status = DentalChairStatus.Occupied; targetChair.Status = targetStatus;

        Assert.Equal(VisitChairService.Unavailable, (await Service().TransferAsync(visit, 6, 99)).Error);

        Assert.Equal(3, visit.ChairId); Assert.Equal(DentalChairStatus.Occupied, chair.Status); Assert.Equal(1, chair.Version);
        Assert.Equal(targetStatus, targetChair.Status); Assert.Equal(1, targetChair.Version);
    }
    [Fact] public async Task TransferRequiresTargetWorkspaceAndPreservesSourceOnMissingWorkspace()
    {
        visit.Status = VisitStatuses.InProgress; visit.ChairId = 3; chair.Status = DentalChairStatus.Occupied;
        workspaces.FindAsync(6, Arg.Any<CancellationToken>()).Returns((StaffWorkspace?)null);

        Assert.Equal(VisitChairService.Unavailable, (await Service().TransferAsync(visit, 6, 99)).Error);

        Assert.Equal(3, visit.ChairId); Assert.Equal(DentalChairStatus.Occupied, chair.Status); Assert.Equal(DentalChairStatus.Available, targetChair.Status);
    }
    [Fact] public async Task TransferRefusesInconsistentSourceWithoutOccupyingTarget()
    {
        visit.Status = VisitStatuses.InProgress; visit.ChairId = 3; chair.Status = DentalChairStatus.Available;

        Assert.Equal(VisitChairService.Inconsistent, (await Service().TransferAsync(visit, 6, 99)).Error);

        Assert.Equal(3, visit.ChairId); Assert.Equal(DentalChairStatus.Available, chair.Status); Assert.Equal(DentalChairStatus.Available, targetChair.Status);
    }
    [Theory] [InlineData(RoleCodes.Admin, true)] [InlineData(RoleCodes.Dentist, true)]
    [InlineData(RoleCodes.Assistant, false)] [InlineData(RoleCodes.Receptionist, false)]
    public void ImagingCancellationRoleTableMatchesDl057(string role, bool allowed)
        => Assert.Equal(allowed, Dental.Application.Features.Queue.Services.QueueStateMachine
            .ValidateTransition(QueueStatus.InImaging, QueueStatus.Cancelled, role).IsSuccess);
    [Fact] public async Task UnconfirmedYesterdayWorkspaceBlocksAcquireReadyAndTransferButAllowsOvernightRelease()
    {
        workspaces.FindAsync(2, Arg.Any<CancellationToken>()).Returns(new StaffWorkspace { UserId = 2, ChairId = 3, WorkDate = clock.Today.AddDays(-1) });
        Assert.Equal(VisitChairService.Unavailable, (await Service().CheckReadyAsync(visit, 2)).Error);
        Assert.Equal(VisitChairService.Unavailable, (await Service().AcquireAsync(visit, 2, 99)).Error);

        visit.Status = VisitStatuses.InProgress; visit.ChairId = 3; chair.Status = DentalChairStatus.Occupied;
        workspaces.FindAsync(6, Arg.Any<CancellationToken>()).Returns(new StaffWorkspace { UserId = 6, ChairId = 4, WorkDate = clock.Today.AddDays(-1) });
        Assert.Equal(VisitChairService.Unavailable, (await Service().TransferAsync(visit, 6, 99)).Error);
        Assert.Equal(3, visit.ChairId); Assert.Equal(DentalChairStatus.Occupied, chair.Status);

        Assert.True((await Service().ReleaseAsync(visit, 99)).IsSuccess);
        Assert.Equal(DentalChairStatus.Available, chair.Status);
    }
    [Fact] public async Task ActiveVisitInSameRoomBlocksAcquireEvenIfChairIdDiffers()
    {
        visits.HasActiveVisitForRoomAsync(10, 7, Arg.Any<CancellationToken>()).Returns(true);
        Assert.Equal(VisitChairService.Unavailable, (await Service().AcquireAsync(visit, 2, 99)).Error);
        Assert.Null(visit.ChairId); Assert.Null(visit.RoomId);
        Assert.Equal(DentalChairStatus.Available, chair.Status);
    }
    [Fact] public async Task ActiveVisitInTargetRoomBlocksTransfer()
    {
        visit.Status = VisitStatuses.InProgress; visit.ChairId = 3; visit.RoomId = 10; chair.Status = DentalChairStatus.Occupied;
        visits.HasActiveVisitForRoomAsync(11, 7, Arg.Any<CancellationToken>()).Returns(true);
        Assert.Equal(VisitChairService.Unavailable, (await Service().TransferAsync(visit, 6, 99)).Error);
        Assert.Equal(3, visit.ChairId); Assert.Equal(10, visit.RoomId);
        Assert.Equal(DentalChairStatus.Occupied, chair.Status);
        Assert.Equal(DentalChairStatus.Available, targetChair.Status);
    }
}
