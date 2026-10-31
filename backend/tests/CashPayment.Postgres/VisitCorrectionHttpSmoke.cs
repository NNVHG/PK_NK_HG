using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.Services;
using Dental.Application.Features.MOD_FDI.Services;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Repositories;
using Dental.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using DiagnosisService = Dental.Application.Features.Visits.Services.VisitService;

internal static class VisitCorrectionHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int seedActorId,
        int patientId, Action<string, int> authenticate, Action<bool, string> check)
    {
        await using var db = new DentalDbContext(options);
        if (!db.Database.GetDbConnection().Database.StartsWith("dental_cash_test_", StringComparison.Ordinal))
            throw new InvalidOperationException("Owned test database required");
        var clock = new VietnamClock(TimeProvider.System);
        async Task<User> Staff(string code)
        {
            var role = await db.Roles.SingleOrDefaultAsync(r => r.RoleCode == code);
            if (role is null) { role = new() { RoleCode = code, RoleName = code }; db.Roles.Add(role); await db.SaveChangesAsync(); }
            var user = new User { RoleId = role.RoleId, FullName = "Synthetic correction staff", IsActive = true,
                Phone = "COR" + Guid.NewGuid().ToString("N")[..14], PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()) };
            db.Users.Add(user); await db.SaveChangesAsync(); db.ChangeTracker.Clear(); return user;
        }
        var admin = await Staff(RoleCodes.Admin); var dentist = await Staff(RoleCodes.Dentist); var other = await Staff(RoleCodes.Dentist);
        var catalog = new DentalService { Code = "CORR-SERVICE", Name = "Synthetic correction service", DurationMinutes = 15,
            Prices = [new() { Amount = 100000, EffectiveFrom = DateTime.UtcNow.AddDays(-1), CreatedByUserId = seedActorId }] };
        db.DentalServices.Add(catalog); await db.SaveChangesAsync(); var price = catalog.Prices.Single(); db.ChangeTracker.Clear();
        async Task<(Visit Visit, QueueEntry Queue, Invoice Invoice)> Completed()
        {
            var visit = new Visit { PatientId = patientId, DentistId = dentist.UserId, CreatedByUserId = seedActorId,
                Status = VisitStatuses.Completed, IsLocked = true, LockedBy = dentist.UserId, LockedAt = DateTime.UtcNow.AddHours(-1),
                StartedAt = DateTime.UtcNow.AddHours(-2), EndedAt = DateTime.UtcNow.AddHours(-1), Diagnosis = "Synthetic original" };
            var row = new QueueEntry { PatientId = patientId, DentistId = dentist.UserId, Visit = visit, QueueDate = clock.Today,
                QueueNumber = (await db.QueueEntries.MaxAsync(q => (int?)q.QueueNumber) ?? 0) + 1,
                Status = QueueStatus.Completed, CheckInTime = DateTime.UtcNow.AddHours(-3) };
            db.QueueEntries.Add(row); await db.SaveChangesAsync();
            var assignment = new Dental.Domain.Entities.VisitService { VisitId = visit.VisitId, ServiceId = catalog.DentalServiceId,
                ServicePriceId = price.ServicePriceId, ServiceCode = catalog.Code, ServiceName = catalog.Name,
                Quantity = 1, UnitPrice = 100000, CreatedByUserId = dentist.UserId, CreatedAt = DateTime.UtcNow.AddHours(-1) };
            db.VisitServices.Add(assignment); await db.SaveChangesAsync();
            var invoice = new Invoice { VisitId = visit.VisitId, InvoiceCode = "CORR-" + Guid.NewGuid().ToString("N"),
                Status = InvoiceStatus.PendingPayment, CreatedByUserId = seedActorId, CreatedAt = DateTime.UtcNow, TotalAmount = 100000,
                Items = [new() { SourceVisitServiceId = assignment.Id, Code = catalog.Code, Name = catalog.Name,
                    Quantity = 1, UnitPrice = 100000, TotalAmount = 100000 }] };
            db.Invoices.Add(invoice); await db.SaveChangesAsync(); db.ChangeTracker.Clear(); visit = await db.Visits.AsNoTracking().SingleAsync(v => v.VisitId == visit.VisitId); return (visit, row, invoice);
        }
        async Task Open(int id)
        {
            authenticate(RoleCodes.Admin, admin.UserId);
            using var response = await client.PostAsJsonAsync($"/api/visits/{id}/unlock", new { unlockReason = "Synthetic correction reason protected" });
            check(response.StatusCode == HttpStatusCode.NoContent, "Admin opens separate correction204");
        }
        var target = await Completed(); var root = $"/api/visits/{target.Visit.VisitId}";
        var chair = new DentalChair { ChairNumber = "CORR-CHAIR", RoomName = "Synthetic occupied room", IsActive = true,
            Status = DentalChairStatus.Occupied, CreatedByUserId = admin.UserId };
        db.DentalChairs.Add(chair); await db.SaveChangesAsync();
        var workDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "SE Asia Standard Time"));
        db.StaffWorkspaces.Add(new() { UserId = dentist.UserId, ChairId = chair.Id, WorkDate = workDate, UpdatedByUserId = admin.UserId });
        var active = new Visit { PatientId = patientId, DentistId = dentist.UserId, CreatedByUserId = admin.UserId,
            Status = VisitStatuses.InProgress, StartedAt = DateTime.UtcNow, Diagnosis = "Synthetic active visit" };
        db.Visits.Add(active); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        client.DefaultRequestHeaders.Authorization = null;
        using (var r = await client.PostAsJsonAsync(root + "/unlock", new { unlockReason = "Synthetic correction" })) check(r.StatusCode == HttpStatusCode.Unauthorized, "Anonymous cannot open correction401");
        foreach (var role in new[] { RoleCodes.Dentist, RoleCodes.Receptionist, RoleCodes.Assistant, RoleCodes.Patient })
        {
            authenticate(role, role == RoleCodes.Dentist ? dentist.UserId : seedActorId);
            using var r = await client.PostAsJsonAsync(root + "/unlock", new { unlockReason = "Synthetic correction" });
            check(r.StatusCode == HttpStatusCode.Forbidden, "Only Admin can open correction403");
        }
        authenticate(RoleCodes.Admin, admin.UserId);
        using (var r = await client.PostAsJsonAsync(root + "/unlock", new { unlockReason = "short" })) check(r.StatusCode == HttpStatusCode.BadRequest, "Correction still validates reason400");
        await Open(target.Visit.VisitId);
        var stored = await db.Visits.AsNoTracking().SingleAsync(v => v.VisitId == target.Visit.VisitId);
        check(stored.Status == VisitStatuses.Correcting && !stored.IsLocked, "Correction has distinct editable status");
        check(stored.StartedAt == target.Visit.StartedAt && stored.EndedAt == target.Visit.EndedAt, "Correction preserves physical clinical timestamps");
        check(await db.QueueEntries.AsNoTracking().AnyAsync(q => q.QueueEntryId == target.Queue.QueueEntryId && q.Status == QueueStatus.Completed) &&
            !await db.QueueStatusHistories.AnyAsync(h => h.QueueEntryId == target.Queue.QueueEntryId), "Correction keeps queue completed without fake transition");
        check(await db.Visits.AsNoTracking().AnyAsync(v => v.VisitId == active.VisitId && v.Status == VisitStatuses.InProgress && v.Diagnosis == "Synthetic active visit"), "Other current consultation of same patient/dentist is unaffected");
        check(await db.DentalChairs.AsNoTracking().AnyAsync(c => c.Id == chair.Id && c.Status == DentalChairStatus.Occupied && c.Version == 1), "Correction never modifies occupied chair or its version");
        check(await db.Invoices.AsNoTracking().AnyAsync(i => i.Id == target.Invoice.Id && i.Status == InvoiceStatus.Cancelled && i.TotalAmount == 100000), "Prior invoice remains cancelled with original money");
        check(await db.InvoiceItems.CountAsync(i => i.InvoiceId == target.Invoice.Id) == 1, "Cancelled invoice snapshot remains intact");
        check(await db.VisitUnlockRecords.AnyAsync(r => r.VisitId == target.Visit.VisitId && r.Reason == "Synthetic correction reason protected") &&
            await db.AuditLogs.Where(a => a.EntityType == "Visit" && a.EntityId == target.Visit.VisitId).AllAsync(a => !a.Detail!.Contains("reason protected")), "Protected immutable reason retained; audit contains references only");
        using (var r = await client.PostAsJsonAsync(root + "/unlock", new { unlockReason = "Synthetic correction" })) check(r.StatusCode == HttpStatusCode.Conflict, "Repeated open is rejected409");
        client.DefaultRequestHeaders.Authorization = null;
        foreach (var path in new[] { "/api/queue/display", "/api/queue/public-monitor" })
        {
            using var r = await client.GetAsync(path); var json = await r.Content.ReadFromJsonAsync<JsonElement>();
            check(r.StatusCode == HttpStatusCode.OK && r.Headers.CacheControl?.NoStore == true &&
                !json.GetProperty("inConsultation").EnumerateArray().Concat(json.GetProperty("waiting").EnumerateArray()).Any(x => x.GetProperty("queueEntryId").GetInt32() == target.Queue.QueueEntryId), "Anonymous TV excludes correction entirely");
        }
        foreach (var role in new[] { RoleCodes.Receptionist, RoleCodes.Assistant, RoleCodes.Patient })
        {
            authenticate(role, seedActorId);
            using var r = await client.PutAsJsonAsync(root + "/diagnosis", new { diagnosis = "Synthetic denied" });
            check(r.StatusCode == HttpStatusCode.Forbidden, "Correction does not expand diagnosis writer roles403");
            using var complete = await client.PutAsync(root + "/complete", null);
            check(complete.StatusCode == HttpStatusCode.Forbidden, "Correction does not expand completion roles403");
        }
        authenticate(RoleCodes.Dentist, other.UserId);
        using (var r = await client.PutAsJsonAsync(root + "/diagnosis", new { diagnosis = "Synthetic denied" })) check(r.StatusCode == HttpStatusCode.Forbidden, "Unrelated dentist cannot edit correction403");
        using (var r = await client.PutAsync(root + "/complete", null)) check(r.StatusCode == HttpStatusCode.Forbidden, "Unrelated dentist cannot finalize correction403");
        authenticate(RoleCodes.Dentist, dentist.UserId);
        using (var r = await client.PutAsJsonAsync(root + "/diagnosis", new { diagnosis = "Synthetic corrected diagnosis", clinicalNotes = "Synthetic corrected note" }))
            check(r.StatusCode == HttpStatusCode.OK, "Assigned dentist saves corrected diagnosis200");
        int conditionId;
        using (var r = await client.PostAsJsonAsync(root + "/tooth-conditions", new { toothNumber = 11, conditionCode = "CARIES", note = "Synthetic correction note" }))
        { var json = await r.Content.ReadFromJsonAsync<JsonElement>(); conditionId = json.GetProperty("id").GetInt32(); check(r.StatusCode == HttpStatusCode.Created, "Assigned dentist adds corrected FDI condition201"); }
        using (var r = await client.PutAsJsonAsync(root + $"/tooth-conditions/{conditionId}", new { toothNumber = 11, conditionCode = "RESTORED", note = "Synthetic corrected tooth" }))
            check(r.StatusCode == HttpStatusCode.OK, "Assigned dentist updates corrected FDI condition200");
        using (var r = await client.DeleteAsync(root + $"/tooth-conditions/{conditionId}")) check(r.StatusCode == HttpStatusCode.NoContent, "Assigned dentist deletes corrected FDI condition204");
        using (var r = await client.PostAsJsonAsync(root + "/services", new { serviceId = catalog.DentalServiceId, toothNumbers = new[] { 11 }, quantity = 2 }))
            check(r.StatusCode == HttpStatusCode.Created, "Assigned dentist adds correction service with current snapshot201");
        int disposableAssignment;
        using (var r = await client.PostAsJsonAsync(root + "/services", new { serviceId = catalog.DentalServiceId, toothNumbers = new[] { 12 }, quantity = 1 }))
        { var json = await r.Content.ReadFromJsonAsync<JsonElement>(); disposableAssignment = json[0].GetProperty("id").GetInt32(); check(r.StatusCode == HttpStatusCode.Created, "Correction may add another service before review"); }
        using (var request = new HttpRequestMessage(HttpMethod.Delete, root + $"/services/{disposableAssignment}") { Content = JsonContent.Create(new { reason = "Synthetic correction service removal" }) })
        using (var r = await client.SendAsync(request)) check(r.StatusCode == HttpStatusCode.NoContent, "Correction removes mistaken service through audited deletion204");
        using (var r = await client.GetAsync($"/api/invoices/by-visit/{target.Visit.VisitId}"))
        {
            var json = await r.Content.ReadFromJsonAsync<JsonElement>();
            check(r.StatusCode == HttpStatusCode.OK && json.GetProperty("status").GetInt32() == (int)InvoiceStatus.Draft &&
                json.GetProperty("totalAmount").GetDecimal() == 300000 && json.GetProperty("invoiceCode").GetString() != target.Invoice.InvoiceCode, "Correction creates a new Draft/code and synchronizes corrected services");
        }
        using (var r = await client.PutAsync(root + "/complete", null)) check(r.StatusCode == HttpStatusCode.NoContent, "Assigned dentist finalizes correction via existing completion endpoint204");
        stored = await db.Visits.AsNoTracking().SingleAsync(v => v.VisitId == target.Visit.VisitId);
        check(stored.Status == VisitStatuses.Completed && stored.IsLocked && stored.LockedBy == dentist.UserId && stored.EndedAt == target.Visit.EndedAt, "Finalization relocks and preserves physical end time");
        check(await db.Invoices.CountAsync(i => i.VisitId == target.Visit.VisitId && i.Status == InvoiceStatus.PendingPayment && i.TotalAmount == 300000) == 1 &&
            await db.Invoices.CountAsync(i => i.VisitId == target.Visit.VisitId && i.Status == InvoiceStatus.Cancelled) == 1, "Exactly one new pending invoice, cancelled history retained");
        check(await db.AuditLogs.CountAsync(a => a.Action == "MOD_PAT_VISIT_CORRECTION_COMPLETED" && a.EntityId == target.Visit.VisitId) == 1 &&
            !await db.QueueStatusHistories.AnyAsync(h => h.QueueEntryId == target.Queue.QueueEntryId), "Finalization requires audit without queue transition");
        using (var r = await client.PutAsJsonAsync(root + "/diagnosis", new { diagnosis = "Synthetic denied after lock" })) check(r.StatusCode == HttpStatusCode.Conflict, "Completed correction cannot be edited409");
        using (var r = await client.PutAsync(root + "/complete", null)) check(r.StatusCode == HttpStatusCode.Conflict, "Duplicate finalization cannot create another invoice409");

        var paid = await Completed(); var paidInvoice = await db.Invoices.FindAsync(paid.Invoice.Id); paidInvoice!.PaidAmount = 1; paidInvoice.Status = InvoiceStatus.PartiallyPaid;
        await db.SaveChangesAsync(); db.ChangeTracker.Clear(); authenticate(RoleCodes.Admin, admin.UserId);
        using (var r = await client.PostAsJsonAsync($"/api/visits/{paid.Visit.VisitId}/unlock", new { unlockReason = "Synthetic correction" })) check(r.StatusCode == HttpStatusCode.Conflict, "Any prior payment still blocks opening409");
        var isolated = await Completed(); await Open(isolated.Visit.VisitId);
        var current = await db.Visits.FindAsync(active.VisitId); current!.Status = VisitStatuses.Completed; current.IsLocked = true;
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        check(!await new VisitRepository(db).HasActiveVisitForDentistAsync(dentist.UserId), "Correction alone never blocks dentist active-visit check");
        check(!await new VisitRepository(db).HasOpenVisitAsync(patientId), "Correction alone never blocks patient's next check-in");

        var fault = await Completed();
        var failOpenOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new FailAudit("MOD_PAT_VISIT_UNLOCKED")).Options;
        await using (var faultDb = new DentalDbContext(failOpenOptions))
        {
            var failed = false;
            try { await new VisitReopenRepository(faultDb).ReopenAsync(fault.Visit.VisitId, admin.UserId, "Synthetic rollback correction", null); }
            catch (InvalidOperationException e) when (e.Message == "Synthetic correction audit failure") { failed = true; }
            check(failed, "Opening required audit failure propagates");
        }
        check(await db.Visits.AsNoTracking().AnyAsync(v => v.VisitId == fault.Visit.VisitId && v.Status == VisitStatuses.Completed && v.IsLocked) &&
            await db.Invoices.AsNoTracking().AnyAsync(i => i.Id == fault.Invoice.Id && i.Status == InvoiceStatus.PendingPayment) &&
            !await db.VisitUnlockRecords.AnyAsync(r => r.VisitId == fault.Visit.VisitId), "Opening audit failure rolls back Visit/invoice/history");
        await Open(fault.Visit.VisitId);
        var failCompleteOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new FailAudit("MOD_PAT_VISIT_CORRECTION_COMPLETED")).Options;
        var counterBefore = await db.InvoiceNumberCounters.Where(c => c.InvoiceDate == clock.Today).Select(c => c.LastNumber).SingleAsync();
        await using (var faultDb = new DentalDbContext(failCompleteOptions))
        {
            var failed = false;
            try { await new InvoiceRepository(faultDb).CompleteCorrectionAsync(fault.Visit.VisitId, admin.UserId, RoleCodes.Admin, clock.Today, null); }
            catch (InvalidOperationException e) when (e.Message == "Synthetic correction audit failure") { failed = true; }
            check(failed, "Finalization required audit failure propagates");
        }
        check(await db.Visits.AsNoTracking().AnyAsync(v => v.VisitId == fault.Visit.VisitId && v.Status == VisitStatuses.Correcting && !v.IsLocked) &&
            !await db.Invoices.AnyAsync(i => i.VisitId == fault.Visit.VisitId && i.Status != InvoiceStatus.Cancelled) &&
            await db.InvoiceNumberCounters.Where(c => c.InvoiceDate == clock.Today).Select(c => c.LastNumber).SingleAsync() == counterBefore,
            "Finalization failure rolls back lock/new invoice/number counter");
        authenticate(RoleCodes.Admin, admin.UserId);
        using (var r = await client.PostAsync($"/api/visits/{fault.Visit.VisitId}/lock", null)) check(r.StatusCode == HttpStatusCode.NoContent, "Admin may finalize via lock alias after rollback204");

        async Task Race(bool tooth)
        {
            var fixture = await Completed(); await Open(fixture.Visit.VisitId);
            var barrier = new SaveBarrier(); var raceOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(barrier).Options;
            async Task<Result> Edit()
            {
                await using var context = new DentalDbContext(raceOptions); var visits = new VisitRepository(context);
                var audit = new AuditLogger(context, NullLogger<AuditLogger>.Instance);
                if (tooth)
                {
                    var result = await new ToothConditionService(visits, new PatientRepository(context), new ToothConditionRepository(context), new UnitOfWork(context), audit, new())
                        .AddAsync(fixture.Visit.VisitId, new(11, null, "CARIES"), dentist.UserId, RoleCodes.Dentist, null);
                    return result.IsFailure ? Result.Failure(result.Error) : Result.Success();
                }
                var diagnosis = await new DiagnosisService(new PatientRepository(context), visits, new UnitOfWork(context), audit)
                    .UpdateDiagnosisAsync(fixture.Visit.VisitId, new("Synthetic racing correction"), dentist.UserId, RoleCodes.Dentist, null);
                return diagnosis.IsFailure ? Result.Failure(diagnosis.Error) : Result.Success();
            }
            async Task<Result> Finish()
            {
                await using var context = new DentalDbContext(raceOptions);
                var result = await new InvoiceRepository(context).CompleteCorrectionAsync(fixture.Visit.VisitId, dentist.UserId, RoleCodes.Dentist, clock.Today, null);
                return result.IsFailure ? Result.Failure(result.Error) : Result.Success();
            }
            var results = await Task.WhenAll(Edit(), Finish());
            check(results.Count(r => r.IsSuccess) == 1 && results.Any(r => r.IsFailure), tooth ? "Tooth edit versus finalization has one winner" : "Diagnosis edit versus finalization has one winner");
            var final = await db.Visits.AsNoTracking().SingleAsync(v => v.VisitId == fixture.Visit.VisitId);
            check(final.Status == VisitStatuses.Correcting && !final.IsLocked || final.Status == VisitStatuses.Completed && final.IsLocked,
                "Concurrent correction remains consistently editable or completed");
            if (final.Status == VisitStatuses.Completed)
                check(tooth ? !await db.ToothConditions.AnyAsync(t => t.VisitId == final.VisitId) : final.Diagnosis == "Synthetic original", "No clinical edit commits after winning finalization");
            else
            {
                authenticate(RoleCodes.Admin, admin.UserId);
                using var r = await client.PutAsync($"/api/visits/{final.VisitId}/complete", null);
                check(r.StatusCode == HttpStatusCode.NoContent, "Fresh finalization after winning edit succeeds204");
            }
        }
        await Race(false); await Race(true);
        var duplicate = await Completed(); await Open(duplicate.Visit.VisitId);
        var duplicateBarrier = new SaveBarrier(); var duplicateOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(duplicateBarrier).Options;
        async Task<bool> FinalizeOnce()
        {
            await using var context = new DentalDbContext(duplicateOptions);
            return (await new InvoiceRepository(context).CompleteCorrectionAsync(duplicate.Visit.VisitId, dentist.UserId, RoleCodes.Dentist, clock.Today, null)).IsSuccess;
        }
        check((await Task.WhenAll(FinalizeOnce(), FinalizeOnce())).Count(success => success) == 1, "Concurrent finalization creates one result, one conflict");
        check(await db.Invoices.CountAsync(i => i.VisitId == duplicate.Visit.VisitId && i.Status == InvoiceStatus.PendingPayment) == 1 &&
            await db.AuditLogs.CountAsync(a => a.Action == "MOD_PAT_VISIT_CORRECTION_COMPLETED" && a.EntityId == duplicate.Visit.VisitId) == 1, "Concurrent finalization persists one invoice and required audit");
    }
    private sealed class FailAudit(string action) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (data.Context!.ChangeTracker.Entries<AuditLog>().Any(e => e.State == EntityState.Added && e.Entity.Action == action))
                throw new InvalidOperationException("Synthetic correction audit failure");
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
            {
                if (seen.Count == 2) ready.TrySetResult();
                await ready.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
            }
            return result;
        }
    }
}
