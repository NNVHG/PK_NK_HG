using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Services;
using Dental.Application.Features.MOD_BIL.Validators;
using Dental.Application.Interfaces;
using Xunit;
namespace Dental.Tests.Billing;
public sealed class InvoiceHistoryTests
{
    private sealed class Repository : IInvoiceHistoryRepository
    {
        public int Calls; public (int?, int?, int, int)? Query;
        public Task<PagedResult<InvoiceLookupResponse>> GetPageAsync(int? number, int? status, int page, int size, DateTime? fromUtc = null, DateTime? untilUtc = null, string? keyword = null, CancellationToken ct = default)
        { Calls++; Query = (number, status, page, size); return Task.FromResult(PagedResult<InvoiceLookupResponse>.Create([], 0, page, size)); }
    }
    [Theory] [InlineData("ADMIN")] [InlineData("RECEPTIONIST")]
    public async Task Normalizes_exact_patient_and_forwards_page(string role)
    {
        var r = new Repository(); var result = await new InvoiceHistoryService(r, new()).GetAsync(
            new() { PatientCode = "  bn000123  ", Status = 2, Page = 3, PageSize = 10 }, role);
        Assert.True(result.IsSuccess); Assert.Equal((123, 2, 3, 10), r.Query); Assert.Empty(result.Value!.Items);
    }
    [Theory] [InlineData(null)] [InlineData("")] [InlineData(" ")]
    public async Task Optional_patient_filter_returns_all(string? code)
    {
        var r = new Repository(); var result = await new InvoiceHistoryService(r, new()).GetAsync(new() { PatientCode = code }, "ADMIN");
        Assert.True(result.IsSuccess); Assert.Equal(((int?)null, (int?)null, 1, 20), r.Query);
    }
    [Theory] [InlineData("DENTIST")] [InlineData("ASSISTANT")] [InlineData("PATIENT")] [InlineData(null)]
    public async Task Denied_roles_do_not_query(string? role)
    {
        var r = new Repository(); var result = await new InvoiceHistoryService(r, new()).GetAsync(new(), role);
        Assert.Equal("GEN_002", result.Error.Code); Assert.Equal(0, r.Calls);
    }
    [Theory]
    [InlineData("BN1")] [InlineData("BN000000")] [InlineData("BN0000001")] [InlineData("BN-000001")]
    [InlineData("BN00012A")] [InlineData("BN2147483648")] [InlineData("XX000001")] [InlineData("BN１２３４５６")]
    public async Task Invalid_patient_never_queries(string code)
    {
        var r = new Repository(); var result = await new InvoiceHistoryService(r, new()).GetAsync(new() { PatientCode = code }, "ADMIN");
        Assert.Equal("GEN_003", result.Error.Code); Assert.Equal(0, r.Calls);
    }
    [Theory] [InlineData(1, 1, null, true)] [InlineData(1000000, 50, 4, true)] [InlineData(0, 20, null, false)]
    [InlineData(1000001, 20, null, false)] [InlineData(1, 0, null, false)] [InlineData(1, 51, null, false)]
    [InlineData(1, 20, -1, false)] [InlineData(1, 20, 5, false)]
    public void Page_size_status_boundaries(int page, int size, int? status, bool valid)
        => Assert.Equal(valid, new InvoiceHistoryQueryValidator().Validate(new InvoiceHistoryQuery { Page = page, PageSize = size, Status = status }).IsValid);
    [Theory] [InlineData("BN000001", 1)] [InlineData("BN1000000", 1000000)] [InlineData("BN2147483647", int.MaxValue)]
    public void Canonical_codes_do_not_prefix_match(string code, int expected)
    { Assert.True(InvoiceHistoryQueryValidator.TryParsePatientCode(code, out var number)); Assert.Equal(expected, number); }
}
