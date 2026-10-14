using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Services;
using Dental.Application.Features.MOD_BIL.Validators;
using Dental.Application.Interfaces;
using Xunit;
namespace Dental.Tests.Billing;
public sealed class InvoiceHistoryDateTests
{
    private sealed class Repository : IInvoiceHistoryRepository
    {
        public int Calls; public DateTime? From, Until;
        public Task<PagedResult<InvoiceLookupResponse>> GetPageAsync(int? number, int? status, int page, int size,
            DateTime? fromUtc = null, DateTime? untilUtc = null, CancellationToken ct = default)
        { Calls++; From = fromUtc; Until = untilUtc; return Task.FromResult(PagedResult<InvoiceLookupResponse>.Create([], 0, page, size)); }
    }
    [Theory] [InlineData("ADMIN")] [InlineData("RECEPTIONIST")]
    public async Task Same_day_includes_all_Vietnam_hours_with_half_open_UTC_bounds(string role)
    {
        var r = new Repository(); var day = new DateOnly(2026, 10, 14);
        var result = await new InvoiceHistoryService(r, new()).GetAsync(new() { FromDate = day, ToDate = day }, role);
        Assert.True(result.IsSuccess); Assert.Equal(new DateTime(2026, 10, 13, 17, 0, 0, DateTimeKind.Utc), r.From);
        Assert.Equal(new DateTime(2026, 10, 14, 17, 0, 0, DateTimeKind.Utc), r.Until);
        Assert.Equal(DateTimeKind.Utc, r.From!.Value.Kind); Assert.Equal(DateTimeKind.Utc, r.Until!.Value.Kind);
    }
    [Theory]
    [InlineData(null, null)] [InlineData("2026-10-14", null)] [InlineData(null, "2026-10-14")]
    public async Task Optional_each_side_preserves_unbounded_side(string? from, string? to)
    {
        var r = new Repository(); var result = await new InvoiceHistoryService(r, new()).GetAsync(new()
            { FromDate = from is null ? null : DateOnly.Parse(from), ToDate = to is null ? null : DateOnly.Parse(to) }, "ADMIN");
        Assert.True(result.IsSuccess); Assert.Equal(from is null, r.From is null); Assert.Equal(to is null, r.Until is null);
    }
    [Theory] [InlineData("2026-10-15", "2026-10-14")]
    [InlineData("0001-01-01", null)] [InlineData(null, "9999-12-31")]
    public async Task Invalid_or_overflow_dates_never_query(string? from, string? to)
    {
        var r = new Repository(); var result = await new InvoiceHistoryService(r, new()).GetAsync(new()
            { FromDate = from is null ? null : DateOnly.Parse(from), ToDate = to is null ? null : DateOnly.Parse(to) }, "ADMIN");
        Assert.Equal("GEN_003", result.Error.Code); Assert.Equal(0, r.Calls);
    }
    [Theory] [InlineData("2024-02-29", "2024-02-29")] [InlineData("0001-01-02", "9999-12-30")]
    [InlineData("1970-01-01", "2099-12-31")] [InlineData("9999-12-31", null)] [InlineData(null, "0001-01-01")]
    public async Task Valid_leap_long_range_or_representable_edges_do_not_throw(string? from, string? to)
    {
        var r = new Repository(); var result = await new InvoiceHistoryService(r, new()).GetAsync(new()
            { FromDate = from is null ? null : DateOnly.Parse(from), ToDate = to is null ? null : DateOnly.Parse(to) }, "ADMIN");
        Assert.True(result.IsSuccess); Assert.Equal(1, r.Calls);
    }
    [Theory] [InlineData("PATIENT")] [InlineData("DENTIST")] [InlineData("ASSISTANT")]
    public async Task Dates_do_not_open_other_roles(string role)
    {
        var r = new Repository(); var result = await new InvoiceHistoryService(r, new()).GetAsync(new()
            { FromDate = new DateOnly(2026, 10, 14), ToDate = new DateOnly(2026, 10, 14) }, role);
        Assert.Equal("GEN_002", result.Error.Code); Assert.Equal(0, r.Calls);
    }
}
