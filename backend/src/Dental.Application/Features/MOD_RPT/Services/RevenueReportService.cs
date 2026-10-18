using System.Globalization;
using Dental.Application.Common;
using Dental.Application.Features.MOD_RPT.DTOs;
using Dental.Application.Features.MOD_RPT.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;

namespace Dental.Application.Features.MOD_RPT.Services;

public sealed class RevenueReportService(IRevenueReportRepository repository, VietnamClock clock)
{
    public async Task<Result<RevenueReportDto>> GetAsync(string? role, RevenueReportRequest request, CancellationToken ct = default)
    {
        if (role != RoleCodes.Admin) return Result<RevenueReportDto>.Failure(Error.Forbidden);
        var first = new DateOnly(clock.Today.Year, clock.Today.Month, 1);
        request = request with { FromDate = request.FromDate ?? first, ToDate = request.ToDate ?? first.AddMonths(1).AddDays(-1) };
        var validation = await new RevenueReportRequestValidator().ValidateAsync(request, ct);
        if (!validation.IsValid) return Result<RevenueReportDto>.Failure("RPT_001", "Bộ lọc không hợp lệ: ngày bắt đầu ≤ ngày kết thúc, tối đa 366 ngày; kiểm tra nhóm thời gian, Top và thu ngân.");
        var days = request.ToDate!.Value.DayNumber - request.FromDate!.Value.DayNumber + 1;
        var group = request.GroupBy == "Auto" ? days <= 31 ? "Day" : days <= 90 ? "Week" : "Month" : request.GroupBy;
        var filter = new RevenueReportFilter(request.FromDate.Value, request.ToDate.Value, group, request.Top, request.SortBy, request.CashierId);
        var data = await repository.ReadAsync(filter, ct);
        var daily = Enumerable.Range(0, days).Select(i => filter.FromDate.AddDays(i))
            .Select(d => data.Days.SingleOrDefault(x => x.Date == d) ?? new RevenueDay(d, 0, 0, 0, 0)).ToArray();
        string Label(DateOnly date) => group switch
        {
            "Month" => date.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            "Week" => $"{ISOWeek.GetYear(date.ToDateTime(TimeOnly.MinValue))}-W{ISOWeek.GetWeekOfYear(date.ToDateTime(TimeOnly.MinValue)):D2}",
            _ => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        };
        var timeline = daily.GroupBy(x => Label(x.Date)).Select(g => new RevenuePeriod(g.Key, g.Sum(x => x.Revenue), g.Sum(x => x.TransactionCount))).ToArray();
        var services = data.Services.GroupBy(x => (x.ServiceCode, x.ServiceName))
            .Select(g => new RevenueService(g.Key.ServiceCode, g.Key.ServiceName, g.Sum(x => x.UsageCount), g.Sum(x => x.Revenue)));
        var sorted = request.SortBy == "UsageCount" ? services.OrderByDescending(x => x.UsageCount).ThenByDescending(x => x.Revenue) : services.OrderByDescending(x => x.Revenue).ThenByDescending(x => x.UsageCount);
        var top = sorted.ThenBy(x => x.ServiceCode, StringComparer.Ordinal).ThenBy(x => x.ServiceName, StringComparer.Ordinal);
        return Result<RevenueReportDto>.Success(new(filter, clock.UtcNow, data.Summary, daily, timeline, data.Methods, data.Cashiers,
            (request.Top == "All" ? top : top.Take(int.Parse(request.Top, CultureInfo.InvariantCulture))).ToArray()));
    }
}
