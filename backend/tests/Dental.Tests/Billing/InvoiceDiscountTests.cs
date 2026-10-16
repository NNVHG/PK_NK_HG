using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Services;
using Dental.Application.Features.MOD_BIL.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using NSubstitute;
using Xunit;

namespace Dental.Tests.Billing;

public sealed class InvoiceDiscountTests
{
    private static Invoice Invoice(decimal services = 1000000, decimal medicines = 200000) => new()
    {
        Id = 7, TotalAmount = services + medicines, Status = InvoiceStatus.PendingPayment,
        Visit = new() { Status = VisitStatuses.Completed, IsLocked = true },
        Items = [new() { ItemType = "Service", Quantity = 1, UnitPrice = services, TotalAmount = services },
                 new() { ItemType = "Medicine", Quantity = 1, UnitPrice = medicines, TotalAmount = medicines }]
    };
    private static DiscountPreset Preset(decimal value = 25, string type = "Percentage")
        => new() { Id = 4, Code = "SYNTHETIC", DiscountType = type, Value = value, IsActive = true };
    private static ApplyInvoiceDiscountRequest Request(decimal expected = 250000, decimal total = 1200000)
        => new("Preset", 4, null, null, "Synthetic discount reason", total, 0, expected);

    [Theory]
    [InlineData(101, 10, 10)]
    [InlineData(105, 10, 11)]
    [InlineData(109, 10, 11)]
    [InlineData(999, 12.5, 125)]
    [InlineData(1, 50, 1)]
    public void Percentage_ExcludesMedicinesAndRoundsToOneDong(decimal services, decimal percent, decimal expected)
    {
        var invoice = Invoice(services, 987654);
        Assert.Equal(expected, InvoiceDiscountRules.Calculate(invoice, "Percentage", percent).Value);
    }

    [Fact]
    public void Percentage_UsesQuantityAndFullPrecisionAtDatabaseMaximum()
    {
        var invoice = Invoice(999999999999999999m, 0);
        Assert.Equal(333300000000000000m, InvoiceDiscountRules.Calculate(invoice, "Percentage", 33.33m).Value);
        invoice = Invoice(210, 17); invoice.Items.First().Quantity = 2; invoice.Items.First().UnitPrice = 105;
        Assert.Equal(21m, InvoiceDiscountRules.Calculate(invoice, "Percentage", 10).Value);
    }

    [Theory]
    [InlineData("Percentage", 0)]
    [InlineData("Percentage", 100.01)]
    [InlineData("Percentage", 2.333)]
    [InlineData("FixedAmount", 1.5)]
    [InlineData("Unknown", 10)]
    public void InvalidValue_IsRejected(string type, decimal value) => Assert.False(InvoiceDiscountRules.ValidValue(type, value));

    [Fact]
    public void FixedAmount_CannotExceedTotal_ButCanReachZeroPayable()
    {
        var invoice = Invoice();
        Assert.True(InvoiceDiscountRules.Calculate(invoice, "FixedAmount", 1200001).IsFailure);
        Assert.Equal(1200000m, InvoiceDiscountRules.Calculate(invoice, "FixedAmount", 1200000).Value);
    }

    [Fact]
    public void ConfirmedFullDiscount_SettlesZeroBalanceWithoutIncreasingPaidAmount()
    {
        var invoice = Invoice();
        var request = Request(1200000) with { Mode = "Custom", PresetId = null, DiscountType = "FixedAmount", Value = 1200000 };
        Assert.True(InvoiceDiscountRules.Apply(invoice, null, request, RoleCodes.Admin).IsSuccess);
        Assert.Equal(0, invoice.FinalAmount); Assert.Equal(0, invoice.PaidAmount); Assert.Equal(InvoiceStatus.Paid, invoice.Status);
    }

    [Fact]
    public void Receptionist_PresetWithinBothLimits_SnapshotsDiscountWithoutChangingLinePrices()
    {
        var invoice = Invoice(); var original = invoice.Items.First().UnitPrice;
        Assert.True(InvoiceDiscountRules.Apply(invoice, Preset(), Request(), RoleCodes.Receptionist).IsSuccess);
        Assert.Equal(250000, invoice.DiscountAmount); Assert.Equal(950000, invoice.FinalAmount);
        Assert.Equal(1200000, invoice.TotalAmount); Assert.Equal(original, invoice.Items.First().UnitPrice);
        Assert.Equal(4, invoice.DiscountPresetId); Assert.Equal(25, invoice.DiscountValue);
    }

    [Theory]
    [InlineData(2000000, 25, 500000, true)]
    [InlineData(2000004, 25, 500001, false)]
    [InlineData(1000000, 25.01, 250100, false)]
    public void Receptionist_BoundariesAreInclusiveAndExcessRequiresAdmin(int services, double percent, int expected, bool allowed)
    {
        var invoice = Invoice(services, 0);
        var result = InvoiceDiscountRules.Apply(invoice, Preset((decimal)percent), Request(expected, services), RoleCodes.Receptionist);
        Assert.Equal(allowed, result.IsSuccess);
        if (!allowed) { Assert.Equal("BIL_023", result.Error.Code); Assert.Equal(0, invoice.DiscountAmount); }
    }

    [Theory]
    [InlineData(250000, true)]
    [InlineData(250001, false)]
    public void Receptionist_FixedDiscountAlsoRespectsPercentageCap(int amount, bool allowed)
    {
        var invoice = Invoice(1000000, 0);
        Assert.Equal(allowed, InvoiceDiscountRules.Apply(invoice, Preset(amount, "FixedAmount"), Request(amount, 1000000), RoleCodes.Receptionist).IsSuccess);
    }

