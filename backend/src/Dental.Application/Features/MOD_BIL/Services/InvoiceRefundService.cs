using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
namespace Dental.Application.Features.MOD_BIL.Services;
public sealed class InvoiceRefundService(IInvoiceRefundRepository repository)
{
    public Task<Result<InvoiceRefundContext>> ReadAsync(int id,int actor,string? role,CancellationToken ct) =>
        role!=RoleCodes.Admin ? Task.FromResult(Result<InvoiceRefundContext>.Failure(Error.Forbidden)) : id<=0 ? Task.FromResult(Result<InvoiceRefundContext>.Failure(Error.Validation)) : repository.ReadAsync(id,actor,ct);
    public Task<Result<InvoiceRefundPreview>> PreviewAsync(int id,int actor,string? role,InvoiceRefundPreviewRequest request,CancellationToken ct) =>
        role!=RoleCodes.Admin ? Task.FromResult(Result<InvoiceRefundPreview>.Failure(Error.Forbidden)) : id<=0 || !new InvoiceRefundPreviewValidator().Validate(request).IsValid ? Task.FromResult(Result<InvoiceRefundPreview>.Failure(Error.Validation)) : repository.PreviewAsync(id,actor,request,ct);
    public Task<Result<RefundRecordDto>> RefundAsync(int id,int actor,string? role,InvoiceRefundRequest request,string? ip,CancellationToken ct) =>
        role!=RoleCodes.Admin ? Task.FromResult(Result<RefundRecordDto>.Failure(Error.Forbidden)) : id<=0 || !new InvoiceRefundRequestValidator().Validate(request).IsValid ? Task.FromResult(Result<RefundRecordDto>.Failure(Error.Validation)) : repository.RefundAsync(id,actor,request,ip,ct);
    public Task<Result<InvoiceRefundVoucher>> PrintAsync(int id,int refundId,int actor,string? role,string? ip,CancellationToken ct) =>
        role!=RoleCodes.Admin ? Task.FromResult(Result<InvoiceRefundVoucher>.Failure(Error.Forbidden)) : id<=0 || refundId<=0 ? Task.FromResult(Result<InvoiceRefundVoucher>.Failure(Error.Validation)) : repository.PrintAsync(id,refundId,actor,ip,ct);
}
