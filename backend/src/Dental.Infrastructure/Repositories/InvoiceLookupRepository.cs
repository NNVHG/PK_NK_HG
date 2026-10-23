using System.Globalization;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Interfaces;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace Dental.Infrastructure.Repositories;
public sealed class InvoiceLookupRepository(DentalDbContext db) : IInvoiceLookupRepository
{
    public async Task<InvoiceLookupResponse?> FindByCodeAsync(string normalizedCode, CancellationToken ct = default)
    {
        var row = await db.Invoices.AsNoTracking().Where(i => i.InvoiceCode == normalizedCode)
            .Select(i => new { i.Id, i.InvoiceCode, Status = (int)i.Status, i.TotalAmount, i.PaidAmount, i.CreatedAt,
                i.Visit.Patient.PatientNumber, PatientName = i.Visit.Patient.FullName }).SingleOrDefaultAsync(ct);
        return row is null ? null : new(row.Id, row.InvoiceCode, row.Status, row.TotalAmount, row.PaidAmount,
            row.TotalAmount - row.PaidAmount, "BN" + row.PatientNumber.ToString("D6", CultureInfo.InvariantCulture), row.PatientName, row.CreatedAt);
    }
}
