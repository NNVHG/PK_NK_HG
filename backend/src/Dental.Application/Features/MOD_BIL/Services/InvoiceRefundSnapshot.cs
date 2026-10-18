using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dental.Domain.Entities;

namespace Dental.Application.Features.MOD_BIL.Services;

public static class InvoiceRefundSnapshot
{
    public static string Create(Invoice invoice) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
    {
        invoice.Id, invoice.RefundRevision, invoice.Status, invoice.TotalAmount, invoice.PaidAmount, invoice.DiscountAmount, invoice.DiscountType, invoice.DiscountValue,
        items = invoice.Items.OrderBy(x => x.Id).Select(x => new { x.Id, x.Quantity, x.UnitPrice, x.ItemType, x.SourcePrescriptionItemId }),
        returned = invoice.Refunds.OrderBy(x => x.Revision).Select(x => new { x.Id, x.Revision, items = x.Items.OrderBy(l => l.InvoiceItemId).Select(l => new { l.InvoiceItemId, l.Quantity }) })
    }))));
}