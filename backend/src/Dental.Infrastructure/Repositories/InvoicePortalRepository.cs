using System.Globalization;
using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Interfaces;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace Dental.Infrastructure.Repositories;
public sealed class InvoicePortalRepository(DentalDbContext db) : IInvoicePortalRepository
{
    public Task<bool> OwnsPatientAsync(int patientId, int userId, CancellationToken ct = default)
        => db.Patients.AsNoTracking().AnyAsync(p => p.PatientId == patientId && p.UserId == userId, ct);
    public async Task<PagedResult<InvoiceLookupResponse>> GetInvoicesAsync(int patientId, int userId, int page, int pageSize, CancellationToken ct = default)
    {
        // Ownership stays in both SQL queries, independent of the earlier service check.
        var query = db.Invoices.AsNoTracking().Where(i => i.Visit.PatientId == patientId && i.Visit.Patient.UserId == userId);
        var count = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(i => i.CreatedAt).ThenByDescending(i => i.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(i => new { i.Id, i.InvoiceCode, Status = (int)i.Status, i.TotalAmount, i.PaidAmount, i.CreatedAt,
                i.Visit.Patient.PatientNumber, PatientName = i.Visit.Patient.FullName }).ToListAsync(ct);
        return PagedResult<InvoiceLookupResponse>.Create(rows.Select(i => new InvoiceLookupResponse(i.Id, i.InvoiceCode, i.Status,
            i.TotalAmount, i.PaidAmount, i.TotalAmount - i.PaidAmount, "BN" + i.PatientNumber.ToString("D6", CultureInfo.InvariantCulture),
            i.PatientName, i.CreatedAt)).ToList(), count, page, pageSize);
    }
    public async Task<PagedResult<InvoicePortalPaymentResponse>?> GetPaymentsAsync(int invoiceId, int patientId, int userId, int page, int pageSize, CancellationToken ct = default)
    {
        if (!await db.Invoices.AsNoTracking().AnyAsync(i => i.Id == invoiceId && i.Visit.PatientId == patientId && i.Visit.Patient.UserId == userId, ct)) return null;
        var query = db.PaymentTransactions.AsNoTracking().Where(p => p.InvoiceId == invoiceId &&
            p.Invoice.Visit.PatientId == patientId && p.Invoice.Visit.Patient.UserId == userId);
        var count = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(p => p.PaidAt).ThenByDescending(p => p.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new InvoicePortalPaymentResponse(p.Id, p.InvoiceId, p.Amount, p.PaymentMethod, p.PaidAt, p.Source == "Simulation")).ToListAsync(ct);
        return PagedResult<InvoicePortalPaymentResponse>.Create(rows, count, page, pageSize);
    }
    public async Task<PagedResult<InvoicePortalItemResponse>?> GetItemsAsync(int invoiceId, int patientId, int userId, int page, int pageSize, CancellationToken ct = default)
    {
        if (!await db.Invoices.AsNoTracking().AnyAsync(i => i.Id == invoiceId && i.Visit.PatientId == patientId && i.Visit.Patient.UserId == userId, ct)) return null;
        // Repeat ownership for count and rows; use stored invoice snapshots, not current catalogue prices.
        var query = db.InvoiceItems.AsNoTracking().Where(item => item.InvoiceId == invoiceId &&
            item.Invoice.Visit.PatientId == patientId && item.Invoice.Visit.Patient.UserId == userId);
        var count = await query.CountAsync(ct);
        var rows = await query.OrderBy(item => item.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(item => new InvoicePortalItemResponse(item.Id, item.Code, item.Name, item.Quantity, item.UnitPrice, item.TotalAmount)).ToListAsync(ct);
        return PagedResult<InvoicePortalItemResponse>.Create(rows, count, page, pageSize);
    }
}
