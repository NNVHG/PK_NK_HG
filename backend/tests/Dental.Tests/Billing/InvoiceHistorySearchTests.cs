using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Services;
using Dental.Application.Features.MOD_BIL.Validators;
using Dental.Application.Interfaces;
using Xunit;
namespace Dental.Tests.Billing;
public sealed class InvoiceHistorySearchTests
{
    private sealed class Repository : IInvoiceHistoryRepository
    {
        public int Calls; public string? Keyword; public (int?, int?, int, int)? Filters; public DateTime? From, Until;
        public Task<PagedResult<InvoiceLookupResponse>> GetPageAsync(int? number, int? status, int page, int size,
            DateTime? fromUtc = null, DateTime? untilUtc = null, string? keyword = null, CancellationToken ct = default)
        { Calls++; Keyword = keyword; Filters = (number, status, page, size); From = fromUtc; Until = untilUtc;
          return Task.FromResult(PagedResult<InvoiceLookupResponse>.Create([], 0, page, size)); }
    }
    [Theory] [InlineData("ADMIN", "  Trần  ", "Trần")] [InlineData("RECEPTIONIST", "  0900  ", "0900")]
    [InlineData("ADMIN", "100%_", "100%_")] [InlineData("ADMIN", "O'Neil", "O'Neil")] [InlineData("ADMIN", "<script>", "<script>")]
    public async Task Trims_and_forwards_literal_keyword(string role, string raw, string expected)
    {
        var r = new Repository(); var result = await new InvoiceHistoryService(r, new()).GetAsync(new() { Keyword = raw }, role);
        Assert.True(result.IsSuccess); Assert.Equal(expected, r.Keyword); Assert.Equal(1, r.Calls);
    }
    [Theory] [InlineData(null)] [InlineData("")] [InlineData("   ")]
    public async Task Blank_keyword_preserves_existing_scope(string? keyword)
    {
        var r = new Repository(); var result = await new InvoiceHistoryService(r, new()).GetAsync(new() { Keyword = keyword }, "ADMIN");
        Assert.True(result.IsSuccess); Assert.Null(r.Keyword); Assert.Equal(1, r.Calls);
    }
    [Theory] [InlineData(100, true)] [InlineData(101, false)]
    public async Task Boundaries_validate_before_repository(int length, bool valid)
    {
        var r = new Repository(); var result = await new InvoiceHistoryService(r, new()).GetAsync(new() { Keyword = new string('A', length) }, "ADMIN");
        Assert.Equal(valid, result.IsSuccess); Assert.Equal(valid ? 1 : 0, r.Calls);
    }
    [Theory] [InlineData("PATIENT")] [InlineData("DENTIST")] [InlineData("ASSISTANT")]
    public async Task Keyword_does_not_grant_other_roles_access(string role)
    {
        var r = new Repository(); var result = await new InvoiceHistoryService(r, new()).GetAsync(new() { Keyword = "Trần" }, role);
        Assert.Equal("GEN_002", result.Error.Code); Assert.Equal(0, r.Calls);
    }
    [Fact]
    public async Task Keyword_intersects_all_existing_filters()
    {
        var r = new Repository(); var day = new DateOnly(2026, 10, 14);
        var result = await new InvoiceHistoryService(r, new()).GetAsync(new()
          { Keyword = "  Trần ", PatientCode = "BN000123", Status = 2, Page = 3, PageSize = 10, FromDate = day, ToDate = day }, "ADMIN");
        Assert.True(result.IsSuccess); Assert.Equal("Trần", r.Keyword); Assert.Equal((123, 2, 3, 10), r.Filters);
        Assert.Equal(new DateTime(2026, 10, 13, 17, 0, 0, DateTimeKind.Utc), r.From);
        Assert.Equal(new DateTime(2026, 10, 14, 17, 0, 0, DateTimeKind.Utc), r.Until);
    }
}
