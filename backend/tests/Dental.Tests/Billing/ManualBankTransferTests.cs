using Dental.Api.Controllers;
using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Services;
using Dental.Application.Features.MOD_BIL.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace Dental.Tests.Billing;

public sealed class ManualBankTransferTests
{
    private static ManualBankTransferRequest Valid() => new(Guid.NewGuid(), 100, "BANK001", 100, true);

    [Theory]
    [InlineData(0, false)] [InlineData(-1, false)] [InlineData(1.5, false)] [InlineData(1, true)] [InlineData(101, true)]
    public void ValidatesWholeDongReceived(decimal amount, bool valid)
        => Assert.Equal(valid, new ManualBankTransferRequestValidator().Validate(Valid() with { Amount = amount }).IsValid);

    [Theory]
    [InlineData(0, false)] [InlineData(-1, false)] [InlineData(1.5, false)] [InlineData(100, true)]
    public void RequiresPositiveWholeDongSnapshot(decimal amount, bool valid)
        => Assert.Equal(valid, new ManualBankTransferRequestValidator().Validate(Valid() with { ExpectedRemainingAmount = amount }).IsValid);

    [Theory]
    [InlineData("BANK001", true)] [InlineData("abc_-123", true)] [InlineData("short", false)]
    [InlineData("BANK 001", false)] [InlineData("123456789012345678901", false)] [InlineData(" BANK001", false)]
    public void ValidatesBankReference(string reference, bool valid)
        => Assert.Equal(valid, new ManualBankTransferRequestValidator().Validate(Valid() with { TransactionReference = reference }).IsValid);

    [Fact]
    public void RequiresExplicitConsentAndRequestId()
    {
        var validator = new ManualBankTransferRequestValidator();
        Assert.False(validator.Validate(Valid() with { Confirmed = false }).IsValid);
        Assert.False(validator.Validate(Valid() with { RequestId = Guid.Empty }).IsValid);
    }

    [Theory]
    [InlineData(null)] [InlineData(RoleCodes.Patient)] [InlineData(RoleCodes.Dentist)] [InlineData(RoleCodes.Assistant)]
    public async Task OtherRolesNeverReachRepository(string? role)
    {
        var repo = Substitute.For<IBankTransferRepository>();
        Assert.Equal(Error.Forbidden, (await new ManualBankTransferService(repo, new()).ConfirmAsync(1, Valid(), 2, role, null)).Error);
        Assert.Empty(repo.ReceivedCalls());
    }

    [Theory]
    [InlineData(RoleCodes.Admin)] [InlineData(RoleCodes.Receptionist)]
    public async Task AuthorizedServiceReturnsStoredReceipt(string role)
    {
        var repo = Substitute.For<IBankTransferRepository>(); var request = Valid();
        repo.ConfirmManualAsync(1, request, 2, role, null, Arg.Any<CancellationToken>()).Returns(
            Result<PaymentTransaction>.Success(new() { Id = 3, InvoiceId = 1, Amount = 100, BankReceivedAmount = 100,
                ChangeAmount = 0, Source = "Manual", TransactionReference = request.TransactionReference }));
        var result = await new ManualBankTransferService(repo, new()).ConfirmAsync(1, request, 2, role, null);
        Assert.True(result.IsSuccess); Assert.Equal(3, result.Value!.Id); Assert.Equal("Manual", result.Value.Source);
    }

    [Fact]
    public async Task InvalidInputDoesNotWrite()
    {
        var repo = Substitute.For<IBankTransferRepository>(); var service = new ManualBankTransferService(repo, new());
        Assert.True((await service.ConfirmAsync(1, Valid() with { Confirmed = false }, 2, RoleCodes.Admin, null)).IsFailure);
        Assert.True((await service.ConfirmAsync(0, Valid(), 2, RoleCodes.Admin, null)).IsFailure);
        Assert.True((await service.ConfirmAsync(1, Valid(), 0, RoleCodes.Admin, null)).IsFailure);
        Assert.Empty(repo.ReceivedCalls());
    }

    [Theory]
    [InlineData("Manual", true)] [InlineData("Webhook", true)] [InlineData("Simulation", false)] [InlineData(null, false)]
    public void OnlyActualTransfersCanBeReconciled(string? source, bool allowed)
    {
        var payment = new PaymentTransaction { InvoiceId = 1, PaymentMethod = "BankTransfer", BankReceivedAmount = 100,
            Source = source, TransactionReference = "BANK001" };
        Assert.Equal(allowed, ManualBankTransferService.MatchesActualTransfer(payment, 1, 100, "BANK001"));
        Assert.False(ManualBankTransferService.MatchesActualTransfer(payment, 2, 100, "BANK001"));
        Assert.False(ManualBankTransferService.MatchesActualTransfer(payment, 1, 101, "BANK001"));
        Assert.False(ManualBankTransferService.MatchesActualTransfer(payment, 1, 100, "bank001"));
    }

    [Fact]
    public void ApiIsJwtCashierOnlyAndNoStore()
    {
        var type = typeof(ManualBankTransfersController);
        var auth = Assert.Single(type.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal("Bearer", auth.AuthenticationSchemes); Assert.Equal("CashPaymentWrite", auth.Policy);
        Assert.Empty(type.GetCustomAttributes(typeof(AllowAnonymousAttribute), true));
        Assert.True(Assert.Single(type.GetCustomAttributes(typeof(ResponseCacheAttribute), true).Cast<ResponseCacheAttribute>()).NoStore);
        var body = Assert.Single(type.GetMethod("Confirm")!.CustomAttributes, a => a.AttributeType == typeof(RequestSizeLimitAttribute));
        Assert.Equal(8192L, body.ConstructorArguments.Single().Value);
    }
}
