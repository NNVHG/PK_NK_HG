using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Services;
using Dental.Application.Features.MOD_BIL.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Dental.Tests.Billing;

public sealed class CashPaymentTests
{
    private static Invoice Invoice() => new() { Id = 1, TotalAmount = 100000,
        Status = InvoiceStatus.PendingPayment, Visit = new Visit { Status = VisitStatuses.Completed, IsLocked = true } };
    private static CashPaymentRequest Request(decimal amount = 10000, decimal tendered = 20000)
        => new(amount, tendered, Guid.NewGuid());

    [Theory]
    [InlineData(0, 10, false)]
    [InlineData(-1, 10, false)]
    [InlineData(1.5, 10, false)]
    [InlineData(10, 9, false)]
    [InlineData(10, 10.5, false)]
    [InlineData(10, 10, true)]
    [InlineData(10, 100, true)]
    public void ValidatesActualAmountAndTendered(decimal amount, decimal tendered, bool allowed)
        => Assert.Equal(allowed, new CashPaymentRequestValidator().Validate(Request(amount, tendered)).IsValid);

    [Fact]
    public void RejectsEmptyRequestIdAndUnsupportedMethod()
    {
        Assert.False(new CashPaymentRequestValidator().Validate(new CashPaymentRequest(10, 10, Guid.Empty)).IsValid);
        Assert.False(new CashPaymentRequestValidator().Validate(new CashPaymentRequest(10, 10, Guid.NewGuid(), "BankTransfer")).IsValid);
        Assert.False(new CashPaymentRequestValidator().Validate(Request(decimal.MaxValue, decimal.MaxValue)).IsValid);
    }

    [Theory]
    [InlineData(InvoiceStatus.Draft)]
    [InlineData(InvoiceStatus.Paid)]
    [InlineData(InvoiceStatus.Cancelled)]
    public void RejectsNonPayableStatuses(InvoiceStatus status)
    {
        var invoice = Invoice(); invoice.Status = status;
        Assert.Equal("BIL_031", CashPaymentRules.Apply(invoice, 10000).Error.Code);
        Assert.Equal(0, invoice.PaidAmount);
    }