    [Fact]
    public void CustomDiscount_RequiresAdminEvenUnderLimits()
    {
        var request = Request(100) with { Mode = "Custom", PresetId = null, DiscountType = "FixedAmount", Value = 100 };
        var invoice = Invoice();
        Assert.Equal("GEN_002", InvoiceDiscountRules.Apply(invoice, null, request, RoleCodes.Receptionist).Error.Code);
        Assert.Equal(0, invoice.DiscountAmount);
        Assert.True(InvoiceDiscountRules.Apply(invoice, null, request, RoleCodes.Admin).IsSuccess);
        Assert.Null(invoice.DiscountPresetId);
    }

    [Fact]
    public void Admin_CanApplyAboveReceptionistLimitsAndRemoveWithAnAuditReason()
    {
        var invoice = Invoice();
        Assert.True(InvoiceDiscountRules.Apply(invoice, Preset(75), Request(750000), RoleCodes.Admin).IsSuccess);
        var remove = Request(0) with { Mode = "Remove", PresetId = null, ExpectedCurrentDiscountAmount = 750000, Reason = " Remove synthetic discount " };
        Assert.True(InvoiceDiscountRules.Apply(invoice, null, remove, RoleCodes.Receptionist).IsSuccess);
        Assert.Equal(0, invoice.DiscountAmount); Assert.Null(invoice.DiscountType); Assert.Null(invoice.DiscountValue);
        Assert.Null(invoice.DiscountPresetId); Assert.Equal("Remove synthetic discount", invoice.DiscountReason);
    }

    [Theory]
    [InlineData(InvoiceStatus.Draft, 0)]
    [InlineData(InvoiceStatus.PartiallyPaid, 1)]
    [InlineData(InvoiceStatus.Paid, 1200000)]
    [InlineData(InvoiceStatus.Cancelled, 0)]
    [InlineData(InvoiceStatus.PendingPayment, 1)]
    public void FinanciallyNonEditableInvoices_AreUnchanged(InvoiceStatus status, int paid)
    {
        var invoice = Invoice(); invoice.Status = status; invoice.PaidAmount = paid;
        Assert.Equal("BIL_021", InvoiceDiscountRules.Apply(invoice, Preset(), Request(), RoleCodes.Admin).Error.Code);
        Assert.Equal(0, invoice.DiscountAmount);
    }

    [Fact]
    public void StaleInvoiceOrPresetAndInactivePreset_DoNotChangeDiscount()
    {
        var invoice = Invoice();
        foreach (var request in new[] { Request() with { ExpectedTotalAmount = 1 }, Request() with { ExpectedCurrentDiscountAmount = 1 }, Request(1) })
            Assert.Equal("BIL_006", InvoiceDiscountRules.Apply(invoice, Preset(), request, RoleCodes.Admin).Error.Code);
        var preset = Preset(); preset.IsActive = false;
        Assert.Equal("BIL_022", InvoiceDiscountRules.Apply(invoice, preset, Request(), RoleCodes.Admin).Error.Code);
        Assert.Equal(0, invoice.DiscountAmount);
    }

    [Fact]
    public void InvalidSnapshotAndUnfinishedVisit_AreRejected()
    {
        var invoice = Invoice(); invoice.Items.First().TotalAmount++;
        Assert.Equal("BIL_024", InvoiceDiscountRules.Calculate(invoice, "Percentage", 25).Error.Code);
        invoice = Invoice(); invoice.Visit.IsLocked = false;
        Assert.Equal("BIL_021", InvoiceDiscountRules.Apply(invoice, Preset(), Request(), RoleCodes.Admin).Error.Code);
    }

    [Fact]
    public void CashAndBankChargeOnlyTheDiscountedBalance()
    {
        var invoice = Invoice(); invoice.DiscountAmount = 250000;
        Assert.True(CashPaymentRules.Apply(invoice, 950001).IsFailure);
        Assert.True(CashPaymentRules.Apply(invoice, 450000).IsSuccess);
        Assert.Equal(InvoiceStatus.PartiallyPaid, invoice.Status);
        Assert.True(BankTransferRules.Apply(invoice, 600000).IsSuccess);
        Assert.Equal(950000, invoice.PaidAmount); Assert.Equal(InvoiceStatus.Paid, invoice.Status);
    }

    [Fact]
    public async Task ValidatorsAndService_RejectInvalidInputsBeforeRepository()
    {
        var repository = Substitute.For<IInvoiceDiscountRepository>();
        var service = new InvoiceDiscountService(repository, new(), new());
        foreach (var role in new[] { RoleCodes.Patient, RoleCodes.Dentist, RoleCodes.Assistant })
            Assert.Equal("GEN_002", (await service.ApplyAsync(7, Request(), 1, role, null)).Error.Code);
        Assert.True((await service.ApplyAsync(0, Request(), 1, RoleCodes.Admin, null)).IsFailure);
        Assert.True((await service.ApplyAsync(7, Request() with { Reason = " " }, 1, RoleCodes.Admin, null)).IsFailure);
        Assert.True((await service.ApplyAsync(7, Request() with { Value = 5 }, 1, RoleCodes.Admin, null)).IsFailure);
        Assert.True((await service.SavePresetAsync(null, new("BAD CODE", "Synthetic", "Percentage", 5, true), 1, RoleCodes.Admin, null)).IsFailure);
        Assert.True((await service.SavePresetAsync(null, new("TEST", "Synthetic", "Percentage", 5, true), 1, RoleCodes.Receptionist, null)).IsFailure);
        Assert.Empty(repository.ReceivedCalls());
    }
}
