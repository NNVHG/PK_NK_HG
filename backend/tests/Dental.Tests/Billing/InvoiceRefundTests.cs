using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Services;
using Dental.Application.Features.MOD_BIL.Validators;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Domain.Constants;
using Xunit;
namespace Dental.Tests.Billing;
public sealed class InvoiceRefundTests
{
    private static Invoice Create(decimal paid=1000000) => new(){Id=1,TotalAmount=1000000,PaidAmount=paid,Status=paid==1000000?InvoiceStatus.Paid:InvoiceStatus.PartiallyPaid,
        Visit=new(){Status=VisitStatuses.Completed,IsLocked=true},Items=[new(){Id=1,Quantity=1,UnitPrice=500000,TotalAmount=500000},new(){Id=2,Quantity=1,UnitPrice=500000,TotalAmount=500000}]};
    [Theory]
    [InlineData(1000000,500000,0,500000)] [InlineData(600000,100000,400000,500000)]
    [InlineData(200000,0,500000,200000)] [InlineData(500000,0,500000,500000)]
    public void DebtFirst_OnlyReturnsExcess(decimal paid,decimal refund,decimal debtReduction,decimal paidAfter)
    {var c=InvoiceRefundRules.Calculate(Create(paid),[new(1,1)]).Value!;Assert.Equal(refund,c.Refund);Assert.Equal(debtReduction,c.DebtReduction);Assert.Equal(paidAfter,c.Paid);Assert.Equal(500000,c.Total);}
    [Fact]public void FixedDiscount_RemainsOriginalOnRemainder()
    {var i=Create(900000);i.DiscountAmount=100000;i.DiscountType="FixedAmount";i.DiscountValue=100000;var c=InvoiceRefundRules.Calculate(i,[new(1,1)]).Value!;Assert.Equal(500000,c.Refund);Assert.Equal(100000,c.Discount);Assert.Equal(400000,c.Paid);}
    [Fact]public void FixedDiscountGreaterThanRemainder_BlocksPartialButAllowsFull()
    {var i=Create(100000);i.DiscountAmount=900000;i.DiscountType="FixedAmount";i.DiscountValue=900000;Assert.True(InvoiceRefundRules.Calculate(i,[new(1,1)]).IsFailure);var c=InvoiceRefundRules.Calculate(i,[new(1,1),new(2,1)]).Value!;Assert.True(c.FullCancellation);Assert.Equal(0,c.Discount);Assert.Equal(100000,c.Refund);}
    [Fact]public void Percentage_OnlyRemainingServicesRoundsAwayFromZero()
    {var i=Create(210);i.TotalAmount=221;i.DiscountType="Percentage";i.DiscountValue=10;i.DiscountAmount=11;i.Items=[new(){Id=1,Quantity=2,UnitPrice=55,TotalAmount=110},new(){Id=2,ItemType="Medicine",Quantity=1,UnitPrice=111,TotalAmount=111}];var c=InvoiceRefundRules.Calculate(i,[new(1,1)]).Value!;Assert.Equal(6,c.Discount);Assert.Equal(50,c.Refund);Assert.Equal(166,c.Total);}
    [Theory][InlineData(1000000)][InlineData(600000)][InlineData(200000)]
    public void FullCancellation_ReturnsOnlyActualPaidAndClearsDebt(decimal paid)
    {var c=InvoiceRefundRules.Calculate(Create(paid),[new(1,1),new(2,1)]).Value!;Assert.True(c.FullCancellation);Assert.Equal(paid,c.Refund);Assert.Equal(0,c.Total);Assert.Equal(0,c.Paid);Assert.Equal(1000000-paid,c.DebtReduction);}
    [Theory][InlineData(0)][InlineData(-1)][InlineData(2)]
    public void OutOfRangeQuantity_IsRejected(int quantity)=>Assert.True(InvoiceRefundRules.Calculate(Create(),[new(1,quantity)]).IsFailure);
    [Fact]public void DuplicateSelection_IsRejected()=>Assert.True(InvoiceRefundRules.Calculate(Create(),[new(1,1),new(1,1)]).IsFailure);
    [Fact]public void ForeignLine_IsRejected()=>Assert.True(InvoiceRefundRules.Calculate(Create(),[new(999,1)]).IsFailure);
    [Fact]public void PriorRefundedQuantityCannotBeRefundedAgain()
    {var i=Create(500000);i.Status=InvoiceStatus.PartiallyRefunded;i.TotalAmount=500000;i.Refunds=[new(){Items=[new(){InvoiceItemId=1,Quantity=1}]}];Assert.True(InvoiceRefundRules.Calculate(i,[new(1,1)]).IsFailure);Assert.True(InvoiceRefundRules.Calculate(i,[new(2,1)]).IsSuccess);}
    [Theory][InlineData(InvoiceStatus.Draft)][InlineData(InvoiceStatus.PendingPayment)][InlineData(InvoiceStatus.Cancelled)][InlineData(InvoiceStatus.Refunded)]
    public void UnsupportedStatus_IsRejected(InvoiceStatus status){var i=Create();i.Status=status;Assert.True(InvoiceRefundRules.Calculate(i,[new(1,1)]).IsFailure);}
    [Fact]public void FullyDiscounted_FreeCancellationDoesNotInventMoney()
    {var i=Create(0);i.Status=InvoiceStatus.Paid;i.DiscountType="FixedAmount";i.DiscountValue=i.DiscountAmount=1000000;var c=InvoiceRefundRules.Calculate(i,[new(1,1),new(2,1)]).Value!;Assert.Equal(0,c.Refund);Assert.Equal(0,c.DebtReduction);}
    [Fact]public void OriginalSnapshotIsNotMutatedByCalculation()
    {var i=Create();InvoiceRefundRules.Calculate(i,[new(1,1)]);Assert.Equal(1000000,i.TotalAmount);Assert.Equal(1,i.Items.First().Quantity);Assert.Equal(InvoiceStatus.Paid,i.Status);}
    [Theory][InlineData(100000)][InlineData(500000)]
    public void CollectionAfterRefund_PreservesRefundedStatus(decimal amount)
    {var i=Create(0);i.Status=InvoiceStatus.PartiallyRefunded;i.TotalAmount=500000;Assert.True(CashPaymentRules.Apply(i,amount).IsSuccess);Assert.Equal(InvoiceStatus.PartiallyRefunded,i.Status);}
    [Fact]public void TransferAfterRefund_UsesSameDebtRules()
    {var i=Create(200000);i.TotalAmount=500000;i.Status=InvoiceStatus.PartiallyRefunded;Assert.True(BankTransferRules.Apply(i,300000).IsSuccess);Assert.Equal(500000,i.PaidAmount);Assert.Equal(InvoiceStatus.PartiallyRefunded,i.Status);}
    [Theory][InlineData(null)][InlineData("")][InlineData("bad")]
    public void InvalidSnapshot_IsRejected(string? snapshot)=>Assert.False(new InvoiceRefundPreviewValidator().Validate(new InvoiceRefundPreviewRequest(snapshot!,[new(1,1)],"Cash")).IsValid);
    [Fact]public void NullOrDuplicateSelectionsAreRejectedWithoutThrowing()
    {var v=new InvoiceRefundPreviewValidator();foreach(var lines in new IReadOnlyList<RefundSelection>[] {null!,[null!],[new(1,1),new(1,1)]})Assert.False(v.Validate(new InvoiceRefundPreviewRequest(new('A',64),lines,"Cash")).IsValid);}
    [Theory][InlineData("bad",true,"Synthetic reason")][InlineData("OTHER",false,"Synthetic reason")][InlineData("OTHER",true,"")][InlineData("OTHER",true,"bad\nreason")]
    public void InvalidConfirmationOrReason_IsRejected(string reason,bool confirmed,string notes)
    {var r=new InvoiceRefundRequest(new('A',64),[new(1,1)],"Cash",new('B',64),Guid.NewGuid(),reason,notes,confirmed);Assert.False(new InvoiceRefundRequestValidator().Validate(r).IsValid);}
    [Theory][InlineData(0,false)][InlineData(7,false)][InlineData(8,true)]
    public void SingleReceiptOrUnambiguousWindow_UsesApprovedSevenDayLimit(int days,bool expected)
    {var now=new DateTime(2026,10,18,2,0,0,DateTimeKind.Utc);Assert.Equal(expected,InvoiceRefundRules.RequiresLateApproval([now.AddDays(-days)],now).Value);}
    [Fact]public void SplitReceiptsAcrossLimit_WaitsForQ324InsteadOfChoosingBusinessPolicy()
    {var now=new DateTime(2026,10,18,2,0,0,DateTimeKind.Utc);Assert.Equal("BIL_REFUND_WINDOW_UNRESOLVED",InvoiceRefundRules.RequiresLateApproval([now.AddDays(-8),now.AddDays(-1)],now).Error.Code);}

}
