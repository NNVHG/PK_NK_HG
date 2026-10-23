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
        public int ProfileCalls; public int? ProfileUser;
        public IReadOnlyList<InvoicePortalProfileResponse> Profiles = [];
        public Task<IReadOnlyList<InvoicePortalProfileResponse>> GetProfilesAsync(int userId, CancellationToken ct = default)
        { ProfileCalls++; ProfileUser = userId; return Task.FromResult(Profiles); }
        public bool Owns = true; public int ItemCalls; public (int, int, int, int, int)? Items;
        public PagedResult<InvoicePortalItemResponse>? ItemRows = PagedResult<InvoicePortalItemResponse>.Create([], 0, 1, 20); public int OwnershipCalls, InvoiceCalls, PaymentCalls;
        public (int?, DateTime?, DateTime?)? ListFilters;
        public (int, int)? Owner; public (int, int, int, int)? List; public (int, int, int, int, int)? Payments;
        public PagedResult<InvoicePortalPaymentResponse>? Rows = PagedResult<InvoicePortalPaymentResponse>.Create([], 0, 1, 20);
        public Task<PagedResult<InvoicePortalItemResponse>?> GetItemsAsync(int invoiceId, int patientId, int userId, int page, int size, CancellationToken ct = default)
        { ItemCalls++; Items = (invoiceId, patientId, userId, page, size); return Task.FromResult(ItemRows); }
        public Task<bool> OwnsPatientAsync(int patientId, int userId, CancellationToken ct = default)
        { OwnershipCalls++; Owner = (patientId, userId); return Task.FromResult(Owns); }
        public Task<PagedResult<InvoiceLookupResponse>> GetInvoicesAsync(int patientId, int userId, int page, int size, int? status = null, DateTime? fromUtc = null, DateTime? untilUtc = null, CancellationToken ct = default)
        { InvoiceCalls++; List = (patientId, userId, page, size); ListFilters = (status, fromUtc, untilUtc); return Task.FromResult(PagedResult<InvoiceLookupResponse>.Create([], 0, page, size)); }
        public Task<PagedResult<InvoicePortalPaymentResponse>?> GetPaymentsAsync(int invoiceId, int patientId, int userId, int page, int size, CancellationToken ct = default)
        { PaymentCalls++; Payments = (invoiceId, patientId, userId, page, size); return Task.FromResult(Rows); }
    }
    [Theory] [InlineData(null)] [InlineData("")] [InlineData("0")] [InlineData("-1")] [InlineData("abc")]
    [InlineData("1.0")] [InlineData("2147483648")] [InlineData(" 1")]
    public async Task Invalid_missing_patient403_without_query(string? patientId)
    {
        var r = new Repository(); var result = await new InvoicePortalService(r, new(), new()).ListAsync(new() { PatientId = patientId }, 7, "PATIENT");
        Assert.Equal("GEN_002", result.Error.Code); Assert.Equal(0, r.OwnershipCalls); Assert.Equal(0, r.InvoiceCalls);
    }
    [Theory] [InlineData("ADMIN")] [InlineData("RECEPTIONIST")] [InlineData("DENTIST")] [InlineData("ASSISTANT")] [InlineData(null)]
    public async Task Roles_cannot_use_patient_portal(string? role)
    {
        var r = new Repository(); var result = await new InvoicePortalService(r, new(), new()).ListAsync(new() { PatientId = "23" }, 7, role);
        Assert.Equal("GEN_002", result.Error.Code); Assert.Equal(0, r.OwnershipCalls);
    }
    [Theory] [InlineData(0)] [InlineData(-1)]
    public async Task Invalid_current_user_never_queries(int userId)
    {
        var r = new Repository(); var result = await new InvoicePortalService(r, new(), new()).ListAsync(new() { PatientId = "23" }, userId, "PATIENT");
        Assert.Equal("GEN_002", result.Error.Code); Assert.Equal(0, r.OwnershipCalls);
    }
    [Theory] [InlineData(0, 20)] [InlineData(1000001, 20)] [InlineData(1, 0)] [InlineData(1, 51)]
    public async Task Invalid_pagination400_without_query(int page, int size)
    {
        var r = new Repository(); var result = await new InvoicePortalService(r, new(), new()).ListAsync(new() { PatientId = "23", Page = page, PageSize = size }, 7, "PATIENT");
        Assert.Equal("GEN_003", result.Error.Code); Assert.Equal(0, r.OwnershipCalls);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Foreign_profile403_without_financial_reads(bool payments)
    {
        var r = new Repository { Owns = false }; var s = new InvoicePortalService(r, new(), new()); var query = new InvoicePortalListQuery { PatientId = "23" };
        var error = payments ? (await s.PaymentsAsync(9, query, 7, "PATIENT")).Error : (await s.ListAsync(query, 7, "PATIENT")).Error;
        Assert.Equal("GEN_002", error.Code); Assert.Equal((23, 7), r.Owner); Assert.Equal(0, r.InvoiceCalls + r.PaymentCalls);
    }
    [Fact]
    public async Task List_preserves_explicit_profile_JWT_user_and_page()
    {
        var r = new Repository(); var result = await new InvoicePortalService(r, new(), new()).ListAsync(new() { PatientId = "23", Page = 2, PageSize = 10 }, 7, "PATIENT");
        Assert.True(result.IsSuccess); Assert.Equal((23, 7), r.Owner); Assert.Equal((23, 7, 2, 10), r.List); Assert.Empty(result.Value!.Items);
    }
    [Fact]
    public async Task Wrong_or_missing_invoice403_same_as_foreign()
    {
        var r = new Repository { Rows = null }; var result = await new InvoicePortalService(r, new(), new()).PaymentsAsync(9, new() { PatientId = "23" }, 7, "PATIENT");
        Assert.Equal("GEN_002", result.Error.Code); Assert.Equal((9, 23, 7, 1, 20), r.Payments);
    }
    [Fact]
    public async Task Payment_response_and_scope_forwarded()
    {
        var r = new Repository(); var result = await new InvoicePortalService(r, new(), new()).PaymentsAsync(9, new() { PatientId = "23", Page = 2 }, 7, "PATIENT");
        Assert.True(result.IsSuccess); Assert.Same(r.Rows, result.Value); Assert.Equal((9, 23, 7, 2, 20), r.Payments);
    }
    [Theory] [InlineData(0)] [InlineData(-1)]
    public async Task Invalid_invoice_id400_never_reads_payments(int id)
    {
        var r = new Repository(); var result = await new InvoicePortalService(r, new(), new()).PaymentsAsync(id, new() { PatientId = "23" }, 7, "PATIENT");
        Assert.Equal("GEN_003", result.Error.Code); Assert.Equal(0, r.PaymentCalls);
    }
    [Theory] [InlineData(1, 1)] [InlineData(1000000, 50)]
    public async Task Valid_page_boundaries_return_empty200(int page, int size)
    {
        var r = new Repository(); var result = await new InvoicePortalService(r, new(), new()).ListAsync(new() { PatientId = "23", Page = page, PageSize = size }, 7, "PATIENT");
        Assert.True(result.IsSuccess); Assert.Equal((23, 7, page, size), r.List);
    }
    [Theory] [InlineData(null)] [InlineData("")] [InlineData("0")] [InlineData("-1")] [InlineData("abc")] [InlineData("2147483648")]
    public async Task Items_require_valid_explicit_profile403(string? patientId)
    {
        var r = new Repository(); var result = await new InvoicePortalService(r, new(), new()).ItemsAsync(9, new() { PatientId = patientId }, 7, "PATIENT");
        Assert.Equal("GEN_002", result.Error.Code); Assert.Equal(0, r.OwnershipCalls + r.ItemCalls);
    }
    [Theory] [InlineData("ADMIN")] [InlineData("RECEPTIONIST")] [InlineData("DENTIST")] [InlineData("ASSISTANT")] [InlineData(null)]
    public async Task Items_patient_only403(string? role)
    {
        var r = new Repository(); var result = await new InvoicePortalService(r, new(), new()).ItemsAsync(9, new() { PatientId = "23" }, 7, role);
        Assert.Equal("GEN_002", result.Error.Code); Assert.Equal(0, r.OwnershipCalls + r.ItemCalls);
    }
    [Theory] [InlineData(0)] [InlineData(-1)]
    public async Task Items_invalid_identity403(int userId)
    {
        var r = new Repository(); var result = await new InvoicePortalService(r, new(), new()).ItemsAsync(9, new() { PatientId = "23" }, userId, "PATIENT");
        Assert.Equal("GEN_002", result.Error.Code); Assert.Equal(0, r.OwnershipCalls + r.ItemCalls);
    }
    [Theory] [InlineData(0, 20)] [InlineData(1000001, 20)] [InlineData(1, 0)] [InlineData(1, 51)]
    public async Task Items_invalid_page400_before_repository(int page, int size)
    {
        var r = new Repository(); var result = await new InvoicePortalService(r, new(), new()).ItemsAsync(9, new() { PatientId = "23", Page = page, PageSize = size }, 7, "PATIENT");
        Assert.Equal("GEN_003", result.Error.Code); Assert.Equal(0, r.OwnershipCalls + r.ItemCalls);
    }
    [Fact]
    public async Task Items_foreign_profile403_never_reads()
    {
        var r = new Repository { Owns = false }; var result = await new InvoicePortalService(r, new(), new()).ItemsAsync(9, new() { PatientId = "23" }, 7, "PATIENT");
        Assert.Equal("GEN_002", result.Error.Code); Assert.Equal((23, 7), r.Owner); Assert.Equal(0, r.ItemCalls);
    }
    [Theory] [InlineData(0)] [InlineData(-1)]
    public async Task Items_invalid_invoice400_never_reads(int id)
    {
        var r = new Repository(); var result = await new InvoicePortalService(r, new(), new()).ItemsAsync(id, new() { PatientId = "23" }, 7, "PATIENT");
        Assert.Equal("GEN_003", result.Error.Code); Assert.Equal(0, r.ItemCalls);
    }
    [Fact]
    public async Task Items_wrong_missing_foreign_invoice403()
    {
        var r = new Repository { ItemRows = null }; var result = await new InvoicePortalService(r, new(), new()).ItemsAsync(9, new() { PatientId = "23" }, 7, "PATIENT");
        Assert.Equal("GEN_002", result.Error.Code); Assert.Equal((9, 23, 7, 1, 20), r.Items);
    }
    [Fact]
    public async Task Items_forward_exact_scope_page_and_snapshot()
    {
        var r = new Repository { ItemRows = PagedResult<InvoicePortalItemResponse>.Create([new(4, "OLD", "Snapshot", 2, 50000, 100000)], 1, 2, 10) };
        var result = await new InvoicePortalService(r, new(), new()).ItemsAsync(9, new() { PatientId = "23", Page = 2, PageSize = 10 }, 7, "PATIENT");
        Assert.True(result.IsSuccess); Assert.Same(r.ItemRows, result.Value); Assert.Equal((9, 23, 7, 2, 10), r.Items);
    }
    [Theory] [InlineData(1, 1)] [InlineData(1000000, 50)]
    public async Task Items_valid_page_bounds_empty200(int page, int size)
    {
        var r = new Repository(); var result = await new InvoicePortalService(r, new(), new()).ItemsAsync(9, new() { PatientId = "23", Page = page, PageSize = size }, 7, "PATIENT");
        Assert.True(result.IsSuccess); Assert.Empty(result.Value!.Items); Assert.Equal((9, 23, 7, page, size), r.Items);
    }
    [Theory] [InlineData("ADMIN")] [InlineData("RECEPTIONIST")] [InlineData("DENTIST")] [InlineData("ASSISTANT")] [InlineData(null)]
    public async Task Profiles_patient_only403_without_query(string? role)
    {
        var r = new Repository(); var result = await new InvoicePortalService(r, new(), new()).ProfilesAsync(7, role);
        Assert.Equal("GEN_002", result.Error.Code); Assert.Equal(0, r.ProfileCalls);
    }
    [Theory] [InlineData(0)] [InlineData(-1)]
    public async Task Profiles_invalid_identity403_without_query(int userId)
    {
        var r = new Repository(); var result = await new InvoicePortalService(r, new(), new()).ProfilesAsync(userId, "PATIENT");
        Assert.Equal("GEN_002", result.Error.Code); Assert.Equal(0, r.ProfileCalls);
    }
    [Fact]
    public async Task Profiles_use_JWT_user_preserve_inactive_and_do_not_read_financial_data()
    {
        var r = new Repository { Profiles = [new(23, "BN000023", "Synthetic profile", new DateOnly(2000, 1, 2), false)] };
        var result = await new InvoicePortalService(r, new(), new()).ProfilesAsync(7, "PATIENT");
        Assert.True(result.IsSuccess); Assert.Same(r.Profiles, result.Value); Assert.False(result.Value!.Single().IsActive);
        Assert.Equal(7, r.ProfileUser); Assert.Equal(1, r.ProfileCalls);
        Assert.Equal(0, r.OwnershipCalls + r.InvoiceCalls + r.PaymentCalls + r.ItemCalls);
    }
    [Fact]
    public async Task Profiles_no_owned_family_returns_empty200()
    {
        var r = new Repository(); var result = await new InvoicePortalService(r, new(), new()).ProfilesAsync(7, "PATIENT");
        Assert.True(result.IsSuccess); Assert.Empty(result.Value!); Assert.Equal(7, r.ProfileUser);
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public async Task List_valid_status_forwarded_with_owner(int status)
    {
        var r = new Repository(); var result = await new InvoicePortalService(r, new(), new()).ListAsync(new() { PatientId = "23", Status = status }, 7, "PATIENT");
        Assert.True(result.IsSuccess); Assert.Equal((23, 7, 1, 20), r.List); Assert.Equal(status, r.ListFilters!.Value.Item1);
    }
    [Theory] [InlineData(-1)] [InlineData(5)] [InlineData(int.MaxValue)]
    public async Task List_invalid_status400_without_invoice_query(int status)
    {
        var r = new Repository(); var result = await new InvoicePortalService(r, new(), new()).ListAsync(new() { PatientId = "23", Status = status }, 7, "PATIENT");
        Assert.Equal("GEN_003", result.Error.Code); Assert.Equal(0, r.InvoiceCalls);
    }
    [Theory] [InlineData("0001-01-01", null)] [InlineData(null, "9999-12-31")] [InlineData("2026-10-15", "2026-10-14")]
    public async Task List_invalid_date_bounds400_without_invoice_query(string? from, string? to)
    {
        var r = new Repository(); var query = new InvoicePortalListQuery { PatientId = "23", FromDate = from is null ? null : DateOnly.Parse(from), ToDate = to is null ? null : DateOnly.Parse(to) };
        var result = await new InvoicePortalService(r, new(), new()).ListAsync(query, 7, "PATIENT");
        Assert.Equal("GEN_003", result.Error.Code); Assert.Equal(0, r.InvoiceCalls);
    }
    [Theory] [InlineData(true, true)] [InlineData(true, false)] [InlineData(false, true)] [InlineData(false, false)]
    public async Task List_dates_use_Vietnam_inclusive_start_exclusive_next_day(bool from, bool to)
    {
        var r = new Repository(); var query = new InvoicePortalListQuery { PatientId = "23", FromDate = from ? new DateOnly(2026, 10, 14) : null, ToDate = to ? new DateOnly(2026, 10, 14) : null };
        var result = await new InvoicePortalService(r, new(), new()).ListAsync(query, 7, "PATIENT");
        Assert.True(result.IsSuccess); var filters = r.ListFilters!.Value;
        Assert.Null(filters.Item1);
        Assert.Equal(from ? new DateTime(2026, 10, 13, 17, 0, 0, DateTimeKind.Utc) : (DateTime?)null, filters.Item2);
        Assert.Equal(to ? new DateTime(2026, 10, 14, 17, 0, 0, DateTimeKind.Utc) : (DateTime?)null, filters.Item3);
        if (from) Assert.Equal(DateTimeKind.Utc, filters.Item2!.Value.Kind);
        if (to) Assert.Equal(DateTimeKind.Utc, filters.Item3!.Value.Kind);
    }
    [Theory] [InlineData("0001-01-02", "9999-12-30")] [InlineData("9999-12-31", null)] [InlineData(null, "0001-01-01")] [InlineData("2024-02-29", "2024-02-29")]
    public async Task List_valid_extreme_and_leap_dates_do_not_overflow(string? from, string? to)
    {
        var r = new Repository(); var query = new InvoicePortalListQuery { PatientId = "23", FromDate = from is null ? null : DateOnly.Parse(from), ToDate = to is null ? null : DateOnly.Parse(to) };
        Assert.True((await new InvoicePortalService(r, new(), new()).ListAsync(query, 7, "PATIENT")).IsSuccess); Assert.Equal(1, r.InvoiceCalls);
    }
    [Fact]
    public async Task List_filters_never_replace_ownership_guard()
    {
        var r = new Repository { Owns = false }; var result = await new InvoicePortalService(r, new(), new()).ListAsync(new() { PatientId = "23", Status = 3, FromDate = new(2026, 10, 14) }, 7, "PATIENT");
        Assert.Equal("GEN_002", result.Error.Code); Assert.Equal(0, r.InvoiceCalls);
    }
}
