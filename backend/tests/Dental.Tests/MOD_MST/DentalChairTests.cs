using Dental.Application.Common;
using Dental.Application.Features.MOD_MST.DTOs;
using Dental.Application.Features.MOD_MST.Services;
using Dental.Application.Features.MOD_MST.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Tests.Common;
using NSubstitute;
using Xunit;

namespace Dental.Tests.MOD_MST;

public sealed class DentalChairTests
{
    private readonly IDentalChairRepository chairs = Substitute.For<IDentalChairRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogger audit = Substitute.For<IAuditLogger>();
    private readonly VietnamClock clock = new(new FixedTimeProvider(new DateTimeOffset(2026, 10, 16, 2, 0, 0, TimeSpan.Zero)));
    private readonly DentalChair chair = new() { Id = 7, ChairNumber = "GHE-01", RoomName = "Phòng 1", Version = 1 };
    private DentalChairService Service() => new(chairs, uow, audit, clock);
    public DentalChairTests()
    {
        chairs.FindAsync(7, Arg.Any<CancellationToken>()).Returns(chair);
        chairs.GetPageAsync(Arg.Any<DentalChairQuery>(), Arg.Any<CancellationToken>()).Returns(PagedResult<DentalChair>.Create(new[] { chair }, 1, 1, 20));
        uow.ExecuteSerializableTransactionAsync(Arg.Any<Func<CancellationToken, Task<Result<DentalChairResponse>>>>(), Arg.Any<CancellationToken>())
            .Returns(c => c.Arg<Func<CancellationToken, Task<Result<DentalChairResponse>>>>()(CancellationToken.None));
    }
    [Theory] [InlineData(RoleCodes.Dentist)] [InlineData(RoleCodes.Assistant)] [InlineData(RoleCodes.Receptionist)] [InlineData(RoleCodes.Patient)] [InlineData(null)]
    public async Task OnlyAdminReadsAndWritesCatalog(string? role)
    {
        if (role is not (RoleCodes.Dentist or RoleCodes.Assistant)) Assert.Equal(Error.Forbidden, (await Service().GetPageAsync(role, new())).Error);
        Assert.Equal(Error.Forbidden, (await Service().CreateAsync(99, role, new("GHE-02", "Phòng 2", 1, null, true))).Error);
        Assert.Equal(Error.Forbidden, (await Service().UpdateAsync(7, 99, role, new("Phòng 2", 1, null, 1, true))).Error);
        Assert.Empty(chairs.ReceivedCalls()); Assert.Empty(uow.ReceivedCalls());
    }
    [Theory] [InlineData(1, true)] [InlineData(3, true)] [InlineData(4, false)]
    public async Task CreateNormalizesNumberAndRecordsAudit(int status, bool active)
    {
        DentalChair? saved = null; await chairs.AddAsync(Arg.Do<DentalChair>(c => saved = c), Arg.Any<CancellationToken>());
        var result = await Service().CreateAsync(99, RoleCodes.Admin, new(" ghe-02 ", " Phòng 2 ", status, " note ", true));
        Assert.True(result.IsSuccess); Assert.Equal("GHE-02", saved!.ChairNumber); Assert.Equal("Phòng 2", saved.RoomName);
        Assert.Equal(active, saved.IsActive); Assert.Equal(99, saved.CreatedByUserId); Assert.Equal("note", saved.Description); Assert.Equal(1, saved.Version);
        await audit.Received(1).LogRequiredAsync("MOD_MST_CHAIR_CREATED", "DentalChair", saved.Id, 99, Arg.Any<string>(), null, Arg.Any<CancellationToken>());
    }
    [Theory] [InlineData(RoleCodes.Admin)] [InlineData(RoleCodes.Dentist)] [InlineData(RoleCodes.Assistant)]
    public async Task CatalogReadRolesFollowMatrix(string role)
    { Assert.True((await Service().GetPageAsync(role, new())).IsSuccess); Assert.Empty(uow.ReceivedCalls()); Assert.Empty(audit.ReceivedCalls()); }
    [Fact] public async Task DuplicateNumberNeverWrites()
    {
        chairs.NumberExistsAsync("GHE-02", Arg.Any<CancellationToken>()).Returns(true);
        Assert.Equal(DentalChairService.DuplicateNumber, (await Service().CreateAsync(99, RoleCodes.Admin, new("ghe-02", "P", 1, null, true))).Error);
        await chairs.DidNotReceive().AddAsync(Arg.Any<DentalChair>(), Arg.Any<CancellationToken>()); Assert.Empty(audit.ReceivedCalls());
    }
    [Theory] [InlineData(1)] [InlineData(3)] [InlineData(4)]
    public async Task UpdateKeepsCodeAndIncrementsVersionWithAudit(int status)
    {
        var result = await Service().UpdateAsync(7, 99, RoleCodes.Admin, new(" Phòng mới ", status, " ", 1, true));
        Assert.True(result.IsSuccess); Assert.Equal("GHE-01", chair.ChairNumber); Assert.Equal("Phòng mới", chair.RoomName);
        Assert.Equal(2, chair.Version); Assert.Equal(99, chair.UpdatedByUserId); Assert.Equal(clock.UtcNow, chair.UpdatedAt);
        Assert.Equal(status != 4, chair.IsActive); Assert.Null(chair.Description);
        await audit.Received(1).LogRequiredAsync("MOD_MST_CHAIR_UPDATED", "DentalChair", 7, 99, Arg.Any<string>(), null, Arg.Any<CancellationToken>());
    }
    [Fact] public async Task StaleVersionAndOccupiedChairCannotBeChanged()
    {
        Assert.Equal(DentalChairService.Changed, (await Service().UpdateAsync(7, 99, RoleCodes.Admin, new("P", 3, null, 2, true))).Error);
        chair.Status = DentalChairStatus.Occupied;
        Assert.Equal(DentalChairService.Occupied, (await Service().UpdateAsync(7, 99, RoleCodes.Admin, new("P", 1, null, 1, true))).Error);
        Assert.Equal(1, chair.Version); Assert.Empty(audit.ReceivedCalls());
    }
    [Fact] public async Task AssignedWorkspaceProtectsCatalogFromIndirectRoomChange()
    {
        chairs.IsAssignedAsync(7,Arg.Any<CancellationToken>()).Returns(true);
        Assert.Equal(DentalChairService.Assigned,(await Service().UpdateAsync(7,99,RoleCodes.Admin,new("Other",3,null,1,true))).Error);
        Assert.Equal("Phòng 1",chair.RoomName); Assert.Equal(1,chair.Version); Assert.Empty(audit.ReceivedCalls());
    }
    [Fact] public async Task UnknownChairIsNotFound()
        => Assert.Equal(Error.NotFound, (await Service().UpdateAsync(8, 99, RoleCodes.Admin, new("P", 1, null, 1, true))).Error);
    [Theory] [InlineData(0)] [InlineData(2)] [InlineData(5)]
    public async Task CannotManuallyCreateOccupiedOrUnknownStatuses(int status)
    {
        var request = new CreateDentalChairRequest("GHE-02", "P", status, null, true);
        Assert.False(new CreateDentalChairRequestValidator().Validate(request).IsValid);
        Assert.Equal(Error.Validation, (await Service().CreateAsync(99, RoleCodes.Admin, request)).Error); Assert.Empty(chairs.ReceivedCalls());
    }
    [Fact] public async Task NamesConfirmationAndLimitsValidatedAtService()
    {
        foreach (var request in new[] { new CreateDentalChairRequest(" ", "P", 1, null, true), new("GHE", " ", 1, null, true),
            new("GHE", "P", 1, null, false), new(new string('x', 51), "P", 1, null, true), new("GHE", new string('x', 101), 1, null, true), new("GHE", "P", 1, new string('x', 501), true) })
            Assert.Equal(Error.Validation, (await Service().CreateAsync(99, RoleCodes.Admin, request)).Error);
        Assert.True(new CreateDentalChairRequestValidator().Validate(new CreateDentalChairRequest(new string('x', 50), new string('x', 100), 1, new string('x', 500), true)).IsValid);
        Assert.Empty(chairs.ReceivedCalls());
    }
    [Fact] public async Task QueryIsBoundedAndReturnsActualEmptyPage()
    {
        foreach (var query in new[] { new DentalChairQuery(Page: 0), new(PageSize: 101), new(Status: 5), new(Keyword: new string('x', 101)) })
            Assert.Equal(Error.Validation, (await Service().GetPageAsync(RoleCodes.Admin, query)).Error);
        Assert.Empty(chairs.ReceivedCalls()); Assert.True((await Service().GetPageAsync(RoleCodes.Admin, new())).IsSuccess);
    }
    [Fact] public async Task SerializableConflictReturnsReloadError()
    {
        uow.ExecuteSerializableTransactionAsync(Arg.Any<Func<CancellationToken, Task<Result<DentalChairResponse>>>>(), Arg.Any<CancellationToken>())
            .Returns<Result<DentalChairResponse>>(_ => throw new ConcurrentOperationException());
        Assert.Equal(DentalChairService.Changed, (await Service().CreateAsync(99, RoleCodes.Admin, new("GHE-02", "P", 1, null, true))).Error);
    }
    [Fact] public async Task RequiredAuditFailureCannotBeSwallowed()
    {
        audit.LogRequiredAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Synthetic chair audit failure")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service().CreateAsync(99, RoleCodes.Admin, new("GHE-02", "P", 1, null, true)));
    }
}
