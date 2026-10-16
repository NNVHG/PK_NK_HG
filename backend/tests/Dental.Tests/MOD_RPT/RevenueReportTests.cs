using System.Text;
using Dental.Application.Common;
using Dental.Application.Features.MOD_RPT.DTOs;
using Dental.Application.Features.MOD_RPT.Services;
using Dental.Application.Features.MOD_RPT.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using NSubstitute;
using Xunit;

namespace Dental.Tests.MOD_RPT;

public sealed class RevenueReportTests
{
    private static readonly DateOnly Jan = new(2025, 1, 1);
    private static RevenueReportFilter Filter(DateOnly? from = null, DateOnly? to = null, int? cashier = null) => new(from ?? Jan, to ?? new(2025, 1, 31), "Day", "All", "Revenue", cashier);
    private static readonly RevenueReportData Empty = new(new(0, 0, 0, 0, 0, 900, 0, 0), [], [], [], []);

    [Theory]
    [InlineData(RoleCodes.Receptionist)] [InlineData(RoleCodes.Dentist)] [InlineData(RoleCodes.Assistant)] [InlineData(RoleCodes.Patient)] [InlineData(null)]
    public async Task OnlyAdminCanQuery(string? role)
    {
        var repo = Substitute.For<IRevenueReportRepository>();
        var service = new RevenueReportService(repo, new VietnamClock(TimeProvider.System));
        Assert.Equal(Error.Forbidden, (await service.GetAsync(role, new())).Error);
        Assert.Empty(repo.ReceivedCalls());
    }
    [Theory] [InlineData("GroupBy")] [InlineData("Top")] [InlineData("SortBy")] [InlineData("CashierId")] [InlineData("Reversed")] [InlineData("TooLong")] [InlineData("MinDate")] [InlineData("MaxDate")]
    public void InvalidFiltersRejected(string kind)
    {
        var q = new RevenueReportRequest(Jan, Jan);
        q = kind switch { "GroupBy" => q with { GroupBy = "Year" }, "Top" => q with { Top = "1000" }, "SortBy" => q with { SortBy = "Name" }, "CashierId" => q with { CashierId = 0 }, "Reversed" => q with { ToDate = Jan.AddDays(-1) }, "TooLong" => q with { ToDate = Jan.AddDays(367) }, "MinDate" => q with { FromDate = DateOnly.MinValue }, _ => q with { ToDate = DateOnly.MaxValue } };
        Assert.False(new RevenueReportRequestValidator().Validate(q).IsValid);
    }
    [Fact] public void Approved366DayDifferenceAccepted() => Assert.True(new RevenueReportRequestValidator().Validate(new RevenueReportRequest(Jan, Jan.AddDays(366))).IsValid);
    [Fact] public void RangeIsHalfOpenVietnamTime()
    {
        var f = Filter(); Assert.Equal(new DateTime(2024, 12, 31, 17, 0, 0, DateTimeKind.Utc), f.FromUtc);
        Assert.Equal(new DateTime(2025, 1, 31, 17, 0, 0, DateTimeKind.Utc), f.UntilUtc);
    }
    [Theory] [InlineData(31, "Day")] [InlineData(32, "Week")] [InlineData(90, "Week")] [InlineData(91, "Month")]
    public async Task AutoGranularityAndZeroDays(int days, string group)
    {
        var repo = Substitute.For<IRevenueReportRepository>(); repo.ReadAsync(Arg.Any<RevenueReportFilter>(), Arg.Any<CancellationToken>()).Returns(Empty);
        var r = (await new RevenueReportService(repo, new VietnamClock(TimeProvider.System)).GetAsync(RoleCodes.Admin, new(Jan, Jan.AddDays(days - 1)))).Value!;
        Assert.Equal(group, r.Filter.GroupBy); Assert.Equal(days, r.DailyRevenue.Count); Assert.All(r.DailyRevenue, d => Assert.Equal(0, d.Revenue)); Assert.Equal(900, r.Summary.TotalOutstanding);
    }
    [Fact] public async Task DefaultMonthAndExplicitGroup()
    {
        var repo = Substitute.For<IRevenueReportRepository>(); repo.ReadAsync(Arg.Any<RevenueReportFilter>(), Arg.Any<CancellationToken>()).Returns(Empty);
        var clock = new VietnamClock(TimeProvider.System);
        var r = (await new RevenueReportService(repo, clock).GetAsync(RoleCodes.Admin, new(GroupBy: "Week"))).Value!;
        Assert.Equal(1, r.Filter.FromDate.Day); Assert.Equal(clock.Today.Month, r.Filter.FromDate.Month); Assert.Equal("Week", r.Filter.GroupBy);
        Assert.Equal(DateTime.DaysInMonth(clock.Today.Year, clock.Today.Month), r.Filter.ToDate.Day);
    }
    [Fact] public async Task IsoWeekUsesWeekYear()
    {
        var repo = Substitute.For<IRevenueReportRepository>(); repo.ReadAsync(Arg.Any<RevenueReportFilter>(), Arg.Any<CancellationToken>()).Returns(Empty);
        var r = (await new RevenueReportService(repo, new VietnamClock(TimeProvider.System)).GetAsync(RoleCodes.Admin, new(new(2024, 12, 30), new(2025, 1, 1), GroupBy: "Week"))).Value!;
        Assert.Equal("2025-W01", Assert.Single(r.RevenueTimeline).Label);
    }
    [Theory] [InlineData("Revenue", "A")] [InlineData("UsageCount", "B")]
    public async Task GroupsServicesAndSortsTop(string sort, string first)
    {
        var repo = Substitute.For<IRevenueReportRepository>(); repo.ReadAsync(Arg.Any<RevenueReportFilter>(), Arg.Any<CancellationToken>()).Returns(Empty with { Services = [new("A", "A", 1, 100), new("A", "A", 1, 50), new("B", "B", 9, 10)] });
        var r = (await new RevenueReportService(repo, new VietnamClock(TimeProvider.System)).GetAsync(RoleCodes.Admin, new(Jan, Jan, Top: "5", SortBy: sort))).Value!;
        Assert.Equal(first, r.TopServices[0].ServiceCode); Assert.Equal(2, r.TopServices.Single(s => s.ServiceCode == "A").UsageCount);
    }
    [Fact] public async Task InvalidRequestNeverQueriesRepository()
    {
        var repo = Substitute.For<IRevenueReportRepository>(); Assert.Equal("RPT_001", (await new RevenueReportService(repo, new VietnamClock(TimeProvider.System)).GetAsync(RoleCodes.Admin, new(Jan, Jan.AddDays(367)))).Error.Code); Assert.Empty(repo.ReceivedCalls());
    }
    [Theory] [InlineData("5", 5)] [InlineData("10", 10)] [InlineData("20", 20)] [InlineData("All", 23)]
    public async Task TopLimitIsAppliedAfterAggregation(string top, int count)
    {
        var repo = Substitute.For<IRevenueReportRepository>();
        repo.ReadAsync(Arg.Any<RevenueReportFilter>(), Arg.Any<CancellationToken>()).Returns(Empty with { Services = Enumerable.Range(1, 23).Select(i => new RevenueService(i.ToString(), "Test " + i, 1, i)).ToArray() });
        var r = (await new RevenueReportService(repo, new VietnamClock(TimeProvider.System)).GetAsync(RoleCodes.Admin, new(Jan, Jan, Top: top))).Value!;
        Assert.Equal(count, r.TopServices.Count); Assert.Equal(23, r.TopServices[0].Revenue);
    }
    private static RevenueReceipt Receipt(int id, int month, decimal amount, int cashier = 1) => new(id, new DateTime(2025, month, 5, 0, 0, 0, DateTimeKind.Utc), amount, cashier);
    [Fact] public void InstallmentsRemainInActualCollectionPeriods()
    {
        RevenueLine[] lines = [new("Service", "A", "A", 1, 1000)]; RevenueReceipt[] receipts = [Receipt(1, 1, 400), Receipt(2, 2, 600)];
        Assert.Equal(400, Assert.Single(RevenueServiceAllocator.Allocate(lines, 0, null, receipts, Filter())).Revenue);
        Assert.Equal(600, Assert.Single(RevenueServiceAllocator.Allocate(lines, 0, null, receipts, Filter(new(2025, 2, 1), new(2025, 2, 28)))).Revenue);
    }
    [Fact] public void PercentageDoesNotDiscountMedicine()
    {
        RevenueLine[] lines = [new("Service", "A", "A", 1, 900), new("Medicine", "M", "M", 1, 100)];
        RevenueReceipt[] receipts = [Receipt(1, 1, 364), Receipt(2, 2, 546)];
        Assert.Equal(324, Assert.Single(RevenueServiceAllocator.Allocate(lines, 90, "Percentage", receipts, Filter())).Revenue);
        Assert.Equal(486, Assert.Single(RevenueServiceAllocator.Allocate(lines, 90, "Percentage", receipts, Filter(new(2025, 2, 1), new(2025, 2, 28)))).Revenue);
    }
    [Fact] public void CashierFilterDoesNotReallocateWholeInvoice()
    {
        RevenueLine[] lines = [new("Service", "A", "A", 2, 1000)]; RevenueReceipt[] receipts = [Receipt(1, 1, 400, 1), Receipt(2, 1, 600, 2)];
        var a = Assert.Single(RevenueServiceAllocator.Allocate(lines, 0, null, receipts, Filter(cashier: 2)));
        Assert.Equal(600, a.Revenue); Assert.Equal(2, a.UsageCount);
    }
    [Fact] public void ManyTinyGroupsPreserveMoneyWithoutNegativeAllocations()
    {
        var weights = Enumerable.Repeat(1m, 100).ToArray(); var parts = RevenueServiceAllocator.Split(50, weights);
        Assert.Equal(50, parts.Sum()); Assert.All(parts, p => Assert.InRange(p, 0, 1));
        RevenueLine[] lines = [new("Service", "A", "A", 1, 2), new("Service", "B", "B", 1, 1), new("Service", "C", "C", 1, 1)];
        RevenueReceipt[] receipts = [Receipt(1, 1, 1), Receipt(2, 1, 1), Receipt(3, 2, 2)];
        var jan = RevenueServiceAllocator.Allocate(lines, 0, null, receipts, Filter());
        var feb = RevenueServiceAllocator.Allocate(lines, 0, null, receipts, Filter(new(2025, 2, 1), new(2025, 2, 28)));
        Assert.Equal(2, jan.Sum(x => x.Revenue)); Assert.Equal(2, feb.Sum(x => x.Revenue)); Assert.All(jan.Concat(feb), s => Assert.True(s.Revenue > 0));
    }
    [Fact] public void FreeInvoiceDoesNotCreateCashRevenue() => Assert.Empty(RevenueServiceAllocator.Allocate([new("Service", "A", "A", 1, 100)], 100, "Percentage", [], Filter()));
    [Fact] public void AllocationConservesEveryReceiptAcrossRoundingCases()
    {
        for (var total = 2; total <= 80; total++)
        {
            RevenueLine[] lines = [new("Service", "A", "A", 1, total - 1), new("Service", "B", "B", 1, 1)];
            var discount = total / 3; var net = total - discount; var first = net / 2;
            RevenueReceipt[] receipts = [Receipt(1, 1, first), Receipt(2, 2, net - first)];
            var jan = RevenueServiceAllocator.Allocate(lines, discount, "Amount", receipts, Filter());
            var feb = RevenueServiceAllocator.Allocate(lines, discount, "Amount", receipts, Filter(new(2025, 2, 1), new(2025, 2, 28)));
            Assert.Equal(first, jan.Sum(s => s.Revenue)); Assert.Equal(net - first, feb.Sum(s => s.Revenue));
            Assert.All(jan.Concat(feb), s => Assert.InRange(s.Revenue, 1, total));
        }
    }
    [Theory] [InlineData(-1)] [InlineData(101)] [InlineData(1.5)]
    public void InvalidMoneyFailsExplicitly(decimal amount) => Assert.Throws<InvalidOperationException>(() => RevenueServiceAllocator.Split(amount, [100]));
    [Fact] public void CsvHasBomEscapesFieldsAndNeutralizesSpreadsheetFormulas()
    {
        var dto = new RevenueReportDto(Filter(), new DateTime(2025, 1, 1, 17, 8, 9, DateTimeKind.Utc), Empty.Summary, [], [], [], [new(1, " \t=cmd(\"x\")", -100, 1)], [new("CODE", "Tên,\"dịch vụ\"\nXuống dòng", 1, 10)]);
        var bytes = RevenueReportCsv.Create(dto); Assert.Equal(new byte[] { 239, 187, 191 }, bytes.Take(3));
        var text = Encoding.UTF8.GetString(bytes); Assert.Contains("' \t=cmd(\"\"x\"\")", text); Assert.Contains("Tên,\"\"dịch vụ\"\"\nXuống dòng", text); Assert.Contains("\"-100\"", text);
        Assert.Equal("BaoCao_DoanhThu_20250101_20250131_000809.csv", RevenueReportCsv.FileName(dto));
        Assert.Contains("\"1048\"", Encoding.UTF8.GetString(RevenueReportCsv.Create(dto with { Summary = dto.Summary with { NetRevenue = 1048.0m } })));
    }
}
