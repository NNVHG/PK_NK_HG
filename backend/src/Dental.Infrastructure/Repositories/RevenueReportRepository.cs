using System.Data;
using Dental.Application.Features.MOD_RPT.DTOs;
using Dental.Application.Features.MOD_RPT.Services;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Repositories;

public sealed class RevenueReportRepository(DentalDbContext db) : IRevenueReportRepository
{
    public async Task<RevenueReportData> ReadAsync(RevenueReportFilter filter, CancellationToken ct = default)
    {
        // One PostgreSQL snapshot across all aggregates, even if another cashier commits during this read.
        await using var snapshot = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var payments = db.PaymentTransactions.AsNoTracking().Where(p => p.Invoice.Status != InvoiceStatus.Draft &&
            p.PaidAt >= filter.FromUtc && p.PaidAt < filter.UntilUtc && (!filter.CashierId.HasValue || p.CashierId == filter.CashierId));
        var days = await payments.GroupBy(p => DateOnly.FromDateTime(p.PaidAt.AddHours(7)))
            .Select(g => new RevenueDay(g.Key, g.Sum(p => p.Amount), g.Count(), g.Sum(p => p.Amount > 0 ? p.Amount : 0), -g.Sum(p => p.Amount < 0 ? p.Amount : 0))).ToListAsync(ct);
        var methodRows = await payments.GroupBy(p => p.PaymentMethod).Select(g => new
        { Method = g.Key, Gross = g.Sum(p => p.Amount > 0 ? p.Amount : 0), Refund = -g.Sum(p => p.Amount < 0 ? p.Amount : 0) }).ToListAsync(ct);
        var gross = methodRows.Sum(x => x.Gross);
        var refunds = methodRows.Sum(x => x.Refund);
        var methods = methodRows.OrderBy(x => x.Method, StringComparer.Ordinal).Select(x => new RevenueMethod(x.Method, x.Gross - x.Refund, x.Gross, x.Refund,
            gross == 0 ? 0 : Math.Round(x.Gross / gross * 100, 2, MidpointRounding.AwayFromZero))).ToArray();
        if (gross > 0)
        {
            var largest = Array.FindIndex(methods, x => x.GrossRevenue == methods.Max(m => m.GrossRevenue));
            methods[largest] = methods[largest] with { Percentage = methods[largest].Percentage + 100 - methods.Sum(x => x.Percentage) };
        }
        var cashiers = await payments.GroupBy(p => new { p.CashierId, p.Cashier.FullName })
            .OrderBy(g => g.Key.CashierId).Select(g => new RevenueCashier(g.Key.CashierId, g.Key.FullName, g.Sum(p => p.Amount), g.Count())).ToListAsync(ct);
        var issued = db.Invoices.AsNoTracking().Where(i => i.Status == InvoiceStatus.PendingPayment || i.Status == InvoiceStatus.PartiallyPaid || i.Status == InvoiceStatus.Paid || i.Status == InvoiceStatus.PartiallyRefunded || i.Status == InvoiceStatus.Refunded);
        var periodInvoices = issued.Where(i => i.CreatedAt >= filter.FromUtc && i.CreatedAt < filter.UntilUtc);
        var invoiced = await periodInvoices.SumAsync(i => i.TotalAmount - i.DiscountAmount, ct);
        var discount = await periodInvoices.SumAsync(i => i.DiscountAmount, ct);
        var outstanding = await issued.Where(i => i.Status != InvoiceStatus.Paid).SumAsync(i =>
            i.TotalAmount - i.DiscountAmount > i.PaidAmount ? i.TotalAmount - i.DiscountAmount - i.PaidAmount : 0, ct);
        var temporary = await payments.Where(p => p.Amount > 0 && (p.Invoice.Status == InvoiceStatus.PartiallyPaid || p.Invoice.Status == InvoiceStatus.PartiallyRefunded && p.Invoice.TotalAmount - p.Invoice.DiscountAmount > p.Invoice.PaidAmount)).SumAsync(p => p.Amount, ct);
        // The model has no CancelledAt; explicitly report creation-period invoices currently cancelled before any collection.
        var cancelled = await db.Invoices.CountAsync(i => i.CreatedAt >= filter.FromUtc && i.CreatedAt < filter.UntilUtc && i.Status == InvoiceStatus.Cancelled &&
            i.PaidAmount == 0 && !db.PaymentTransactions.Any(p => p.InvoiceId == i.Id && p.Amount > 0), ct);
        var candidates = await issued.Where(i => i.Status == InvoiceStatus.Paid && i.Visit.Status == VisitStatuses.Completed &&
            payments.Any(p => p.InvoiceId == i.Id && p.Amount > 0) && !db.PaymentTransactions.Any(p => p.InvoiceId == i.Id && p.Amount < 0))
            .Select(i => new { i.Id, i.TotalAmount, i.DiscountAmount, i.DiscountType }).ToListAsync(ct);
        var ids = candidates.Select(i => i.Id).ToArray();
        var items = await db.InvoiceItems.AsNoTracking().Where(i => ids.Contains(i.InvoiceId))
            .Select(i => new { i.InvoiceId, Line = new RevenueLine(i.ItemType, i.Code, i.Name, i.Quantity, i.TotalAmount) }).ToListAsync(ct);
        var receipts = await db.PaymentTransactions.AsNoTracking().Where(p => ids.Contains(p.InvoiceId) && p.Amount > 0)
            .Select(p => new { p.InvoiceId, Receipt = new RevenueReceipt(p.Id, p.PaidAt, p.Amount, p.CashierId) }).ToListAsync(ct);
        var services = new List<RevenueService>();
        var itemLookup = items.ToLookup(x => x.InvoiceId, x => x.Line);
        var receiptLookup = receipts.ToLookup(x => x.InvoiceId, x => x.Receipt);
        foreach (var invoice in candidates)
        {
            var lines = itemLookup[invoice.Id].ToArray();
            if (lines.Sum(x => x.Amount) != invoice.TotalAmount || receiptLookup[invoice.Id].Sum(x => x.Amount) != invoice.TotalAmount - invoice.DiscountAmount)
                throw new InvalidOperationException("Inconsistent settled invoice snapshot for revenue reporting.");
            services.AddRange(RevenueServiceAllocator.Allocate(lines, invoice.DiscountAmount, invoice.DiscountType, receiptLookup[invoice.Id].ToArray(), filter));
        }
        await snapshot.CommitAsync(ct);
        return new(new(gross, refunds, gross - refunds, invoiced, discount, outstanding, temporary, cancelled), days, methods, cashiers, services);
    }
}
