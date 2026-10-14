using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Services;
using Dental.Application.Interfaces;
using Xunit;
namespace Dental.Tests.Billing;
public sealed class InvoiceHistoryCodeTests
{
    private sealed class Repository : IInvoiceHistoryRepository
    {
        public int Calls; public string? Code, Keyword; public (int?, int?, int, int)? Scope; public DateTime? From, Until;
        public Task<PagedResult<InvoiceLookupResponse>> GetPageAsync(int? number, int? status, int page, int size,
            DateTime? fromUtc = null, DateTime? untilUtc = null, string? keyword = null, string? invoiceCode = null, CancellationToken ct = default)
        { Calls++; Code = invoiceCode; Keyword = keyword; Scope = (number, status, page, size); From = fromUtc; Until = untilUtc;
          return Task.FromResult(PagedResult<InvoiceLookupResponse>.Create([], 0, page, size)); }
    }
    [Theory] [InlineData("ADMIN", "  inv-20261015-0001  ", "INV-20261015-0001")]
    [InlineData("RECEPTIONIST", "Inv-X", "INV-X")]
    public async Task Exact_code_normalized_before_forwarding(string role, string raw, string expected)
    {
        var r = new Repository(); var result = await new InvoiceHistoryService(r, new()).GetAsync(new() { InvoiceCode = raw }, role);
        Assert.True(result.IsSuccess); Assert.Equal(expected, r.Code); Assert.Equal(1, r.Calls);
    }
    [Theory] [InlineData(null)] [InlineData("")] [InlineData("   ")]
    public async Task Blank_optional_code_preserves_other_scope(string? code)
    {
        var r = new Repository(); var result = await new InvoiceHistoryService(r, new()).GetAsync(new() { InvoiceCode = code, Status = 0 }, "ADMIN");
        Assert.True(result.IsSuccess); Assert.Null(r.Code); Assert.Equal((null, 0, 1, 20), r.Scope);
    }
    [Theory] [InlineData("INV X")] [InlineData("INV%")] [InlineData("INV_")] [InlineData("INV'")] [InlineData("HÓA-01")]
    [InlineData("INV１２")] [InlineData("ınv-01")] [InlineData("<script>")]
    public async Task Invalid_code400_without_query(string code)
    {
        var r = new Repository(); var result = await new InvoiceHistoryService(r, new()).GetAsync(new() { InvoiceCode = code }, "ADMIN");
        Assert.Equal("GEN_003", result.Error.Code); Assert.Equal(0, r.Calls);
    }
    [Theory] [InlineData(50, true)] [InlineData(51, false)]
    public async Task Canonical_length_boundary(int length, bool valid)
    {
        var r = new Repository(); var result = await new InvoiceHistoryService(r, new()).GetAsync(new() { InvoiceCode = " " + new string('a', length) + " " }, "ADMIN");
        Assert.Equal(valid, result.IsSuccess); Assert.Equal(valid ? 1 : 0, r.Calls);
    }
    [Theory] [InlineData("PATIENT")] [InlineData("DENTIST")] [InlineData("ASSISTANT")]
    public async Task Code_never_grants_denied_role_access(string role)
    {
        var r = new Repository(); var result = await new InvoiceHistoryService(r, new()).GetAsync(new() { InvoiceCode = "INV-X" }, role);
        Assert.Equal("GEN_002", result.Error.Code); Assert.Equal(0, r.Calls);
    }
    [Fact]
    public async Task Code_combines_with_every_existing_filter_and_page()
    {
        var r = new Repository(); var result = await new InvoiceHistoryService(r, new()).GetAsync(new() { InvoiceCode = " inv-x ", Keyword = " Trần ",
            PatientCode = "BN000123", Status = 2, FromDate = new(2026, 10, 15), ToDate = new(2026, 10, 15), Page = 2, PageSize = 10 }, "ADMIN");
        Assert.True(result.IsSuccess); Assert.Equal("INV-X", r.Code); Assert.Equal("Trần", r.Keyword); Assert.Equal((123, 2, 2, 10), r.Scope);
        Assert.Equal(new DateTime(2026, 10, 14, 17, 0, 0, DateTimeKind.Utc), r.From); Assert.Equal(new DateTime(2026, 10, 15, 17, 0, 0, DateTimeKind.Utc), r.Until);
    }
}
