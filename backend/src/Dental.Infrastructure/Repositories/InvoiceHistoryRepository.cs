using System.Globalization;
using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Interfaces;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace Dental.Infrastructure.Repositories;
public sealed class InvoiceHistoryRepository(DentalDbContext db) : IInvoiceHistoryRepository
{
    public async Task<PagedResult<InvoiceLookupResponse>> GetPageAsync(int? patientNumber, int? status, int page, int pageSize,
        DateTime? fromUtc = null, DateTime? untilUtc = null, string? keyword = null, string? invoiceCode = null, CancellationToken ct = default)
    {
        var query = db.Invoices.AsNoTracking();
        if (patientNumber is int number) query = query.Where(i => i.Visit.Patient.PatientNumber == number);
        if (status is int state) query = query.Where(i => (int)i.Status == state);
        if (fromUtc is DateTime from) query = query.Where(i => i.CreatedAt >= from);
        if (untilUtc is DateTime until) query = query.Where(i => i.CreatedAt < until);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim().ToLowerInvariant();
            query = query.Where(i => i.Visit.Patient.FullName.ToLower().Contains(term) || i.Visit.Patient.Phone.Contains(term));
        }
        if (invoiceCode is not null) query = query.Where(i => i.InvoiceCode == invoiceCode);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(i => i.CreatedAt).ThenByDescending(i => i.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(i => new { i.Id, i.InvoiceCode, Status = (int)i.Status, i.TotalAmount, i.PaidAmount, i.CreatedAt,
                i.Visit.Patient.PatientNumber, PatientName = i.Visit.Patient.FullName }).ToListAsync(ct);
        return PagedResult<InvoiceLookupResponse>.Create(rows.Select(row => new InvoiceLookupResponse(row.Id, row.InvoiceCode,
            row.Status, row.TotalAmount, row.PaidAmount, row.TotalAmount - row.PaidAmount,
            "BN" + row.PatientNumber.ToString("D6", CultureInfo.InvariantCulture), row.PatientName, row.CreatedAt)).ToList(), total, page, pageSize);
    }
}