    [Theory]
    [InlineData(VisitStatuses.InProgress, false)]
    [InlineData(VisitStatuses.Completed, false)]
    [InlineData(VisitStatuses.Cancelled, true)]
    public void RequiresCompletedLockedVisit(string status, bool locked)
    {
        var invoice = Invoice(); invoice.Visit.Status = status; invoice.Visit.IsLocked = locked;
        Assert.True(CashPaymentRules.Apply(invoice, 10000).IsFailure);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100001)]
    [InlineData(0.5)]
    public void InvalidAmountDoesNotMutateInvoice(decimal amount)
    {
        var invoice = Invoice();
        Assert.Equal("BIL_032", CashPaymentRules.Apply(invoice, amount).Error.Code);
        Assert.Equal(0, invoice.PaidAmount); Assert.Equal(InvoiceStatus.PendingPayment, invoice.Status);
    }

    [Fact]
    public void PartialThenFullPaymentUpdatesExactDebt()
    {
        var invoice = Invoice();
        Assert.True(CashPaymentRules.Apply(invoice, 30000).IsSuccess);
        Assert.Equal(InvoiceStatus.PartiallyPaid, invoice.Status); Assert.Equal(30000, invoice.PaidAmount);
        Assert.True(CashPaymentRules.Apply(invoice, 70000).IsSuccess);
        Assert.Equal(InvoiceStatus.Paid, invoice.Status); Assert.Equal(100000, invoice.PaidAmount);
        Assert.True(CashPaymentRules.Apply(invoice, 1).IsFailure);
    }

    [Fact]
    public void OverRemainingBalanceIsRejected()
    {
        var invoice = Invoice(); invoice.PaidAmount = 90000; invoice.Status = InvoiceStatus.PartiallyPaid;
        Assert.True(CashPaymentRules.Apply(invoice, 10001).IsFailure);
        Assert.Equal(90000, invoice.PaidAmount);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100001)]
    public void CorruptBalanceIsRejected(decimal paid)
    {
        var invoice = Invoice(); invoice.PaidAmount = paid;
        Assert.True(CashPaymentRules.Apply(invoice, 1).IsFailure);
    }

    [Theory]
    [InlineData(RoleCodes.Dentist)]
    [InlineData(RoleCodes.Patient)]
    [InlineData(RoleCodes.Assistant)]
    public async Task NonCashierCannotReceive(string role)
    {
        var repo = Substitute.For<ICashPaymentRepository>();
        var result = await new CashPaymentService(repo, new CashPaymentRequestValidator()).ReceiveAsync(1, 9, role, Request(), null);
        Assert.Equal(Error.Forbidden, result.Error); Assert.Empty(repo.ReceivedCalls());
    }

    [Theory]
    [InlineData(RoleCodes.Admin)]
    [InlineData(RoleCodes.Receptionist)]
    public async Task CashierIdentityComesFromCurrentUser(string role)
    {
        var repo = Substitute.For<ICashPaymentRepository>(); var request = Request();
        repo.ReceiveAsync(1, 9, request, null, Arg.Any<CancellationToken>()).Returns(Result<PaymentTransaction>.Success(new() {
            Id = 7, InvoiceId = 1, Amount = 10000, AmountTendered = 20000, ChangeAmount = 10000, CashierId = 9 }));
        var result = await new CashPaymentService(repo, new CashPaymentRequestValidator()).ReceiveAsync(1, 9, role, request, null);
        Assert.True(result.IsSuccess); Assert.Equal(9, result.Value!.CashierId); Assert.Equal(10000, result.Value.ChangeAmount);
    }

    [Fact]
    public async Task InvalidInvoiceIdDoesNotReachRepository()
    {
        var repo = Substitute.For<ICashPaymentRepository>();
        Assert.Equal(Error.Validation, (await new CashPaymentService(repo, new CashPaymentRequestValidator())
            .ReceiveAsync(0, 9, RoleCodes.Admin, Request(), null)).Error);
        Assert.Empty(repo.ReceivedCalls());
    }

    [Fact]
    public async Task RepositoryConflictIsPropagated()
    {
        var repo = Substitute.For<ICashPaymentRepository>(); var request = Request();
        repo.ReceiveAsync(1, 9, request, null, Arg.Any<CancellationToken>()).Returns(Result<PaymentTransaction>.Failure(Error.Conflict));
        Assert.Equal(Error.Conflict, (await new CashPaymentService(repo, new CashPaymentRequestValidator())
            .ReceiveAsync(1, 9, RoleCodes.Admin, request, null)).Error);
    }

    [Theory]
    [InlineData(1, 9, 10, 20, true)]
    [InlineData(2, 9, 10, 20, false)]
    [InlineData(1, 8, 10, 20, false)]
    [InlineData(1, 9, 11, 20, false)]
    [InlineData(1, 9, 10, 21, false)]
    public void RetryRequiresIdenticalInvoiceActorAndAmounts(int invoiceId, int cashier, decimal amount, decimal tendered, bool same)
        => Assert.Equal(same, CashPaymentRules.IsSameRequest(new PaymentTransaction { InvoiceId = 1, CashierId = 9,
            Amount = 10, AmountTendered = 20 }, invoiceId, cashier, Request(amount, tendered)));

    [Theory]
    [InlineData(EntityState.Modified)]
    [InlineData(EntityState.Deleted)]
    public async Task TransactionsAreAppendOnly(EntityState state)
    {
        await using var db = new DentalDbContext(new DbContextOptionsBuilder<DentalDbContext>().UseNpgsql("Host=localhost;Database=unused").Options);
        var payment = new PaymentTransaction { Id = 1 }; db.Attach(payment); db.Entry(payment).State = state;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }
}
