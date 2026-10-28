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
public sealed class StaffWorkspaceTests
{
    private readonly IStaffWorkspaceRepository workspaces = Substitute.For<IStaffWorkspaceRepository>();
    private readonly IDentalChairRepository chairs = Substitute.For<IDentalChairRepository>();
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IVisitRepository visits = Substitute.For<IVisitRepository>();
    private readonly IQueueRepository queues = Substitute.For<IQueueRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogger audit = Substitute.For<IAuditLogger>();
    private readonly VietnamClock clock = new(new FixedTimeProvider(new DateTimeOffset(2026,10,16,2,0,0,TimeSpan.Zero)));
    private readonly Dictionary<int, User> staff = new();
    private readonly Dictionary<int, StaffWorkspace> assignments = new();
    private readonly Dictionary<int, DentalChair> locations = new();
    private StaffWorkspaceService Service() => new(workspaces, chairs, users, visits, queues, uow, audit, clock);
    public StaffWorkspaceTests()
    {
        foreach (var (id, role) in new[] { (5,RoleCodes.Dentist),(7,RoleCodes.Dentist),(6,RoleCodes.Assistant),(8,RoleCodes.Assistant),(99,RoleCodes.Admin),(4,RoleCodes.Receptionist),(100,RoleCodes.Patient) })
            staff[id] = new User { UserId=id, FullName="Synthetic staff " + id, IsActive=true, Role=new Role { RoleCode=role } };
        locations[1]=new() { Id=1, ChairNumber="GHE-01", RoomName="Phòng 3", Status=DentalChairStatus.Available, IsActive=true };
        locations[2]=new() { Id=2, ChairNumber="GHE-02", RoomName="Phòng 4", Status=DentalChairStatus.Available, IsActive=true };
        assignments[5]=new() { UserId=5, ChairId=1, Version=1 }; assignments[6]=new() { UserId=6, DentistId=5, Version=1 };
        users.FindByIdAsync(Arg.Any<int>(),Arg.Any<CancellationToken>()).Returns(c=>staff.GetValueOrDefault(c.Arg<int>()));
        users.GetActiveDentistsAsync(Arg.Any<CancellationToken>()).Returns(new[] { staff[5],staff[7] });
        workspaces.FindAsync(Arg.Any<int>(),Arg.Any<CancellationToken>()).Returns(c=>assignments.GetValueOrDefault(c.Arg<int>()));
        workspaces.GetAllAsync(Arg.Any<CancellationToken>()).Returns(_=>assignments.Values.ToArray());
        workspaces.When(x=>x.AddAsync(Arg.Any<StaffWorkspace>(),Arg.Any<CancellationToken>())).Do(c=>assignments[c.Arg<StaffWorkspace>().UserId]=c.Arg<StaffWorkspace>());
        chairs.FindAsync(Arg.Any<int>(),Arg.Any<CancellationToken>()).Returns(c=>locations.GetValueOrDefault(c.Arg<int>()));
        chairs.GetAllAsync(Arg.Any<CancellationToken>()).Returns(_=>locations.Values.ToArray());
        uow.ExecuteSerializableTransactionAsync(Arg.Any<Func<CancellationToken,Task<Result<WorkspaceSnapshot>>>>(),Arg.Any<CancellationToken>())
            .Returns(c=>c.Arg<Func<CancellationToken,Task<Result<WorkspaceSnapshot>>>>()(CancellationToken.None));
    }
    [Fact] public async Task DentistChoosesOwnChairAndAssistantImmediatelyFollowsWithoutCopy()
    {
        var result=await Service().ChangeAsync(5,RoleCodes.Dentist,5,new(2,null,1,true));
        Assert.True(result.IsSuccess); Assert.Equal("Phòng 4",result.Value!.RoomName); Assert.Equal(2,assignments[5].Version);
        var assistant=(await Service().GetAsync(6,RoleCodes.Assistant,6)).Value!;
        Assert.Equal(2,assistant.ChairId); Assert.Equal("Phòng 4",assistant.RoomName); Assert.Equal(1,assistant.Version);
        Assert.Null(assignments[6].ChairId); Assert.Equal(5,assignments[6].DentistId);
        await audit.Received(1).LogRequiredAsync("MOD_MST_WORKSPACE_CHANGED","StaffWorkspace",5,5,Arg.Any<string>(),null,Arg.Any<CancellationToken>());
    }
    [Fact] public async Task AssistantChoosesDentistAndCannotSetChair()
    {
        assignments.Remove(6);
        var result=await Service().ChangeAsync(6,RoleCodes.Assistant,6,new(null,5,0,true));
        Assert.True(result.IsSuccess); Assert.Equal("Phòng 3",result.Value!.RoomName); Assert.Null(assignments[6].ChairId);
        Assert.Equal(Error.Validation,(await Service().ChangeAsync(6,RoleCodes.Assistant,6,new(2,null,1,true))).Error);
    }
    [Theory] [InlineData(5,RoleCodes.Dentist,7)] [InlineData(6,RoleCodes.Assistant,8)] [InlineData(5,RoleCodes.Dentist,6)]
    public async Task StaffCannotReadOrChangeOtherPeople(int actor,string role,int target)
    {
        Assert.Equal(Error.Forbidden,(await Service().GetAsync(actor,role,target)).Error);
        Assert.Equal(Error.Forbidden,(await Service().ChangeAsync(actor,role,target,new(null,null,0,true))).Error);
        Assert.Empty(uow.ReceivedCalls());
    }
    [Theory] [InlineData(4,RoleCodes.Receptionist)] [InlineData(100,RoleCodes.Patient)] [InlineData(99,"PATIENT")]
    public async Task OtherRolesCannotUseOptionsOrChange(int actor,string role)
    {
        Assert.Equal(Error.Forbidden,(await Service().GetOptionsAsync(actor,role)).Error);
        Assert.Equal(Error.Forbidden,(await Service().ChangeAsync(actor,role,actor,new(null,null,0,true))).Error); Assert.Empty(audit.ReceivedCalls());
    }
    [Fact] public async Task InactiveActorAndForgedClaimRoleAreRejected()
    {
        staff[5].IsActive=false; Assert.Equal(Error.Forbidden,(await Service().ChangeAsync(5,RoleCodes.Dentist,5,new(2,null,1,true))).Error);
        Assert.Equal(Error.Forbidden,(await Service().ChangeAsync(6,RoleCodes.Admin,5,new(2,null,1,true))).Error);
        Assert.Empty(audit.ReceivedCalls());
    }
    [Theory] [InlineData(DentalChairStatus.Maintenance)] [InlineData(DentalChairStatus.Inactive)] [InlineData(DentalChairStatus.Occupied)]
    public async Task UnavailableChairsCannotBeChosen(DentalChairStatus status)
    {
        locations[2].Status=status;
        Assert.Equal(StaffWorkspaceService.Unavailable,(await Service().ChangeAsync(5,RoleCodes.Dentist,5,new(2,null,1,true))).Error);
        Assert.Equal(1,assignments[5].ChairId); Assert.Empty(audit.ReceivedCalls());
    }
    [Fact] public async Task InactiveMissingOrAlreadyClaimedChairCannotBeChosen()
    {
        locations[2].IsActive=false; Assert.Equal(StaffWorkspaceService.Unavailable,(await Service().ChangeAsync(5,RoleCodes.Dentist,5,new(2,null,1,true))).Error);
        locations[2].IsActive=true; workspaces.ChairClaimedAsync(2,5,Arg.Any<CancellationToken>()).Returns(true);
        Assert.Equal(StaffWorkspaceService.Unavailable,(await Service().ChangeAsync(5,RoleCodes.Dentist,5,new(2,null,1,true))).Error);
        Assert.Equal(StaffWorkspaceService.Unavailable,(await Service().ChangeAsync(5,RoleCodes.Dentist,5,new(999,null,1,true))).Error);
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public async Task ActiveVisitAndPendingInvitationBlockMovingDentist(bool activeVisit)
    {
        if(activeVisit) visits.HasActiveVisitForDentistAsync(5,Arg.Any<CancellationToken>()).Returns(true);
        else queues.HasCallingForDentistAsync(clock.Today,5,Arg.Any<CancellationToken>()).Returns(true);
        Assert.Equal(StaffWorkspaceService.Busy,(await Service().ChangeAsync(5,RoleCodes.Dentist,5,new(2,null,1,true))).Error);
        Assert.False((await Service().GetAsync(5,RoleCodes.Dentist,5)).Value!.CanChange); Assert.Empty(audit.ReceivedCalls());
    }
    [Fact] public async Task AssistantCanLeaveOrJoinBusyDentistWithoutMovingVisit()
    {
        visits.HasActiveVisitForDentistAsync(5,Arg.Any<CancellationToken>()).Returns(true);
        Assert.True((await Service().GetAsync(6,RoleCodes.Assistant,6)).Value!.CanChange);
        Assert.True((await Service().ChangeAsync(6,RoleCodes.Assistant,6,new(null,null,1,true))).IsSuccess);
        Assert.True((await Service().ChangeAsync(8,RoleCodes.Assistant,8,new(null,5,0,true))).IsSuccess);
        Assert.Equal(1,assignments[5].ChairId);
    }
    [Fact] public async Task MultipleAssistantsCanChooseSameDentist()
    {
        Assert.True((await Service().ChangeAsync(8,RoleCodes.Assistant,8,new(null,5,0,true))).IsSuccess);
        Assert.Equal(5,assignments[6].DentistId);
        Assert.Equal(5,assignments[8].DentistId);
        var options=(await Service().GetOptionsAsync(8,RoleCodes.Assistant)).Value!;
        Assert.Equal(2,options.Dentists.Single(x=>x.DentistId==5).AssistantCount);
        Assert.True(new[] { 6, 8 }.Contains(options.Dentists.Single(x=>x.DentistId==5).AssistantId.GetValueOrDefault()));
    }
    [Fact] public async Task AssistantRequiresActiveDentistButCanLinkBeforeRoomSelection()
    {
        Assert.True((await Service().ChangeAsync(8,RoleCodes.Assistant,8,new(null,7,0,true))).IsSuccess);
        staff[5].IsActive=false; Assert.Equal(StaffWorkspaceService.InvalidTarget,(await Service().ChangeAsync(8,RoleCodes.Assistant,8,new(null,5,1,true))).Error);
        staff[5].IsActive=true; locations[1].Status=DentalChairStatus.Maintenance;
        Assert.True((await Service().ChangeAsync(8,RoleCodes.Assistant,8,new(null,5,1,true))).IsSuccess);
    }
    [Fact] public async Task DentistCannotUseAssistantLink()
        => Assert.Equal(Error.Validation,(await Service().ChangeAsync(5,RoleCodes.Dentist,5,new(null,7,1,true))).Error);
    [Fact] public async Task AdminCanChangeOtherStaffAndClearInactiveOrChangedRoleWithoutImpersonation()
    {
        Assert.True((await Service().ChangeAsync(99,RoleCodes.Admin,5,new(2,null,1,true))).IsSuccess);
        staff[5].IsActive=false; staff[5].Role.RoleCode=RoleCodes.Receptionist;
        Assert.True((await Service().ChangeAsync(99,RoleCodes.Admin,5,new(null,null,2,true))).IsSuccess);
        Assert.Null(assignments[5].ChairId);
        await audit.Received(2).LogRequiredAsync("MOD_MST_WORKSPACE_CHANGED","StaffWorkspace",5,99,Arg.Any<string>(),null,Arg.Any<CancellationToken>());
    }
    [Fact] public async Task CannotAssignInactiveStaffOrPatient()
    {
        staff[5].IsActive=false; Assert.Equal(StaffWorkspaceService.InvalidTarget,(await Service().ChangeAsync(99,RoleCodes.Admin,5,new(2,null,1,true))).Error);
        Assert.Equal(StaffWorkspaceService.InvalidTarget,(await Service().ChangeAsync(99,RoleCodes.Admin,100,new(null,null,0,true))).Error);
    }
    [Fact] public async Task StaleAndMissingTargetsNeverWrite()
    {
        Assert.Equal(StaffWorkspaceService.Changed,(await Service().ChangeAsync(5,RoleCodes.Dentist,5,new(2,null,0,true))).Error);
        Assert.Equal(Error.NotFound,(await Service().ChangeAsync(99,RoleCodes.Admin,999,new(null,null,0,true))).Error); Assert.Empty(audit.ReceivedCalls());
    }
    [Fact] public async Task SameSelectionIsIdempotentEvenWhenBusy()
    {
        visits.HasActiveVisitForDentistAsync(5,Arg.Any<CancellationToken>()).Returns(true);
        Assert.True((await Service().ChangeAsync(5,RoleCodes.Dentist,5,new(1,null,1,true))).IsSuccess);
        Assert.Equal(1,assignments[5].Version); Assert.Empty(audit.ReceivedCalls());
    }
    [Fact] public async Task AdminTargetsAreMinimalAndBounded()
    {
        users.GetStaffPageAsync(null,null,null,1,20,Arg.Any<CancellationToken>()).Returns((new[] { staff[5] },1));
        var result=await Service().GetTargetsAsync(99,RoleCodes.Admin,new()); Assert.True(result.IsSuccess);
        Assert.Equal(4,Assert.Single(result.Value!.Items).GetType().GetProperties().Length);
        Assert.Equal(Error.Forbidden,(await Service().GetTargetsAsync(5,RoleCodes.Dentist,new())).Error);
        Assert.Equal(Error.Validation,(await Service().GetTargetsAsync(99,RoleCodes.Admin,new(Page:0))).Error);
    }
    [Fact] public async Task InactiveAssistantLocationIsNotAdvertisedAsUsable()
    {
        staff[6].IsActive=false;
        var snapshot=(await Service().GetAsync(99,RoleCodes.Admin,6)).Value!;
        Assert.False(snapshot.IsLocationUsable); Assert.Equal("Phòng 3",snapshot.RoomName);
    }
    [Fact] public async Task OptionsExposeActualChairAndPairClaimsWithoutClinicalData()
    {
        var result=(await Service().GetOptionsAsync(6,RoleCodes.Assistant)).Value!;
        var dentist=result.Dentists.Single(x=>x.DentistId==5); Assert.Equal("Phòng 3",dentist.RoomName); Assert.Equal(1,dentist.AssistantCount);
        Assert.Equal(5,result.Chairs.Single(x=>x.Id==1).ClaimedByUserId); Assert.True(result.Dentists.Single(x=>x.DentistId==7).IsReady);
    }
    [Theory] [InlineData(0,null,1,true)] [InlineData(null,0,1,true)] [InlineData(1,5,1,true)] [InlineData(1,null,-1,true)] [InlineData(1,null,1,false)]
    public async Task ValidatorRejectsInvalidRequests(int? chair,int? dentist,int version,bool confirmed)
    {
        var request=new ChangeWorkspaceRequest(chair,dentist,version,confirmed);
        Assert.False(new ChangeWorkspaceRequestValidator().Validate(request).IsValid);
        Assert.Equal(Error.Validation,(await Service().ChangeAsync(5,RoleCodes.Dentist,5,request)).Error);
    }
    [Fact] public async Task SerializationConflictReturnsReloadError()
    {
        uow.ExecuteSerializableTransactionAsync(Arg.Any<Func<CancellationToken,Task<Result<WorkspaceSnapshot>>>>(),Arg.Any<CancellationToken>())
            .Returns<Result<WorkspaceSnapshot>>(_=>throw new ConcurrentOperationException());
        Assert.Equal(StaffWorkspaceService.Changed,(await Service().ChangeAsync(5,RoleCodes.Dentist,5,new(2,null,1,true))).Error);
    }
    [Fact] public async Task RequiredAuditFailurePropagatesToRollbackBoundary()
    {
        audit.LogRequiredAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<int?>(),Arg.Any<int?>(),Arg.Any<string?>(),Arg.Any<string?>(),Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Synthetic workspace audit failure")));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Service().ChangeAsync(5,RoleCodes.Dentist,5,new(2,null,1,true)));
    }
}
