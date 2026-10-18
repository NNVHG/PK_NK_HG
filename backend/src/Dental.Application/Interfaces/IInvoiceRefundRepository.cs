using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
namespace Dental.Application.Interfaces;
public interface IInvoiceRefundRepository
{
    Task<Result<InvoiceRefundContext>> ReadAsync(int id,int actorId,CancellationToken ct);
    Task<Result<InvoiceRefundPreview>> PreviewAsync(int id,int actorId,InvoiceRefundPreviewRequest request,CancellationToken ct);
    Task<Result<RefundRecordDto>> RefundAsync(int id,int actorId,InvoiceRefundRequest request,string? ip,CancellationToken ct);
    Task<Result<InvoiceRefundVoucher>> PrintAsync(int id,int refundId,int actorId,string? ip,CancellationToken ct);
}
