namespace Dental.Application.Features.MOD_BIL.DTOs;
public sealed record RefundSelection(int InvoiceItemId, int Quantity);
public sealed record InvoiceRefundPreviewRequest(string Snapshot, IReadOnlyList<RefundSelection> Items, string PaymentMethod);
public sealed record InvoiceRefundRequest(string Snapshot, IReadOnlyList<RefundSelection> Items, string PaymentMethod,
    string ExpectedPreview, Guid RequestId, string ReasonCode, string ReasonNotes, bool Confirmed,
    bool LateApproval = false, bool ReserveConfirmed = false);
// New refund APIs use invariant decimal strings to preserve all numeric(18,0) values in JavaScript.
public sealed record RefundLineDto(int Id, string ItemType, string Code, string Name, int OriginalQuantity,
    int RefundedQuantity, int RemainingQuantity, string UnitPrice);
public sealed record RefundRecordDto(int Id, int Revision, Guid RequestId, int? PaymentId, string? VoucherCode, string RefundAmount,
    string DebtReduction, string TotalAfter, string DiscountAfter, string PaidAfter, string RemainingAfter,
    string ReasonCode, string ReasonNotes, string PaymentMethod, DateTime CreatedAt, string ActorName);
public sealed record InvoiceRefundContext(int InvoiceId, string InvoiceCode, int Status, string Snapshot,
    string TotalAmount, string DiscountAmount, string PaidAmount, string RemainingAmount,
    IReadOnlyList<RefundLineDto> Items, IReadOnlyList<RefundRecordDto> History);
public sealed record InvoiceRefundPreview(string Preview, string RefundAmount, string DebtReduction,
    string TotalAfter, string DiscountAfter, string PaidAfter, string RemainingAfter, bool FullCancellation,
    bool RequiresLateApproval, bool RequiresReserve, string CashBalance);
public sealed record InvoiceRefundVoucher(RefundRecordDto Record, string InvoiceCode, string ClinicName,
    string? ClinicAddress, string? ClinicPhone, string PatientName, int PrintNumber);
