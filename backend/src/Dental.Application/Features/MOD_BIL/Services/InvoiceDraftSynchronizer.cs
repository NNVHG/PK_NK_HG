using Dental.Application.Common;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
namespace Dental.Application.Features.MOD_BIL.Services;
public static class InvoiceDraftSynchronizer
{
    public static Result<bool> Apply(Invoice invoice, IEnumerable<VisitService> services, IEnumerable<PrescriptionItem>? medicines = null)
        => ApplyCore(invoice, services, medicines, InvoiceStatus.Draft);
    // DL-287: only the prescription workflow can update a completed, unpaid PendingPayment invoice.
    public static Result<bool> ApplyPendingPrescription(Invoice invoice, IEnumerable<VisitService> services, IEnumerable<PrescriptionItem> medicines)
        => ApplyCore(invoice, services, medicines, InvoiceStatus.PendingPayment);
    private static Result<bool> ApplyCore(Invoice invoice, IEnumerable<VisitService> services, IEnumerable<PrescriptionItem>? medicines, InvoiceStatus expected)
    {
        if (invoice.Status != expected || invoice.PaidAmount != 0)
            return Result<bool>.Failure(new Error("BIL_007", "Chỉ đồng bộ hóa đơn chưa có thanh toán, đúng trạng thái cho phép."));
        var snapshot = InvoiceServiceSnapshot.Create(services, medicines);
        if (snapshot.IsFailure) return Result<bool>.Failure(snapshot.Error);
        var items = snapshot.Value!;
        var unchanged = invoice.Items.OrderBy(x => x.ItemType).ThenBy(x => x.SourceVisitServiceId).ThenBy(x => x.SourcePrescriptionItemId).Select(Key)
            .SequenceEqual(items.OrderBy(x => x.ItemType).ThenBy(x => x.SourceVisitServiceId).ThenBy(x => x.SourcePrescriptionItemId).Select(Key));
        var amount = items.Sum(x => x.TotalAmount);
        if (invoice.DiscountAmount > amount) return Result<bool>.Failure(new Error("RX_008", "Giảm giá vượt tổng mới; Thu ngân/Admin cần đối chiếu lại."));
        if (unchanged && invoice.TotalAmount == amount) return Result<bool>.Success(false);
        invoice.Items = items.ToList(); invoice.TotalAmount = amount;
        return Result<bool>.Success(true);
    }
    private static object Key(InvoiceItem item) => (item.SourceVisitServiceId, item.SourcePrescriptionItemId, item.ItemType, item.Code,
        item.Name, item.ToothNumber, item.Surface, item.Quantity, item.UnitPrice, item.TotalAmount);
}
