using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Services;
using Dental.Application.Features.Queue.Services;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Repositories;
using Dental.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

internal static class VisitCancellationHttpSmoke
{
    private const string FinancialAudit = "MOD_BIL_DRAFT_CANCELLED_WITH_VISIT";
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int actorId,
        Action<string, int> authenticate, Action<bool, string> check)
    {
        await using var db = new DentalDbContext(options);
        if (!db.Database.GetDbConnection().Database.StartsWith("dental_cash_test_", StringComparison.Ordinal))
            throw new InvalidOperationException("Owned test database required");
        var clock = new VietnamClock(TimeProvider.System);
        async Task<User> Staff(string code)
        {
            var role = await db.Roles.SingleOrDefaultAsync(r => r.RoleCode == code);
            if (role is null) { role = new() { RoleCode = code, RoleName = code }; db.Roles.Add(role); await db.SaveChangesAsync(); }
            var user = new User { RoleId = role.RoleId, FullName = "Synthetic cancellation staff", IsActive = true,
                Phone = "VC" + Guid.NewGuid().ToString("N")[..14], PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()) };
            db.Users.Add(user); await db.SaveChangesAsync();
            if (code == RoleCodes.Dentist) await TestChairFixtures.EnsureAsync(db, user.UserId, actorId);
            db.ChangeTracker.Clear(); return user;
        }
        async Task<QueueEntry> Started(User doctor)
        {
            var patient = new Patient { FullName = "Synthetic cancellation patient", Phone = "0999999907", DateOfBirth = new(1990, 1, 1) };
            var row = new QueueEntry { Patient = patient, DentistId = doctor.UserId, QueueDate = clock.Today,
                QueueNumber = (await db.QueueEntries.MaxAsync(q => (int?)q.QueueNumber) ?? 0) + 1,
                CheckInTime = DateTime.UtcNow, Status = QueueStatus.Waiting,
                Visit = new Visit { Patient = patient, Status = VisitStatuses.Created, CreatedByUserId = actorId } };
            db.QueueEntries.Add(row); await db.SaveChangesAsync(); db.ChangeTracker.Clear(); authenticate(RoleCodes.Dentist, doctor.UserId);
            using var response = await client.PutAsJsonAsync($"/api/queue/{row.QueueEntryId}/status", new { newStatus = 2 });
            if (!response.IsSuccessStatusCode) throw new Exception("Cannot start cancellation fixture");
            return row;
        }
        async Task<Invoice> Draft(QueueEntry row, User doctor)
        {
            var service = new DentalService { Code = "SYN-" + Guid.NewGuid().ToString("N"), Name = "Synthetic snapshot", DurationMinutes = 15 };
            var price = new ServicePrice { Service = service, Amount = 100005, EffectiveFrom = DateTime.UtcNow.AddDays(-1), CreatedByUserId = actorId };
            db.VisitServices.Add(new() { VisitId = row.VisitId!.Value, Service = service, Price = price, ServiceCode = "SYN", ServiceName = service.Name,
                Quantity = 1, UnitPrice = 100005, CreatedByUserId = doctor.UserId, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            authenticate(RoleCodes.Dentist, doctor.UserId);
            using var response = await client.GetAsync($"/api/visits/{row.VisitId}/invoice-draft");
            if (!response.IsSuccessStatusCode) throw new Exception("Cannot synchronize Draft fixture");
            var invoice = await db.Invoices.SingleAsync(i => i.VisitId == row.VisitId && i.Status == InvoiceStatus.Draft);
            // Synthetic financial snapshot retained exactly across cancellation.
            invoice.TotalAmount = 100005; invoice.DiscountAmount = 1000;
            await db.SaveChangesAsync(); db.ChangeTracker.Clear(); return invoice;
        }
        async Task<HttpStatusCode> Cancel(QueueEntry row, string? reason = "Synthetic cancellation reason")
        { using var response = await client.PutAsJsonAsync($"/api/queue/{row.QueueEntryId}/status", new { newStatus = 4, reason }); return response.StatusCode; }
        async Task<DentalChair> Chair(QueueEntry row)
        { var chairId = await db.Visits.Where(v => v.VisitId == row.VisitId).Select(v => v.ChairId).SingleAsync(); return await db.DentalChairs.AsNoTracking().SingleAsync(c => c.Id == chairId); }
        QueueService Service(DentalDbContext context)
        {
            var visits = new VisitRepository(context); var patients = new PatientRepository(context); var audit = new AuditLogger(context, NullLogger<AuditLogger>.Instance);
            return new(new QueueRepository(context), new AppointmentRepository(context), patients, visits, new InvoiceRepository(context), new UserRepository(context), new UnitOfWork(context), audit, clock,
                new InvoiceDraftService(new InvoiceRepository(context), visits, patients, audit, clock), TestChairFixtures.Service(context, clock));
        }
        var doctor = await Staff(RoleCodes.Dentist); var otherDoctor = await Staff(RoleCodes.Dentist);
        var admin = await Staff(RoleCodes.Admin); var assistant = await Staff(RoleCodes.Assistant);
        var row = await Started(doctor); var invoice = await Draft(row, doctor); var priorChair = await Chair(row);
        client.DefaultRequestHeaders.Authorization = null;
        check(await Cancel(row) == HttpStatusCode.Unauthorized, "Anonymous cannot cancel clinical visit401");
        foreach (var role in new[] { RoleCodes.Assistant, RoleCodes.Receptionist, RoleCodes.Patient })
        { authenticate(role, assistant.UserId); check(await Cancel(row) == HttpStatusCode.Forbidden, "Nonclinical role cannot trigger derived Draft cancellation403"); }
        authenticate(RoleCodes.Dentist, otherDoctor.UserId); check(await Cancel(row) == HttpStatusCode.Forbidden, "Foreign dentist cannot cancel clinical invoice403");
        authenticate(RoleCodes.Dentist, doctor.UserId);
        check(await Cancel(row, " ") == HttpStatusCode.BadRequest, "Blank cancellation reason400");
        check(await Cancel(row, new string('x', 501)) == HttpStatusCode.BadRequest, "Overlong cancellation reason400");
        check(await db.Invoices.AnyAsync(i => i.Id == invoice.Id && i.Status == InvoiceStatus.Draft) && (await Chair(row)).Version == priorChair.Version,
            "Invalid cancellations leave invoice and chair unchanged");
        using (var response = await client.GetAsync($"/api/queue/{row.QueueEntryId}"))
        {
            var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            check(json.GetProperty("entry").GetProperty("canCancelConsultation").GetBoolean(), "Internal queue exposes active cancellation eligibility");
        }
        check(await Cancel(row) == HttpStatusCode.OK, "Assigned dentist cancels visit and Draft200 DL264");
        var stored = await db.Invoices.AsNoTracking().Include(i => i.Items).SingleAsync(i => i.Id == invoice.Id);
        check(stored.Status == InvoiceStatus.Cancelled && stored.InvoiceCode == invoice.InvoiceCode && stored.TotalAmount == 100005 && stored.DiscountAmount == 1000 &&
            stored.Items.Single().Code == "SYN" && stored.Items.Single().TotalAmount == 100005, "Cancelled invoice retains identity code amounts discount and item snapshots");
        check(await db.Visits.AnyAsync(v => v.VisitId == row.VisitId && v.Status == VisitStatuses.Cancelled && v.ChairId == priorChair.Id) &&
            await db.QueueEntries.AnyAsync(q => q.QueueEntryId == row.QueueEntryId && q.Status == QueueStatus.Cancelled) &&
            (await Chair(row)).Status == DentalChairStatus.Available && (await Chair(row)).Version == priorChair.Version + 1, "Visit queue chair commit with cancelled invoice");
        check(await db.QueueStatusHistories.AnyAsync(h => h.QueueEntryId == row.QueueEntryId && h.ToStatus == QueueStatus.Cancelled && h.Reason == "Synthetic cancellation reason" && h.ChangedByUserId == doctor.UserId), "Clinical reason and actor retained in queue history");
        var financialAudit = await db.AuditLogs.AsNoTracking().SingleAsync(a => a.Action == FinancialAudit && a.EntityId == invoice.Id);
        check(financialAudit.UserId == doctor.UserId && financialAudit.Detail!.Contains("visit_cancelled") && !financialAudit.Detail.Contains("Synthetic cancellation reason"), "Financial audit links trigger without clinical text");
        check(await db.AuditLogs.AnyAsync(a => a.Action == AuditActions.QueueStatusChanged && a.EntityId == row.QueueEntryId), "Queue audit retained alongside financial audit");
        check(await Cancel(row) == HttpStatusCode.BadRequest, "Repeated cancellation rejected by terminal state400 without another derived audit");
        check(await db.AuditLogs.CountAsync(a => a.Action == FinancialAudit && a.EntityId == invoice.Id) == 1, "Exactly one financial cancellation audit");
        using (var response = await client.GetAsync($"/api/visits/{row.VisitId}/invoice-draft")) check(response.StatusCode == HttpStatusCode.NotFound, "Cancelled visit does not regenerate Draft404");
        check(await db.Invoices.CountAsync(i => i.VisitId == row.VisitId) == 1, "No replacement invoice created after cancellation");
        var noDraft = await Started(doctor); check(await Cancel(noDraft) == HttpStatusCode.OK, "Cancellation without Draft remains valid200");
        check(!await db.Invoices.AnyAsync(i => i.VisitId == noDraft.VisitId), "No synthetic cancelled invoice created for visit without Draft");
        var adminRow = await Started(doctor); var adminInvoice = await Draft(adminRow, doctor); authenticate(RoleCodes.Admin, admin.UserId);
        check(await Cancel(adminRow) == HttpStatusCode.OK && await db.AuditLogs.AnyAsync(a => a.Action == FinancialAudit && a.EntityId == adminInvoice.Id && a.UserId == admin.UserId), "Admin cancellation records actual financial actor");

        foreach (var state in new[] { QueueStatus.InImaging, QueueStatus.Waiting, QueueStatus.Calling })
        {
            var phaseDoctor = await Staff(RoleCodes.Dentist); var phase = await Started(phaseDoctor); var draft = await Draft(phase, phaseDoctor);
            var tracked = await db.QueueEntries.SingleAsync(q => q.QueueEntryId == phase.QueueEntryId); tracked.Status = state; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            check(await Cancel(phase) == HttpStatusCode.OK && await db.Invoices.AnyAsync(i => i.Id == draft.Id && i.Status == InvoiceStatus.Cancelled), "Imaging and returning active phases cancel Draft atomically");
        }
        foreach (var invalid in new[] { "pending", "paidAmount", "receipt", "historicalReceipt" })
        {
            var blockedDoctor = await Staff(RoleCodes.Dentist); var blocked = await Started(blockedDoctor); var draft = await Draft(blocked, blockedDoctor);
            var tracked = await db.Invoices.SingleAsync(i => i.Id == draft.Id);
            if (invalid == "pending") tracked.Status = InvoiceStatus.PendingPayment;
            if (invalid == "paidAmount") tracked.PaidAmount = 1;
            if (invalid is "receipt" or "historicalReceipt")
            {
                var target = tracked;
                if (invalid == "historicalReceipt")
                {
                    target = new() { VisitId = blocked.VisitId!.Value, InvoiceCode = "HIST-" + Guid.NewGuid().ToString("N"), CreatedByUserId = actorId, Status = InvoiceStatus.Cancelled, TotalAmount = 10 };
                    db.Invoices.Add(target);
                }
                db.PaymentTransactions.Add(new() { Invoice = target, Amount = 1, AmountTendered = 1, ChangeAmount = 0, CashierId = actorId, PaidAt = DateTime.UtcNow, RequestId = Guid.NewGuid() });
            }
            await db.SaveChangesAsync(); db.ChangeTracker.Clear(); var before = await Chair(blocked);
            check(await Cancel(blocked) == HttpStatusCode.Conflict, "Unexpected financial state blocks clinical cancellation409: " + invalid);
            check(await db.Visits.AnyAsync(v => v.VisitId == blocked.VisitId && v.Status == VisitStatuses.InProgress) &&
                await db.QueueEntries.AnyAsync(q => q.QueueEntryId == blocked.QueueEntryId && q.Status == QueueStatus.InConsultation) && (await Chair(blocked)).Version == before.Version &&
                !await db.AuditLogs.AnyAsync(a => a.Action == FinancialAudit && a.EntityId == draft.Id), "Rejected financial state commits no clinical chair or audit changes: " + invalid);
        }
        var unavailableDoctor = await Staff(RoleCodes.Dentist); var unavailable = await Started(unavailableDoctor); var unavailableInvoice = await Draft(unavailable, unavailableDoctor);
        var unavailableChair = await Chair(unavailable); db.Attach(unavailableChair); unavailableChair.Status = DentalChairStatus.Available; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        check(await Cancel(unavailable) == HttpStatusCode.Conflict, "Inconsistent chair rejects cancellation after financial staging409");
        check(await db.Invoices.AnyAsync(i => i.Id == unavailableInvoice.Id && i.Status == InvoiceStatus.Draft) &&
            !await db.AuditLogs.AnyAsync(a => a.Action == FinancialAudit && a.EntityId == unavailableInvoice.Id), "Failure after staging commits neither cancelled invoice nor financial audit");
        var inactiveDoctor = await Staff(RoleCodes.Dentist); var inactive = await Started(inactiveDoctor); var inactiveInvoice = await Draft(inactive, inactiveDoctor);
        db.Attach(inactiveDoctor); inactiveDoctor.IsActive = false; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        check(await Cancel(inactive) == HttpStatusCode.Forbidden && await db.Invoices.AnyAsync(i => i.Id == inactiveInvoice.Id && i.Status == InvoiceStatus.Draft), "Inactive actor with stale JWT cannot trigger financial cancellation403");
        foreach (var action in new[] { FinancialAudit, AuditActions.QueueStatusChanged })
        {
            var faultDoctor = await Staff(RoleCodes.Dentist); var fault = await Started(faultDoctor); var draft = await Draft(fault, faultDoctor); var before = await Chair(fault);
            var faultOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new FailAudit(action)).Options;
            await using var context = new DentalDbContext(faultOptions); var failed = false;
            try { await Service(context).UpdateQueueStatusAsync(fault.QueueEntryId, faultDoctor.UserId, RoleCodes.Dentist, new(4, Reason: "Synthetic rollback"), null); }
            catch (InvalidOperationException e) when (e.Message == "Synthetic cancellation audit fault") { failed = true; }
            check(failed, "Required audit failure propagates: " + action);
            check(await db.Invoices.AnyAsync(i => i.Id == draft.Id && i.Status == InvoiceStatus.Draft) &&
                await db.Visits.AnyAsync(v => v.VisitId == fault.VisitId && v.Status == VisitStatuses.InProgress) &&
                await db.QueueEntries.AnyAsync(q => q.QueueEntryId == fault.QueueEntryId && q.Status == QueueStatus.InConsultation) &&
                (await Chair(fault)).Status == DentalChairStatus.Occupied && (await Chair(fault)).Version == before.Version &&
                !await db.QueueStatusHistories.AnyAsync(h => h.QueueEntryId == fault.QueueEntryId && h.ToStatus == QueueStatus.Cancelled) &&
                !await db.AuditLogs.AnyAsync(a => a.Action == FinancialAudit && a.EntityId == draft.Id), "Audit failure rolls back invoice Visit queue chair version history and audit");
        }
        var raceDoctor = await Staff(RoleCodes.Dentist); var race = await Started(raceDoctor); var raceInvoice = await Draft(race, raceDoctor);
        var raceOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new SaveBarrier()).Options;
        async Task<bool> ConcurrentCancel()
        { await using var context = new DentalDbContext(raceOptions); return (await Service(context).UpdateQueueStatusAsync(race.QueueEntryId, raceDoctor.UserId, RoleCodes.Dentist, new(4, Reason: "Synthetic concurrent cancellation"), null)).IsSuccess; }
        check((await Task.WhenAll(ConcurrentCancel(), ConcurrentCancel())).Count(success => success) == 1, "Two concurrent cancellations have exactly one winner");
        check(await db.AuditLogs.CountAsync(a => a.Action == FinancialAudit && a.EntityId == raceInvoice.Id) == 1 &&
            await db.QueueStatusHistories.CountAsync(h => h.QueueEntryId == race.QueueEntryId && h.ToStatus == QueueStatus.Cancelled) == 1, "Cancellation race preserves single invoice audit and history");
        var syncDoctor = await Staff(RoleCodes.Dentist); var sync = await Started(syncDoctor); var syncInvoice = await Draft(sync, syncDoctor);
        var syncOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new SaveBarrier()).Options;
        async Task<bool> CancelWhileSyncing()
        { await using var context = new DentalDbContext(syncOptions); return (await Service(context).UpdateQueueStatusAsync(sync.QueueEntryId, syncDoctor.UserId, RoleCodes.Dentist, new(4, Reason: "Synthetic sync cancellation"), null)).IsSuccess; }
        async Task<bool> SyncWhileCancelling()
        { await using var context = new DentalDbContext(syncOptions); return (await new InvoiceRepository(context).SynchronizeDraftAsync(sync.VisitId!.Value, syncDoctor.UserId, clock.Today)).IsSuccess; }
        var outcomes = await Task.WhenAll(CancelWhileSyncing(), SyncWhileCancelling());
        check(outcomes.Any(success => success), "Concurrent sync and cancellation returns controlled outcomes");
        authenticate(RoleCodes.Dentist, syncDoctor.UserId);
        var retrySucceeded = !await db.Visits.AnyAsync(v => v.VisitId == sync.VisitId && v.Status == VisitStatuses.InProgress) || await Cancel(sync) == HttpStatusCode.OK;
        check(retrySucceeded, "If sync wins, explicit retry cancels refreshed visit successfully");
        check(await db.Invoices.CountAsync(i => i.VisitId == sync.VisitId && i.Status != InvoiceStatus.Cancelled) == 0 &&
            await db.Visits.AnyAsync(v => v.VisitId == sync.VisitId && v.Status == VisitStatuses.Cancelled), "Draft sync cannot resurrect active invoice after cancelled visit");
        await using var outside = new DentalDbContext(options); var requiresTransaction = false;
        try { await new InvoiceRepository(outside).StageVisitCancellationAsync(sync.VisitId!.Value, syncDoctor.UserId, RoleCodes.Dentist, null); }
        catch (InvalidOperationException) { requiresTransaction = true; }
        check(requiresTransaction, "Derived invoice cancellation cannot run as independent persistence operation");
    }
    private sealed class FailAudit(string action) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (data.Context!.ChangeTracker.Entries<AuditLog>().Any(e => e.State == EntityState.Added && e.Entity.Action == action))
                throw new InvalidOperationException("Synthetic cancellation audit fault");
            return ValueTask.FromResult(result);
        }
    }
    private sealed class SaveBarrier : SaveChangesInterceptor
    {
        private readonly ConcurrentDictionary<Guid, byte> seen = new();
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (seen.TryAdd(data.Context!.ContextId.InstanceId, 0))
            { if (seen.Count == 2) ready.TrySetResult(); await ready.Task.WaitAsync(TimeSpan.FromSeconds(15), ct); }
            return result;
        }
    }
}
