using Dental.Application.Common;
using Dental.Domain.Entities;

namespace Dental.Application.Features.MOD_BIL.Services;

/// <summary>MOD_BIL: sao chụp đúng giá tại chỉ định, không tra lại giá danh mục.</summary>
public static class InvoiceServiceSnapshot
{
    public const decimal MaximumAmount = 999999999999999999m;

    public static Result<IReadOnlyList<InvoiceItem>> Create(IEnumerable<VisitService> rows)
    {
        var items = new List<InvoiceItem>();
        decimal amount = 0;
        foreach (var row in rows)
        {
            if (row.Quantity <= 0 || row.UnitPrice < 0 || decimal.Truncate(row.UnitPrice) != row.UnitPrice)
                return Result<IReadOnlyList<InvoiceItem>>.Failure(new Error("BIL_004", "Dữ liệu giá dịch vụ không hợp lệ."));
            if (row.UnitPrice > (MaximumAmount - amount) / row.Quantity)
                return Result<IReadOnlyList<InvoiceItem>>.Failure(new Error("BIL_004", "Tổng hóa đơn vượt giới hạn lưu trữ."));
            var total = row.UnitPrice * row.Quantity;
            items.Add(new InvoiceItem { SourceVisitServiceId = row.Id, Code = row.ServiceCode,
                Name = row.ServiceName, ToothNumber = row.ToothNumber, Surface = row.Surface,
                Quantity = row.Quantity, UnitPrice = row.UnitPrice, TotalAmount = total });
            amount += total;
        }
        return Result<IReadOnlyList<InvoiceItem>>.Success(items);
    }
}
