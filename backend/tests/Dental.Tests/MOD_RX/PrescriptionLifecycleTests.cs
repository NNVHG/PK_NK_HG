using Dental.Application.Common;
using Dental.Application.Features.MOD_RX.DTOs;
using Dental.Application.Features.MOD_RX.Services;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using NSubstitute;
using Xunit;
namespace Dental.Tests.MOD_RX;
public sealed class PrescriptionLifecycleTests
{
    private static PrescriptionSnapshot State() => new(new Visit { VisitId=7,Status=VisitStatuses.InProgress },
        new Invoice { Id=8,Status=InvoiceStatus.Draft },new Prescription { Id=9,Version=1 },false,null,[],[],[]);
    [Fact] public void OnlyInProgressUnpaidDraftCanCancel()
    {
        var state=State(); Assert.True(PrescriptionRules.CanCancel(state));
        foreach(var status in new[] { VisitStatuses.Created,VisitStatuses.Completed,VisitStatuses.Correcting,VisitStatuses.Cancelled })
        { state.Visit.Status=status;Assert.False(PrescriptionRules.CanCancel(state)); }
    }
    [Fact] public void PaymentHistoryLockDispenseCancelAndOverflowBlockCancellation()
    {
        var s=State();Assert.False(PrescriptionRules.CanCancel(s with { HasReceiptHistory=true }));
        Assert.False(PrescriptionRules.CanCancel(s with { Prescription=null }));Assert.False(PrescriptionRules.CanCancel(s with { Invoice=null }));
        s.Visit.IsLocked=true;Assert.False(PrescriptionRules.CanCancel(s));s.Visit.IsLocked=false;
        foreach(var status in new[] { InvoiceStatus.PendingPayment,InvoiceStatus.PartiallyPaid,InvoiceStatus.Paid,InvoiceStatus.Cancelled })
        { s.Invoice!.Status=status;Assert.False(PrescriptionRules.CanCancel(s)); }s.Invoice!.Status=InvoiceStatus.Draft;
        s.Invoice.PaidAmount=1;Assert.False(PrescriptionRules.CanCancel(s));s.Invoice.PaidAmount=0;
        s.Prescription!.IsCancelled=true;Assert.False(PrescriptionRules.CanCancel(s));s.Prescription.IsCancelled=false;
        s.Prescription.DispensedAt=DateTime.UtcNow;Assert.False(PrescriptionRules.CanCancel(s));s.Prescription.DispensedAt=null;
        s.Prescription.Version=int.MaxValue;Assert.False(PrescriptionRules.CanCancel(s));
    }
    [Fact] public void ReplacementRequiresInProgressAndPreviewTracksCancellationGeneration()
    {
        var state=State() with { Prescription=null, Medicines=[new Medicine { Id=1,Version=1,Code="TEST",Name="Synthetic",Unit="Unit",UnitPrice=100,IsActive=true }] };
        var request=new PrescriptionWriteRequest(0,"Synthetic",[new(1,1,1,"Synthetic saved usage")]);
        var before=PrescriptionRules.Build(state,request,false);Assert.True(before.IsSuccess);
        var after=state with { CancellationGeneration=9 };
        Assert.NotEqual(before.Value!.Preview.Token,PrescriptionRules.Build(after,request,false).Value!.Preview.Token);
        Assert.Equal("RX_003",PrescriptionRules.Build(after,request with { PreviewToken=before.Value.Preview.Token },true).Error.Code);
        Assert.True(PrescriptionRules.CanEdit(after));after.Visit.Status=VisitStatuses.Correcting;Assert.False(PrescriptionRules.CanEdit(after));
    }
    [Theory] [InlineData(0)] [InlineData(-1)] [InlineData(int.MaxValue)]
    public void CancellationVersionBounds(int value) => Assert.False(new PrescriptionCancellationValidator().Validate(new PrescriptionCancellationRequest(value)).IsValid);
    [Theory] [InlineData(null)] [InlineData("")] [InlineData("Transfer replacement")]
    public void OptionalReasonPreservesExistingCancellation(string? reason) => Assert.True(new PrescriptionCancellationValidator().Validate(new PrescriptionCancellationRequest(1,reason)).IsValid);
    [Fact] public async Task ReasonLengthIsBoundedBeforeRepository()
    {
        var validator=new PrescriptionCancellationValidator();
        Assert.True(validator.Validate(new PrescriptionCancellationRequest(1,new string('x',500))).IsValid);
        var repo=Substitute.For<IPrescriptionRepository>();
        Assert.Equal(Error.Validation,(await new PrescriptionService(repo).CancelAsync(7,9,3,RoleCodes.Dentist,new(1,new string('x',501)))).Error);
        Assert.Empty(repo.ReceivedCalls());
    }
    [Theory] [InlineData(RoleCodes.Admin)] [InlineData(RoleCodes.Receptionist)] [InlineData(RoleCodes.Assistant)] [InlineData(RoleCodes.Patient)] [InlineData(null)]
    public async Task OnlyDentistMayCancel(string? role)
    {
        var repo=Substitute.For<IPrescriptionRepository>();
        Assert.Equal(Error.Forbidden,(await new PrescriptionService(repo).CancelAsync(7,9,3,role,new(1))).Error);Assert.Empty(repo.ReceivedCalls());
    }
    [Fact] public async Task InvalidCancelAndHistoryInputNeverReachRepository()
    {
        var repo=Substitute.For<IPrescriptionRepository>();var service=new PrescriptionService(repo);
        Assert.True((await service.CancelAsync(0,9,3,RoleCodes.Dentist,new(1))).IsFailure);
        Assert.True((await service.CancelAsync(7,0,3,RoleCodes.Dentist,new(1))).IsFailure);
        Assert.True((await service.CancelAsync(7,9,0,RoleCodes.Dentist,new(1))).IsFailure);
        Assert.True((await service.CancelAsync(7,9,3,RoleCodes.Dentist,new(0))).IsFailure);
        foreach(var pair in new[] { (0,10),(1000001,10),(1,0),(1,51) })Assert.True((await service.HistoryAsync(7,3,RoleCodes.Dentist,pair.Item1,pair.Item2)).IsFailure);
        Assert.True((await service.HistoryAsync(7,3,"UNKNOWN")).IsFailure);Assert.Empty(repo.ReceivedCalls());
    }
    [Fact] public async Task ValidCancelDelegatesExactTargetIdAndVersion()
    {
        var repo=Substitute.For<IPrescriptionRepository>();var request=new PrescriptionCancellationRequest(2,"Transfer replacement");
        repo.CancelAsync(7,9,3,request,null,default).Returns(Result<PrescriptionCancellationResult>.Success(new(9,7,3,8,100)));
        Assert.True((await new PrescriptionService(repo).CancelAsync(7,9,3,RoleCodes.Dentist,request)).IsSuccess);
        await repo.Received().CancelAsync(7,9,3,request,null,default);
    }
}
