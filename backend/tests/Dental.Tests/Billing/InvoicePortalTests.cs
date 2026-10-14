using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Services;
using Dental.Application.Interfaces;
using Xunit;
namespace Dental.Tests.Billing;
public sealed class InvoicePortalTests
{
    private sealed class Repository : IInvoicePortalRepository
    {
        public bool Owns = true; public int OwnershipCalls, InvoiceCalls, PaymentCalls;
        public (int, int)? Owner; public (int, int, int, int)? List; public (int, int, int, int, int)? Payments;
        public PagedResult<InvoicePortalPaymentResponse>? Rows = PagedResult<InvoicePortalPaymentResponse>.Create([], 0, 1, 20);
        public Task<bool> OwnsPatientAsync(int patientId, int userId, CancellationToken ct = default)
        { OwnershipCalls++; Owner = (patientId, userId); return Task.FromResult(Owns); }
        public Task<PagedResult<InvoiceLookupResponse>> GetInvoicesAsync(int patientId, int userId, int page, int size, CancellationToken ct = default)
        { InvoiceCalls++; List = (patientId, userId, page, size); return Task.FromResult(PagedResult<InvoiceLookupResponse>.Create([], 0, page, size)); }
        public Task<PagedResult<InvoicePortalPaymentResponse>?> GetPaymentsAsync(int invoiceId, int patientId, int userId, int page, int size, CancellationToken ct = default)
        { PaymentCalls++; Payments = (invoiceId, patientId, userId, page, size); return Task.FromResult(Rows); }
    }
    [Theory] [InlineData(null)] [InlineData("")] [InlineData("0")] [InlineData("-1")] [InlineData("abc")]
    [InlineData("1.0")] [InlineData("2147483648")] [InlineData(" 1")]
    public async Task Invalid_missing_patient403_without_query(string? patientId)
    {
        var r = new Repository(); var result = await new InvoicePortalService(r, new()).ListAsync(new() { PatientId = patientId }, 7, "PATIENT");
        Assert.Equal("GEN_002", result.Error.Code); Assert.Equal(0, r.OwnershipCalls); Assert.Equal(0, r.InvoiceCalls);
    }
    [Theory] [InlineData("ADMIN")] [InlineData("RECEPTIONIST")] [InlineData("DENTIST")] [InlineData("ASSISTANT")] [InlineData(null)]
    public async Task Roles_cannot_use_patient_portal(string? role)
    {
        var r = new Repository(); var result = await new InvoicePortalService(r, new()).ListAsync(new() { PatientId = "23" }, 7, role);
        Assert.Equal("GEN_002", result.Error.Code); Assert.Equal(0, r.OwnershipCalls);
    }
    [Theory] [InlineData(0)] [InlineData(-1)]
    public async Task Invalid_current_user_never_queries(int userId)
    {
        var r = new Repository(); var result = await new InvoicePortalService(r, new()).ListAsync(new() { PatientId = "23" }, userId, "PATIENT");
        Assert.Equal("GEN_002", result.Error.Code); Assert.Equal(0, r.OwnershipCalls);
    }
    [Theory] [InlineData(0, 20)] [InlineData(1000001, 20)] [InlineData(1, 0)] [InlineData(1, 51)]
    public async Task Invalid_pagination400_without_query(int page, int size)
    {
        var r = new Repository(); var result = await new InvoicePortalService(r, new()).ListAsync(new() { PatientId = "23", Page = page, PageSize = size }, 7, "PATIENT");
        Assert.Equal("GEN_003", result.Error.Code); Assert.Equal(0, r.OwnershipCalls);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Foreign_profile403_without_financial_reads(bool payments)
    {
        var r = new Repository { Owns = false }; var s = new InvoicePortalService(r, new()); var query = new InvoicePortalQuery { PatientId = "23" };
        var error = payments ? (await s.PaymentsAsync(9, query, 7, "PATIENT")).Error : (await s.ListAsync(query, 7, "PATIENT")).Error;
        Assert.Equal("GEN_002", error.Code); Assert.Equal((23, 7), r.Owner); Assert.Equal(0, r.InvoiceCalls + r.PaymentCalls);
    }
    [Fact]
    public async Task List_preserves_explicit_profile_JWT_user_and_page()
    {
        var r = new Repository(); var result = await new InvoicePortalService(r, new()).ListAsync(new() { PatientId = "23", Page = 2, PageSize = 10 }, 7, "PATIENT");
        Assert.True(result.IsSuccess); Assert.Equal((23, 7), r.Owner); Assert.Equal((23, 7, 2, 10), r.List); Assert.Empty(result.Value!.Items);
    }
    [Fact]
    public async Task Wrong_or_missing_invoice403_same_as_foreign()
    {
        var r = new Repository { Rows = null }; var result = await new InvoicePortalService(r, new()).PaymentsAsync(9, new() { PatientId = "23" }, 7, "PATIENT");
        Assert.Equal("GEN_002", result.Error.Code); Assert.Equal((9, 23, 7, 1, 20), r.Payments);
    }
    [Fact]
    public async Task Payment_response_and_scope_forwarded()
    {
        var r = new Repository(); var result = await new InvoicePortalService(r, new()).PaymentsAsync(9, new() { PatientId = "23", Page = 2 }, 7, "PATIENT");
        Assert.True(result.IsSuccess); Assert.Same(r.Rows, result.Value); Assert.Equal((9, 23, 7, 2, 20), r.Payments);
    }
    [Theory] [InlineData(0)] [InlineData(-1)]
    public async Task Invalid_invoice_id400_never_reads_payments(int id)
    {
        var r = new Repository(); var result = await new InvoicePortalService(r, new()).PaymentsAsync(id, new() { PatientId = "23" }, 7, "PATIENT");
        Assert.Equal("GEN_003", result.Error.Code); Assert.Equal(0, r.PaymentCalls);
    }
    [Theory] [InlineData(1, 1)] [InlineData(1000000, 50)]
    public async Task Valid_page_boundaries_return_empty200(int page, int size)
    {
        var r = new Repository(); var result = await new InvoicePortalService(r, new()).ListAsync(new() { PatientId = "23", Page = page, PageSize = size }, 7, "PATIENT");
        Assert.True(result.IsSuccess); Assert.Equal((23, 7, page, size), r.List);
    }
}
