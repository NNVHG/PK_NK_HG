using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_ASS.DTOs;
using Dental.Application.Features.MOD_ASS.Services;
using Dental.Application.Features.MOD_ASS.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Tests.Common;
using NSubstitute;
using Xunit;

namespace Dental.Tests.MOD_ASS;

public sealed class ChairStatusTests
{
    private readonly IChairStatusRepository chairs = Substitute.For<IChairStatusRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogger audit = Substitute.For<IAuditLogger>();
    private readonly VietnamClock clock = new(new FixedTimeProvider(new DateTimeOffset(2026, 10, 19, 2, 0, 0, TimeSpan.Zero)));
    private readonly DentalChair chair = new() { Id = 7, ChairNumber = "GHE-01", RoomName = "Phòng 1", Description = "Keep", Version = 1, CreatedByUserId = 50 };
    private ChairStatusService Service() => new(chairs, uow, audit, clock);
    public ChairStatusTests()
    {
        chairs.FindAsync(7, Arg.Any<CancellationToken>()).Returns(chair);
        chairs.ActorAllowedAsync(99, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        chairs.GetPageAsync(Arg.Any<ChairStatusQuery>(), Arg.Any<CancellationToken>())
            .Returns(PagedResult<ChairStatusResponse>.Create([ChairStatusResponse.From(chair)], 1, 1, 20));
        uow.ExecuteSerializableTransactionAsync(Arg.Any<Func<CancellationToken, Task<Result<ChairStatusResponse>>>>(), Arg.Any<CancellationToken>())
            .Returns(c => c.Arg<Func<CancellationToken, Task<Result<ChairStatusResponse>>>>()(CancellationToken.None));
    }
    [Theory]
    [InlineData(RoleCodes.Dentist)] [InlineData(RoleCodes.Receptionist)] [InlineData(RoleCodes.Patient)] [InlineData(null)]
    public async Task OtherRolesCannotReadOrWrite(string? role)
    {
        Assert.Equal(Error.Forbidden, (await Service().GetPageAsync(99, role, new())).Error);
        Assert.Equal(Error.Forbidden, (await Service().ChangeAsync(7, 99, role, new(3, 1, true))).Error);
        Assert.Empty(chairs.ReceivedCalls()); Assert.Empty(uow.ReceivedCalls());
    }
    [Theory] [InlineData(RoleCodes.Admin)] [InlineData(RoleCodes.Assistant)]
    public async Task BothRolesReadActualPageWithoutWrites(string role)
    {
        var result = await Service().GetPageAsync(99, role, new());
        Assert.True(result.IsSuccess); Assert.NotNull(result.Value); Assert.Equal(7, result.Value.Items.Single().Id);
        Assert.Empty(uow.ReceivedCalls()); Assert.Empty(audit.ReceivedCalls());
    }
    [Theory] [InlineData(RoleCodes.Admin, 3)] [InlineData(RoleCodes.Assistant, 3)] [InlineData(RoleCodes.Assistant, 4)] [InlineData(RoleCodes.Admin, 1)]
    public async Task ChangeOnlyStatusAndMetadataWithRequiredAudit(string role, int status)
    {
        chair.Status = DentalChairStatus.Maintenance; chair.IsActive = true;
        if (status == 3) chair.Status = DentalChairStatus.Available;
        var previous = (int)chair.Status;
        var result = await Service().ChangeAsync(7, 99, role, new(status, 1, true));
        Assert.True(result.IsSuccess); Assert.NotNull(result.Value); Assert.Equal(status, result.Value.Status); Assert.Equal(status != 4, chair.IsActive);
        Assert.Equal(2, chair.Version); Assert.Equal(99, chair.UpdatedByUserId); Assert.Equal(clock.UtcNow, chair.UpdatedAt);
        Assert.Equal("GHE-01", chair.ChairNumber); Assert.Equal("Phòng 1", chair.RoomName); Assert.Equal("Keep", chair.Description); Assert.Equal(50, chair.CreatedByUserId);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await audit.Received(1).LogRequiredAsync("MOD_ASS_CHAIR_STATUS_CHANGED", "DentalChair", 7, 99,
            Arg.Is<string>(s => AuditMatches(s, previous, status)), null, Arg.Any<CancellationToken>());
    }
    private static bool AuditMatches(string json, int previous, int status)
    { using var d = JsonDocument.Parse(json); return d.RootElement.GetProperty("previousStatus").GetInt32() == previous && d.RootElement.GetProperty("status").GetInt32() == status; }
    [Theory] [InlineData(1)] [InlineData(3)] [InlineData(4)]
    public async Task WorkspaceAssignmentBlocksEveryManualTarget(int status)
    {
        chairs.IsAssignedAsync(7, Arg.Any<CancellationToken>()).Returns(true);
        Assert.Equal(ChairStatusService.Assigned, (await Service().ChangeAsync(7, 99, RoleCodes.Assistant, new(status, 1, true))).Error);
        Assert.Equal(DentalChairStatus.Available, chair.Status); Assert.Equal(1, chair.Version); Assert.Empty(audit.ReceivedCalls());
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public async Task OccupiedOrActualInProgressBlocksManualRelease(bool statusFlag)
    {
        if (statusFlag) chair.Status = DentalChairStatus.Occupied;
        else chairs.HasActiveVisitAsync(7, Arg.Any<CancellationToken>()).Returns(true);
        Assert.Equal(ChairStatusService.Occupied, (await Service().ChangeAsync(7, 99, RoleCodes.Admin, new(3, 1, true))).Error);
        Assert.Equal(1, chair.Version); Assert.Empty(audit.ReceivedCalls());
    }
    [Theory] [InlineData(0)] [InlineData(2)] [InlineData(5)] [InlineData(-1)]
    public async Task ManualOccupiedAndUnknownTargetsRejectedBeforeRepository(int status)
    {
        Assert.Equal(Error.Validation, (await Service().ChangeAsync(7, 99, RoleCodes.Admin, new(status, 1, true))).Error);
        Assert.Empty(chairs.ReceivedCalls());
    }
    [Theory] [InlineData(0, true)] [InlineData(-1, true)] [InlineData(1, false)]
    public async Task VersionAndConfirmationAreRequired(int version, bool confirmed)
    {
        Assert.False(new ChangeChairStatusRequestValidator().Validate(new ChangeChairStatusRequest(3, version, confirmed)).IsValid);
        Assert.Equal(Error.Validation, (await Service().ChangeAsync(7, 99, RoleCodes.Admin, new(3, version, confirmed))).Error);
        Assert.Empty(chairs.ReceivedCalls());
    }
    [Theory] [InlineData(0)] [InlineData(-1)]
    public async Task InvalidChairIdCannotWrite(int id)
    {
        Assert.Equal(Error.Validation, (await Service().ChangeAsync(id, 99, RoleCodes.Admin, new(3, 1, true))).Error);
        Assert.Empty(chairs.ReceivedCalls());
    }
    [Fact] public async Task UnknownChairAndStaleVersionNeverWrite()
    {
        Assert.Equal(Error.NotFound, (await Service().ChangeAsync(8, 99, RoleCodes.Admin, new(3, 1, true))).Error);
        Assert.Equal(ChairStatusService.Changed, (await Service().ChangeAsync(7, 99, RoleCodes.Admin, new(3, 2, true))).Error);
        chair.Version = int.MaxValue;
        Assert.Equal(ChairStatusService.Changed, (await Service().ChangeAsync(7, 99, RoleCodes.Admin, new(3, int.MaxValue, true))).Error);
        Assert.Equal(DentalChairStatus.Available, chair.Status); Assert.Empty(audit.ReceivedCalls());
    }
    [Fact] public async Task NoOpDoesNotCreateFalseAuditOrIncrementVersion()
    {
        Assert.True((await Service().ChangeAsync(7, 99, RoleCodes.Admin, new(1, 1, true))).IsSuccess);
        Assert.Equal(1, chair.Version); await uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>()); Assert.Empty(audit.ReceivedCalls());
    }
    [Fact] public async Task InactiveChairCanBeMadeReadyWithConsistentFlag()
    {
        chair.Status = DentalChairStatus.Inactive; chair.IsActive = false;
        Assert.True((await Service().ChangeAsync(7, 99, RoleCodes.Assistant, new(1, 1, true))).IsSuccess);
        Assert.True(chair.IsActive); Assert.Equal(2, chair.Version);
    }
    [Fact] public async Task InactiveOrChangedDatabaseActorCannotReadOrWrite()
    {
        chairs.ActorAllowedAsync(99, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        Assert.Equal(Error.Forbidden, (await Service().GetPageAsync(99, RoleCodes.Assistant, new())).Error);
        Assert.Equal(Error.Forbidden, (await Service().ChangeAsync(7, 99, RoleCodes.Assistant, new(3, 1, true))).Error);
        await chairs.DidNotReceive().FindAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()); Assert.Empty(audit.ReceivedCalls());
    }
    [Fact] public async Task InvalidQueriesNeverReadChairList()
    {
        foreach (var q in new[] { new ChairStatusQuery(Page: 0), new(Page: 1_000_001), new(PageSize: 0), new(PageSize: 101), new(Status: 5), new(Keyword: new string('x', 101)) })
            Assert.Equal(Error.Validation, (await Service().GetPageAsync(99, RoleCodes.Admin, q)).Error);
        await chairs.DidNotReceive().GetPageAsync(Arg.Any<ChairStatusQuery>(), Arg.Any<CancellationToken>());
    }
    [Fact] public async Task SerializableConflictReturnsReloadError()
    {
        uow.ExecuteSerializableTransactionAsync(Arg.Any<Func<CancellationToken, Task<Result<ChairStatusResponse>>>>(), Arg.Any<CancellationToken>())
            .Returns<Task<Result<ChairStatusResponse>>>(_ => throw new ConcurrentOperationException());
        Assert.Equal(ChairStatusService.Changed, (await Service().ChangeAsync(7, 99, RoleCodes.Admin, new(3, 1, true))).Error);
    }
    [Fact] public async Task RequiredAuditFailurePropagatesForRollback()
    {
        audit.LogRequiredAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("Synthetic required audit failure"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service().ChangeAsync(7, 99, RoleCodes.Admin, new(3, 1, true)));
    }
}
