namespace Dental.Application.Features.MOD_BIL.DTOs;

public sealed class InvoicePortalListQuery : InvoicePortalQuery
{
    public int? Status { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
}
