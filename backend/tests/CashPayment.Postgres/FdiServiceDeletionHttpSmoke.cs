using System.Net;
using System.Net.Http.Json;
using Dental.Application.Features.MOD_FDI.DTOs;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
internal static class FdiServiceDeletionHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int actorId,
        int patientId, Action<string> authenticate, Action<bool, string> check)
    {
        await using var db = new DentalDbContext(options);
        var catalog = new DentalService { Code = "TEST-FDI-DELETE", Name = "Dịch vụ kiểm thử", DurationMinutes = 10 };
        var price = new ServicePrice { Service = catalog, Amount = 50000, EffectiveFrom = DateTime.UtcNow, CreatedByUserId = actorId };
        db.ServicePrices.Add(price); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        async Task<(Visit Visit, VisitService Row, Invoice Invoice)> Fixture(InvoiceStatus status = InvoiceStatus.Draft)
        {
            var visit = new Visit { PatientId = patientId, DentistId = actorId, CreatedByUserId = actorId, Status = VisitStatuses.InProgress };
            var row = new VisitService { Visit = visit, ServiceId = catalog.DentalServiceId, ServicePriceId = price.ServicePriceId,
                ServiceCode = catalog.Code, ServiceName = catalog.Name, UnitPrice = 50000, Quantity = 2, CreatedByUserId = actorId, CreatedAt = DateTime.UtcNow };
            db.VisitServices.Add(row); await db.SaveChangesAsync();
            var invoice = new Invoice { VisitId = visit.VisitId, InvoiceCode = "TEST-FDI-" + Guid.NewGuid().ToString("N"),
                Status = status, TotalAmount = 100000, CreatedByUserId = actorId, CreatedAt = DateTime.UtcNow,
                Items = [new() { SourceVisitServiceId = row.Id, Code = row.ServiceCode, Name = row.ServiceName, Quantity = 2, UnitPrice = 50000, TotalAmount = 100000 }] };
            db.Invoices.Add(invoice); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            return (visit, row, invoice);
        }
        async Task<HttpStatusCode> Delete(int visit, int row, string reason = "Điều chỉnh chỉ định")
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/visits/{visit}/services/{row}") { Content = JsonContent.Create(new DeleteVisitServiceBody(reason)) };
            using var response = await client.SendAsync(request); return response.StatusCode;
        }
        var f = await Fixture();
        client.DefaultRequestHeaders.Authorization = null;
        check(await Delete(f.Visit.VisitId, f.Row.Id) == HttpStatusCode.Unauthorized, "FDI delete anonymous 401");
        foreach (var role in new[] { RoleCodes.Receptionist, RoleCodes.Patient, RoleCodes.Assistant })
        { authenticate(role); check(await Delete(f.Visit.VisitId, f.Row.Id) == HttpStatusCode.Forbidden, $"FDI delete {role} 403"); }
        authenticate(RoleCodes.Admin);
        check(await Delete(0, f.Row.Id) == HttpStatusCode.BadRequest, "FDI delete invalid visit 400");
        check(await Delete(f.Visit.VisitId, 0) == HttpStatusCode.BadRequest, "FDI delete invalid assignment 400");
        check(await Delete(f.Visit.VisitId, f.Row.Id, " ") == HttpStatusCode.BadRequest, "FDI delete blank reason 400");
        check(await Delete(f.Visit.VisitId, f.Row.Id, new string('x', 501)) == HttpStatusCode.BadRequest, "FDI delete long reason 400");
        check(await Delete(int.MaxValue, f.Row.Id) == HttpStatusCode.NotFound, "FDI delete missing visit 404");
        check(await Delete(f.Visit.VisitId, int.MaxValue) == HttpStatusCode.NotFound, "FDI delete wrong assignment 404");
        var visit = await db.Visits.SingleAsync(v => v.VisitId == f.Visit.VisitId);
        visit.DentistId = null; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        authenticate(RoleCodes.Dentist);
        check(await Delete(f.Visit.VisitId, f.Row.Id) == HttpStatusCode.Forbidden, "FDI dentist must own visit");
        authenticate(RoleCodes.Admin);
        visit = await db.Visits.SingleAsync(v => v.VisitId == f.Visit.VisitId); visit.IsLocked = true; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        check(await Delete(f.Visit.VisitId, f.Row.Id) == HttpStatusCode.Conflict, "FDI locked visit 409");
        visit = await db.Visits.SingleAsync(v => v.VisitId == f.Visit.VisitId); visit.IsLocked = false; visit.Status = VisitStatuses.Completed;
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        check(await Delete(f.Visit.VisitId, f.Row.Id) == HttpStatusCode.Conflict, "FDI completed visit 409");
        visit = await db.Visits.SingleAsync(v => v.VisitId == f.Visit.VisitId); visit.Status = VisitStatuses.InProgress;
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        // Test-only invalid audit actor proves deletion + invoice update roll back as a unit.
        await using (var failing = new DentalDbContext(options))
        {
            try { await new FdiServiceDeletionRepository(failing).DeleteAsync(new(f.Visit.VisitId, f.Row.Id, "Kiểm thử rollback"), int.MaxValue, RoleCodes.Admin, null); throw new InvalidOperationException("Expected audit FK error."); }
            catch (DbUpdateException) { }
        }
        check(await db.VisitServices.AnyAsync(s => s.Id == f.Row.Id) && await db.InvoiceItems.AnyAsync(i => i.InvoiceId == f.Invoice.Id) &&
            (await db.Invoices.AsNoTracking().SingleAsync(i => i.Id == f.Invoice.Id)).TotalAmount == 100000, "FDI audit failure rolls back row and invoice");
        check(await Delete(f.Visit.VisitId, f.Row.Id) == HttpStatusCode.NoContent, "FDI Admin delete 204");
        check(!await db.VisitServices.AnyAsync(s => s.Id == f.Row.Id) && !await db.InvoiceItems.AnyAsync(i => i.InvoiceId == f.Invoice.Id) &&
            (await db.Invoices.AsNoTracking().SingleAsync(i => i.Id == f.Invoice.Id)).TotalAmount == 0, "FDI hard delete last service updates draft to zero");
        var audit = await db.AuditLogs.AsNoTracking().SingleAsync(a => a.Action == "VisitServiceDeleted" && a.EntityId == f.Row.Id);
        check(audit.UserId == actorId && audit.Detail!.Contains("50000") && audit.Detail.Contains("reason"), "FDI audit stores actor, price, reason snapshot");
        check(await Delete(f.Visit.VisitId, f.Row.Id) == HttpStatusCode.NotFound, "FDI duplicate delete 404");
        f = await Fixture(InvoiceStatus.PendingPayment); authenticate(RoleCodes.Dentist);
        check(await Delete(f.Visit.VisitId, f.Row.Id) == HttpStatusCode.NoContent &&
            (await db.Invoices.AsNoTracking().SingleAsync(i => i.Id == f.Invoice.Id)).TotalAmount == 0, "FDI assigned dentist deletes unpaid pending snapshot");
        f = await Fixture();
        var invoice = await db.Invoices.SingleAsync(i => i.Id == f.Invoice.Id); invoice.PaidAmount = 1;
        await db.SaveChangesAsync(); db.ChangeTracker.Clear(); authenticate(RoleCodes.Admin);
        check(await Delete(f.Visit.VisitId, f.Row.Id) == HttpStatusCode.Conflict && await db.VisitServices.AnyAsync(s => s.Id == f.Row.Id), "FDI any paid amount blocks deletion");
        f = await Fixture();
        var cancelled = new Invoice { VisitId = f.Visit.VisitId, InvoiceCode = "TEST-FDI-CANCEL-" + Guid.NewGuid().ToString("N"),
            Status = InvoiceStatus.Cancelled, TotalAmount = 100000, CreatedByUserId = actorId, CreatedAt = DateTime.UtcNow,
            Items = [new() { SourceVisitServiceId = f.Row.Id, Code = catalog.Code, Name = catalog.Name, Quantity = 2, UnitPrice = 50000, TotalAmount = 100000 }] };
        db.Invoices.Add(cancelled);
        db.InvoiceItems.Add(new InvoiceItem { InvoiceId = f.Invoice.Id, SourceVisitServiceId = int.MaxValue, Code = "TEST-OTHER",
            Name = "Dòng kiểm thử khác", Quantity = 1, UnitPrice = 20000, TotalAmount = 20000 });
        invoice = await db.Invoices.SingleAsync(i => i.Id == f.Invoice.Id); invoice.TotalAmount = 120000;
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        check(await Delete(f.Visit.VisitId, f.Row.Id) == HttpStatusCode.NoContent &&
            (await db.Invoices.AsNoTracking().SingleAsync(i => i.Id == f.Invoice.Id)).TotalAmount == 20000 &&
            await db.InvoiceItems.CountAsync(i => i.InvoiceId == f.Invoice.Id) == 1, "FDI preserves unrelated live invoice lines");
        check((await db.Invoices.AsNoTracking().SingleAsync(i => i.Id == cancelled.Id)).TotalAmount == 100000 &&
            await db.InvoiceItems.AnyAsync(i => i.InvoiceId == cancelled.Id && i.SourceVisitServiceId == f.Row.Id), "FDI preserves cancelled invoice snapshot history");
        f = await Fixture();
        db.PaymentTransactions.Add(new PaymentTransaction { InvoiceId = f.Invoice.Id, Amount = 1, CashierId = actorId,
            PaidAt = DateTime.UtcNow, RequestId = Guid.NewGuid() });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        check(await Delete(f.Visit.VisitId, f.Row.Id) == HttpStatusCode.Conflict && await db.VisitServices.AnyAsync(s => s.Id == f.Row.Id),
            "FDI recorded payment blocks deletion even if stored PaidAmount is zero");
        f = await Fixture();
        var responses = await Task.WhenAll(Delete(f.Visit.VisitId, f.Row.Id), Delete(f.Visit.VisitId, f.Row.Id));
        check(responses.Contains(HttpStatusCode.NoContent) && responses.All(s => s is HttpStatusCode.NoContent or HttpStatusCode.NotFound or HttpStatusCode.Conflict), "FDI concurrent delete no 500");
        check(await db.AuditLogs.CountAsync(a => a.Action == "VisitServiceDeleted" && a.EntityId == f.Row.Id) == 1, "FDI concurrent deletion writes exactly one audit");
    }
}
