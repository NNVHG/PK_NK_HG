namespace Dental.Application.Features.MOD_BIL.DTOs;
public sealed class InvoiceHistoryQuery
{
    public string? PatientCode { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public int? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
