using Dental.Application.Common;
using Dental.Domain.Entities;
using Dental.Domain.Enums;

namespace Dental.Application.Features.MOD_BIL.Services;

public static class InvoiceDraftSynchronizer
{
    public static Result<bool> Apply(Invoice invoice, IEnumerable<VisitService> services)
    {
        if (invoice.Status != InvoiceStatus.Draft || invoice.PaidAmount != 0)
            return Result<bool>.Failure(new Error("BIL_007", "Chỉ đồng bộ hóa đơn nháp chưa thanh toán."));
        var snapshot = InvoiceServiceSnapshot.Create(services);
        if (snapshot.IsFailure) return Result<bool>.Failure(snapshot.Error);
        var items = snapshot.Value!;
        var unchanged = invoice.Items.OrderBy(x => x.SourceVisitServiceId).Select(Key)
            .SequenceEqual(items.OrderBy(x => x.SourceVisitServiceId).Select(Key));
        var amount = items.Sum(x => x.TotalAmount);
        if (unchanged && invoice.TotalAmount == amount) return Result<bool>.Success(false);
        invoice.Items = items.ToList();
        invoice.TotalAmount = amount;
        return Result<bool>.Success(true);
    }

    private static object Key(InvoiceItem item) => (item.SourceVisitServiceId, item.ItemType, item.Code,
        item.Name, item.ToothNumber, item.Surface, item.Quantity, item.UnitPrice, item.TotalAmount);
}
