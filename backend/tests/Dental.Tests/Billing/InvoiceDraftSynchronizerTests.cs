using Dental.Application.Features.MOD_BIL.Services;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Xunit;

namespace Dental.Tests.Billing;

public sealed class InvoiceDraftSynchronizerTests
{
    private static VisitService Row(int id = 1, int quantity = 1, decimal price = 100)
        => new() { Id = id, ServiceCode = "S", ServiceName = "Snapshot", Quantity = quantity, UnitPrice = price };

    [Fact]
    public void NewDraft_UsesActualServicesAndPreservesIdentity()
    {
        var invoice = new Invoice { Id = 9, InvoiceCode = "INV-20261011-0001" };
        var result = InvoiceDraftSynchronizer.Apply(invoice, [Row(quantity: 2)]);
        Assert.True(result.IsSuccess); Assert.True(result.Value);
        Assert.Equal(200m, invoice.TotalAmount);
        Assert.Equal(9, invoice.Id); Assert.Equal("INV-20261011-0001", invoice.InvoiceCode);
        Assert.Equal(InvoiceStatus.Draft, invoice.Status);
    }

    [Fact]
    public void AddedRemovedChangedServices_ReplacesSnapshotAndRecalculates()
    {
        var invoice = new Invoice();
        InvoiceDraftSynchronizer.Apply(invoice, [Row(1), Row(2)]);
        var result = InvoiceDraftSynchronizer.Apply(invoice, [Row(2, 3, 123), Row(3, 2, 10)]);
        Assert.True(result.Value);
        Assert.Equal(new[] { 2, 3 }, invoice.Items.Select(x => x.SourceVisitServiceId));
        Assert.Equal(389m, invoice.TotalAmount);
    }

    [Fact]
    public void UnchangedOrReorderedRows_KeepStoredItemIds()
    {
        var invoice = new Invoice();
        InvoiceDraftSynchronizer.Apply(invoice, [Row(1), Row(2)]);
        var oldItems = invoice.Items; oldItems.First().Id = 77;
        Assert.False(InvoiceDraftSynchronizer.Apply(invoice, [Row(2), Row(1)]).Value);
        Assert.Same(oldItems, invoice.Items); Assert.Equal(77, invoice.Items.First().Id);
    }

    [Theory]
    [InlineData(InvoiceStatus.PendingPayment, 0)]
    [InlineData(InvoiceStatus.PartiallyPaid, 10)]
    [InlineData(InvoiceStatus.Paid, 100)]
    [InlineData(InvoiceStatus.Cancelled, 0)]
    [InlineData(InvoiceStatus.Draft, 1)]
    public void NonEditableInvoice_IsUnchanged(InvoiceStatus status, int paid)
    {
        var invoice = new Invoice { Status = status, PaidAmount = paid, TotalAmount = 900 };
        Assert.True(InvoiceDraftSynchronizer.Apply(invoice, [Row()]).IsFailure);
        Assert.Equal(900m, invoice.TotalAmount); Assert.Empty(invoice.Items);
    }

    [Fact]
    public void InvalidServices_DoNotPartiallyOverwriteExistingSnapshot()
    {
        var invoice = new Invoice(); InvoiceDraftSynchronizer.Apply(invoice, [Row()]);
        var items = invoice.Items;
        Assert.True(InvoiceDraftSynchronizer.Apply(invoice, [Row(2), Row(3, 0)]).IsFailure);
        Assert.Same(items, invoice.Items); Assert.Equal(100m, invoice.TotalAmount);
    }

    [Fact]
    public void EmptyServices_RemoveOldLinesAndClearTotal()
    {
        var invoice = new Invoice(); InvoiceDraftSynchronizer.Apply(invoice, [Row()]);
        Assert.True(InvoiceDraftSynchronizer.Apply(invoice, []).Value);
        Assert.Empty(invoice.Items); Assert.Equal(0m, invoice.TotalAmount);
    }
}
