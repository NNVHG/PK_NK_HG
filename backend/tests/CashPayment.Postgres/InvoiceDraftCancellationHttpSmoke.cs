using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

internal static class InvoiceDraftCancellationHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int cashierId,
        int patientId, Action<string, int> authenticate, Action<bool, string> check)
    {
        await using var db = new DentalDbContext(options);
        var adminRole = await db.Roles.SingleOrDefaultAsync(x => x.RoleCode == RoleCodes.Admin);
        if (adminRole is null)
        {
            adminRole = new Role { RoleCode = RoleCodes.Admin, RoleName = "Test administrator" };
            db.Roles.Add(adminRole);
            await db.SaveChangesAsync();
        }
        var admin = new User { RoleId = adminRole.RoleId, FullName = "Synthetic cancellation admin", Phone = "0970000101",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()) };
        db.Users.Add(admin);
        var visit = new Visit { PatientId = patientId, CreatedByUserId = cashierId, Status = VisitStatuses.InProgress };
        var draft = new Invoice { Visit = visit, CreatedByUserId = cashierId, InvoiceCode = "CANCEL-DRAFT-SMOKE",
            TotalAmount = 120000, Status = InvoiceStatus.Draft, CreatedAt = DateTime.UtcNow };
        var pendingVisit = new Visit { PatientId = patientId, CreatedByUserId = cashierId, Status = VisitStatuses.Completed, IsLocked = true };
        var pending = new Invoice { Visit = pendingVisit, CreatedByUserId = cashierId, InvoiceCode = "CANCEL-PENDING-SMOKE",
            TotalAmount = 120000, Status = InvoiceStatus.PendingPayment, CreatedAt = DateTime.UtcNow };
        db.Invoices.AddRange(draft, pending);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var path = $"/api/invoices/{draft.Id}/cancel";
        authenticate(RoleCodes.Receptionist, cashierId);
        using (var forbidden = await client.PostAsJsonAsync(path, new { reason = "sai hóa đơn" }))
            check(forbidden.StatusCode == HttpStatusCode.Forbidden, "Receptionist cannot cancel a Draft invoice");

        authenticate(RoleCodes.Admin, admin.UserId);
        using (var invalid = await client.PostAsJsonAsync(path, new { reason = "   " }))
            check(invalid.StatusCode == HttpStatusCode.BadRequest, "Blank cancellation reason is rejected");
        using (var invalidLong = await client.PostAsJsonAsync(path, new { reason = new string('x', 501) }))
            check(invalidLong.StatusCode == HttpStatusCode.BadRequest, "Cancellation reason over 500 characters is rejected");
        using (var cancelled = await client.PostAsJsonAsync(path, new { reason = "   tạo nhầm hóa đơn   " }))
            check(cancelled.StatusCode == HttpStatusCode.NoContent && cancelled.Headers.CacheControl?.NoStore == true,
                "Admin cancels Draft with no-store response");
        using (var repeated = await client.PostAsJsonAsync(path, new { reason = "gửi lại" }))
            check(repeated.StatusCode == HttpStatusCode.Conflict, "Repeated cancellation does not rewrite history");
        using (var pendingCancel = await client.PostAsJsonAsync($"/api/invoices/{pending.Id}/cancel", new { reason = "không phải Draft" }))
            check(pendingCancel.StatusCode == HttpStatusCode.Conflict, "PendingPayment invoice cannot use Draft cancellation");

        var saved = await db.Invoices.Include(x => x.Items).SingleAsync(x => x.Id == draft.Id);
        var audit = await db.AuditLogs.SingleAsync(x => x.EntityType == "Invoice" && x.EntityId == draft.Id && x.Action == "MOD_BIL_DRAFT_CANCELLED");
        using var detail = JsonDocument.Parse(audit.Detail!);
        check(saved.Status == InvoiceStatus.Cancelled && saved.TotalAmount == 120000 && audit.UserId == admin.UserId &&
            detail.RootElement.GetProperty("reason").GetString() == "tạo nhầm hóa đơn",
            "Cancellation preserves invoice values and writes actor, trimmed reason, and audit atomically");
    }
}
