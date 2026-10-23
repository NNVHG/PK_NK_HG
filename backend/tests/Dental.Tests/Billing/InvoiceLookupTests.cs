using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Services;
using Dental.Application.Features.MOD_BIL.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Xunit;
namespace Dental.Tests.Billing;
public sealed class InvoiceLookupTests
{
    private sealed class Repository : IInvoiceLookupRepository
    {
        public string? Code; public InvoiceLookupResponse? Row;
        public Task<InvoiceLookupResponse?> FindByCodeAsync(string code, CancellationToken ct = default)
        { Code = code; return Task.FromResult(Row); }
    }
    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData(" ")] [InlineData("INV_1")] [InlineData("INV 1")] [InlineData("HÃ“A-1")]
    public async Task Invalid_code_never_queries(string? code)
    {
        var r = new Repository(); var result = await new InvoiceLookupService(r, new()).GetAsync(new() { Code = code }, RoleCodes.Admin);
        Assert.Equal("GEN_003", result.Error.Code); Assert.Null(r.Code);
    }
    [Theory]
    [InlineData("DENTIST")] [InlineData("ASSISTANT")] [InlineData("PATIENT")] [InlineData(null)]
    public async Task Denied_role_never_queries(string? role)
    {
        var r = new Repository(); var result = await new InvoiceLookupService(r, new()).GetAsync(new() { Code = "INV-1" }, role);
        Assert.Equal("GEN_002", result.Error.Code); Assert.Null(r.Code);
    }
    [Theory] [InlineData("ADMIN")] [InlineData("RECEPTIONIST")]
    public async Task Normalizes_exact_code_and_returns_missing(string role)
    {
        var r = new Repository(); var result = await new InvoiceLookupService(r, new()).GetAsync(new() { Code = "  inv-1  " }, role);
        Assert.Equal("INV-1", r.Code); Assert.Equal("GEN_001", result.Error.Code);
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public async Task Returns_exact_invoice_including_cancelled_without_replacing(int status)
    {
        var r = new Repository { Row = new(42, "INV-1", status, 100, 40, 60, "BN000001", "Synthetic", DateTime.UtcNow) };
        var result = await new InvoiceLookupService(r, new()).GetAsync(new() { Code = "INV-1" }, RoleCodes.Admin);
        Assert.True(result.IsSuccess); Assert.Same(r.Row, result.Value);
    }
    [Theory] [InlineData(50, true)] [InlineData(51, false)]
    public void Boundary_length(int length, bool valid)
        => Assert.Equal(valid, new InvoiceLookupRequestValidator().Validate(new InvoiceLookupRequest { Code = new string('A', length) }).IsValid);
}
