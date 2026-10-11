using Dental.Api.Controllers;
using Dental.Api.Policies;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Services;
using Dental.Application.Features.MOD_BIL.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace Dental.Tests.Billing;

public sealed class BankTransferTests
{
    [Theory]
    [InlineData("PKNK INV-20261011-0001", "INV-20261011-0001")]
    [InlineData("bank note pknk inv-20261011-0001 received", "INV-20261011-0001")]
    [InlineData("INV-20261011-0001", null)]
    [InlineData("PKNK INV-20261011-00010", null)]
    [InlineData("PKNK INV-20261011-0001 PK NK", "INV-20261011-0001")]
    [InlineData("PKNK INV-20261011-0001 PKNK INV-20261011-0002", null)]
    [InlineData("", null)]
    public void ExtractsOneUnambiguousCode(string content, string? expected) => Assert.Equal(expected, BankTransferRules.InvoiceCode(content));

    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(1.5, false)]
    [InlineData(10, true)]
    public void ValidatesAmount(decimal amount, bool allowed)
        => Assert.Equal(allowed, new BankTransferRequestValidator().Validate(new BankTransferRequest("PKNK INV-20261011-0001", amount, "BANK001")).IsValid);

    [Theory]
    [InlineData("short", false)]
    [InlineData("BANK001", true)]
    [InlineData("BANK001 bad", false)]
    [InlineData("123456789012345678901", false)]
    public void ValidatesReference(string reference, bool allowed)
        => Assert.Equal(allowed, new BankTransferRequestValidator().Validate(new BankTransferRequest("PKNK INV-20261011-0001", 10, reference)).IsValid);

    [Theory]
    [InlineData(20, 60, InvoiceStatus.PartiallyPaid)]
    [InlineData(60, 100, InvoiceStatus.Paid)]
    [InlineData(80, 100, InvoiceStatus.Paid)]
    public void CreditsOnlyRemainingDebt(decimal received, decimal expected, InvoiceStatus status)
    {
        var invoice = new Invoice { TotalAmount = 100, PaidAmount = 40, Status = InvoiceStatus.PartiallyPaid,
            Visit = new Visit { Status = VisitStatuses.Completed, IsLocked = true } };
        Assert.True(BankTransferRules.Apply(invoice, received).IsSuccess);
        Assert.Equal(expected, invoice.PaidAmount); Assert.Equal(status, invoice.Status);
    }

    [Theory]
    [InlineData(InvoiceStatus.Draft)]
    [InlineData(InvoiceStatus.Paid)]
    [InlineData(InvoiceStatus.Cancelled)]
    public void RejectsNonPayableInvoice(InvoiceStatus status)
    {
        var invoice = new Invoice { TotalAmount = 100, Status = status, Visit = new Visit { Status = VisitStatuses.Completed, IsLocked = true } };
        Assert.True(BankTransferRules.Apply(invoice, 20).IsFailure); Assert.Equal(0, invoice.PaidAmount);
    }

    [Fact]
    public void SourceSeparatesSimulationAndActualReplayKeys()
    {
        Assert.Equal(BankTransferRules.RequestId("Webhook", "BANK001"), BankTransferRules.RequestId("Webhook", "BANK001"));
        Assert.NotEqual(BankTransferRules.RequestId("Webhook", "BANK001"), BankTransferRules.RequestId("Simulation", "BANK001"));
        Assert.Null(BankTransferRules.ExcessNote(0)); Assert.Contains("hoàn lại", BankTransferRules.ExcessNote(20));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("Bearer invalid", false)]
    [InlineData("Apikey wrong", false)]
    [InlineData("Apikey 01234567890123456789012345678901", true)]
    public void RequiresCorrectApiKey(string? header, bool allowed)
        => Assert.Equal(allowed, BankWebhookAuthenticationHandler.KeyMatches("01234567890123456789012345678901", header));

    [Fact]
    public void MissingOrWeakWebhookSecretFailsClosed()
    {
        Assert.False(BankWebhookAuthenticationHandler.KeyMatches(null, "Apikey null"));
        Assert.False(BankWebhookAuthenticationHandler.KeyMatches("short", "Apikey short"));
    }

    [Fact]
    public async Task SimulationIsDisabledInProductionEvenForAuthenticatedActor()
    {
        var repository = Substitute.For<IBankTransferRepository>(); var environment = Substitute.For<IWebHostEnvironment>();
        environment.EnvironmentName.Returns("Production");
        var controller = new BankTransferWebhookController(new(repository, new BankTransferRequestValidator()), repository,
            Substitute.For<ICurrentUser>(), environment);
        Assert.IsType<NotFoundResult>(await controller.Simulate(new("INV-20261011-0001", 10, "BANK001"), default));
        Assert.Empty(repository.ReceivedCalls());
    }
}
