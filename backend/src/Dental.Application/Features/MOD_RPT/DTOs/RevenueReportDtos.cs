namespace Dental.Application.Features.MOD_RPT.DTOs;

public sealed record RevenueReportRequest(DateOnly? FromDate = null, DateOnly? ToDate = null,
    string GroupBy = "Auto", string Top = "10", string SortBy = "Revenue", int? CashierId = null);

public sealed record RevenueReportFilter(DateOnly FromDate, DateOnly ToDate, string GroupBy,
    string Top, string SortBy, int? CashierId)
{
    public DateTime FromUtc => DateTime.SpecifyKind(FromDate.ToDateTime(TimeOnly.MinValue).AddHours(-7), DateTimeKind.Utc);
    // Half-open range includes PostgreSQL microseconds at the very end of ToDate.
    public DateTime UntilUtc => DateTime.SpecifyKind(ToDate.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(-7), DateTimeKind.Utc);
}

public sealed record RevenueSummary(decimal GrossRevenue, decimal TotalRefunded, decimal NetRevenue,
    decimal TotalInvoiced, decimal TotalDiscount, decimal TotalOutstanding,
    decimal TemporaryCollections, int CancelledInvoiceCount)
{
    public decimal TotalRevenue => NetRevenue;
}
public sealed record RevenueDay(DateOnly Date, decimal Revenue, int TransactionCount, decimal GrossRevenue, decimal TotalRefunded);
public sealed record RevenuePeriod(string Label, decimal Revenue, int TransactionCount);
public sealed record RevenueMethod(string Method, decimal Amount, decimal GrossRevenue, decimal TotalRefunded, decimal Percentage);
public sealed record RevenueCashier(int CashierId, string Name, decimal Amount, int TransactionCount);
public sealed record RevenueService(string ServiceCode, string ServiceName, int UsageCount, decimal Revenue);
public sealed record RevenueReportDto(RevenueReportFilter Filter, DateTime GeneratedAt, RevenueSummary Summary,
    IReadOnlyList<RevenueDay> DailyRevenue, IReadOnlyList<RevenuePeriod> RevenueTimeline,
    IReadOnlyList<RevenueMethod> ByPaymentMethod, IReadOnlyList<RevenueCashier> ByCashier,
    IReadOnlyList<RevenueService> TopServices);

// Only financial snapshots cross this boundary; no patient/diagnosis/contact data is queried.
public sealed record RevenueReportData(RevenueSummary Summary, IReadOnlyList<RevenueDay> Days,
    IReadOnlyList<RevenueMethod> Methods, IReadOnlyList<RevenueCashier> Cashiers, IReadOnlyList<RevenueService> Services);
public sealed record RevenueLine(string Type, string Code, string Name, int Quantity, decimal Amount);
public sealed record RevenueReceipt(int Id, DateTime PaidAt, decimal Amount, int CashierId);
