using Dental.Application.Features.MOD_RPT.DTOs;

namespace Dental.Application.Features.MOD_RPT.Services;

/// <summary>DL-268/275: immutable line snapshots, whole VND, stable receipt allocation.</summary>
public static class RevenueServiceAllocator
{
    public static IReadOnlyList<RevenueService> Allocate(IReadOnlyList<RevenueLine> lines, decimal discount,
        string? discountType, IReadOnlyList<RevenueReceipt> receipts, RevenueReportFilter filter)
    {
        // Stable grouping keeps renames as separate historical snapshots, and counts quantities once per invoice/period.
        var groups = lines.GroupBy(x => (x.Type, x.Code, x.Name)).OrderBy(g => g.Key.Type, StringComparer.Ordinal)
            .ThenBy(g => g.Key.Code, StringComparer.Ordinal).ThenBy(g => g.Key.Name, StringComparer.Ordinal)
            .Select(g => new RevenueLine(g.Key.Type, g.Key.Code, g.Key.Name, g.Sum(x => x.Quantity), g.Sum(x => x.Amount))).ToArray();
        var eligible = groups.Select(x => discountType == "Percentage" && x.Type != "Service" ? 0m : x.Amount).ToArray();
        var discounts = Split(discount, eligible);
        var net = groups.Select((x, i) => x.Amount - discounts[i]).ToArray();
        var total = net.Sum();
        if (total == 0) return [];
        var revenue = new decimal[groups.Length];
        var remaining = net.ToArray();
        foreach (var receipt in receipts.OrderBy(x => x.PaidAt).ThenBy(x => x.Id))
        {
            // Split against remaining balances so rounding cannot make a later receipt negative.
            // Always process all receipts, regardless of the requested period/cashier.
            var allocated = Split(receipt.Amount, remaining);
            if (receipt.PaidAt >= filter.FromUtc && receipt.PaidAt < filter.UntilUtc &&
                (!filter.CashierId.HasValue || receipt.CashierId == filter.CashierId))
                for (var i = 0; i < revenue.Length; i++) revenue[i] += allocated[i];
            for (var i = 0; i < remaining.Length; i++) remaining[i] -= allocated[i];
        }
        return groups.Select((x, i) => new RevenueService(x.Code, x.Name, x.Quantity, revenue[i]))
            .Where((_, i) => groups[i].Type == "Service" && revenue[i] > 0).ToArray();
    }

    public static decimal[] Split(decimal amount, IReadOnlyList<decimal> weights)
    {
        var sum = weights.Sum();
        if (amount < 0 || amount > sum || amount != decimal.Truncate(amount) || weights.Any(w => w < 0 || w != decimal.Truncate(w)))
            throw new InvalidOperationException("Invalid financial snapshot for revenue allocation.");
        if (sum == 0) return new decimal[weights.Count];
        var allocated = weights.Select(w => Math.Round(amount * (w / sum), MidpointRounding.AwayFromZero)).ToArray();
        var remainder = amount - allocated.Sum();
        // Prefer the largest group; if rounding many tiny groups exhausts it, continue in stable descending order.
        foreach (var i in Enumerable.Range(0, weights.Count).OrderByDescending(i => weights[i]).ThenBy(i => i))
        {
            var delta = remainder > 0 ? Math.Min(remainder, weights[i] - allocated[i]) : -Math.Min(-remainder, allocated[i]);
            allocated[i] += delta; remainder -= delta;
            if (remainder == 0) break;
        }
        return allocated;
    }
}
