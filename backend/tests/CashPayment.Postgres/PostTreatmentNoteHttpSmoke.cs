using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Dental.Application.Common;
using Dental.Application.Features.Visits.DTOs;
using Dental.Application.Features.Visits.Services;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Repositories;
using Dental.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

internal static class PostTreatmentNoteHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int actorId,
        Action<string, int> authenticate, Action<bool, string> check)
    {
        await using var db = new DentalDbContext(options);
        if (!db.Database.GetDbConnection().Database.StartsWith("dental_cash_test_", StringComparison.Ordinal)) throw new InvalidOperationException("Owned test DB required");
        async Task<User> Staff(string code)
        {
            var role = await db.Roles.SingleOrDefaultAsync(r => r.RoleCode == code);
            role ??= new() { RoleCode = code, RoleName = code };
            var user = new User { Role = role, FullName = "Synthetic addendum actor", IsActive = true,
                Phone = "AD" + Guid.NewGuid().ToString("N")[..14], PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()) };
            db.Users.Add(user); await db.SaveChangesAsync(); db.ChangeTracker.Clear(); return user;
        }
        var doctor = await Staff(RoleCodes.Dentist); var other = await Staff(RoleCodes.Dentist); var admin = await Staff(RoleCodes.Admin);
        var patientUser = await Staff(RoleCodes.Patient);
        async Task<Visit> Paid(User dentist)
        {
            var visit = new Visit { Patient = new() { UserId = patientUser.UserId, FullName = "Synthetic addendum patient", Phone = "0999999907", DateOfBirth = new(1990, 1, 1) },
                CreatedByUserId = actorId, DentistId = dentist.UserId, Status = VisitStatuses.Completed, IsLocked = true,
                StartedAt = DateTime.UtcNow.AddHours(-2), EndedAt = DateTime.UtcNow.AddHours(-1), Diagnosis = "Synthetic diagnosis", ClinicalNotes = "Synthetic original" };
            var invoice = new Invoice { Visit = visit, CreatedByUserId = actorId, Status = InvoiceStatus.Paid,
                InvoiceCode = "AD-" + Guid.NewGuid().ToString("N"), TotalAmount = 100000, PaidAmount = 100000 };
            db.Invoices.Add(invoice); await db.SaveChangesAsync(); db.ChangeTracker.Clear(); return await db.Visits.AsNoTracking().SingleAsync(v => v.VisitId == visit.VisitId);
        }
        var visit = await Paid(doctor); var another = await Paid(other); var key = Guid.NewGuid();
        string PathFor(Visit v) => $"/api/visits/{v.VisitId}/clinical-notes/addendum";
        async Task<HttpResponseMessage> Post(Visit v, Guid? requestId, string note = "Synthetic follow-up")
            => await client.PostAsJsonAsync(PathFor(v), new CreatePostTreatmentNoteRequest(note, requestId));
        client.DefaultRequestHeaders.Authorization = null;
        using (var response = await Post(visit, key)) check(response.StatusCode == HttpStatusCode.Unauthorized, "Anonymous addendum401");
        foreach (var role in new[] { RoleCodes.Assistant, RoleCodes.Receptionist, RoleCodes.Patient })
        { authenticate(role, actorId); using var response = await Post(visit, key); check(response.StatusCode == HttpStatusCode.Forbidden, "Nonclinical addendum writer403"); }
        authenticate(RoleCodes.Dentist, other.UserId);
        using (var response = await Post(visit, key)) check(response.StatusCode == HttpStatusCode.Forbidden, "Foreign dentist cannot append403");
        authenticate(RoleCodes.Dentist, doctor.UserId);
        foreach (var note in new[] { " ", new string('x', 1001) })
        { using var response = await Post(visit, key, note); check(response.StatusCode == HttpStatusCode.BadRequest, "Addendum content validation400"); }
        using (var response = await Post(visit, Guid.Empty)) check(response.StatusCode == HttpStatusCode.BadRequest, "Empty request key rejected400");
        PostTreatmentNoteResponse created;
        using (var response = await Post(visit, key))
        { check(response.StatusCode == HttpStatusCode.Created, "Assigned dentist appends after Paid201"); created = (await response.Content.ReadFromJsonAsync<PostTreatmentNoteResponse>())!; }
        check(created.VisitId == visit.VisitId && created.CreatedByUserId == doctor.UserId && created.Note == "Synthetic follow-up" && created.CreatedByFullName.Length > 0, "Real author timestamp and clinical text retained");
        using (var response = await Post(visit, key)) check((await response.Content.ReadFromJsonAsync<PostTreatmentNoteResponse>())?.Id == created.Id, "Identical retry returns same note identity");
        using (var response = await Post(visit, key, "Different")) check(response.StatusCode == HttpStatusCode.Conflict, "Same key with changed text409");
        authenticate(RoleCodes.Dentist, other.UserId);
        using (var response = await Post(visit, key)) check(response.StatusCode == HttpStatusCode.Forbidden, "Existing key never bypasses visit ownership403");
        using (var response = await Post(another, key)) check(response.StatusCode == HttpStatusCode.NotFound, "Existing key never returns another visit or author's note404");
        authenticate(RoleCodes.Admin, admin.UserId);
        using (var response = await Post(visit, key)) check(response.StatusCode == HttpStatusCode.NotFound, "Admin cannot replay another author's key404");
        using (var response = await Post(visit, Guid.NewGuid(), "Synthetic administrator follow-up")) check(response.StatusCode == HttpStatusCode.Created, "Admin appends under own identity201 DL265");
        authenticate(RoleCodes.Patient, patientUser.UserId);
        using (var response = await client.GetAsync(PathFor(visit))) check(response.StatusCode == HttpStatusCode.OK && response.Headers.CacheControl?.NoStore == true &&
            (await response.Content.ReadFromJsonAsync<PostTreatmentNoteResponse[]>())!.Length == 2, "Patient owner reads append-only history with NoStore");
        authenticate(RoleCodes.Patient, actorId);
        using (var response = await client.GetAsync(PathFor(visit))) check(response.StatusCode == HttpStatusCode.NotFound, "Other patient read IDOR404");
        var stored = await db.Visits.AsNoTracking().SingleAsync(v => v.VisitId == visit.VisitId);
        check(stored.Status == VisitStatuses.Completed && stored.IsLocked && stored.Diagnosis == visit.Diagnosis && stored.ClinicalNotes == visit.ClinicalNotes &&
            stored.StartedAt == visit.StartedAt && stored.EndedAt == visit.EndedAt, "Addendum never unlocks visit or replaces original clinical record");
        check(await db.Invoices.AnyAsync(i => i.VisitId == visit.VisitId && i.Status == InvoiceStatus.Paid && i.TotalAmount == 100000 && i.PaidAmount == 100000), "Paid financial totals and status remain unchanged");
        var audit = await db.AuditLogs.AsNoTracking().Where(a => a.Action == AuditActions.PostTreatmentNoteCreated && a.EntityId == visit.VisitId).ToListAsync();
        check(audit.Count == 2 && audit.All(a => !a.Detail!.Contains("follow-up")), "One required metadata audit per note, no clinical content");
        var noteEntity = await db.PostTreatmentNotes.SingleAsync(n => n.Id == created.Id); noteEntity.Note = "Overwrite";
        var rejected = false; try { await db.SaveChangesAsync(); } catch (InvalidOperationException) { rejected = true; } db.ChangeTracker.Clear();
        check(rejected && await db.PostTreatmentNotes.AnyAsync(n => n.Id == created.Id && n.Note == "Synthetic follow-up"), "Immutability guard rejects overwrite of persisted note");
        foreach (var invoiceStatus in new[] { InvoiceStatus.PendingPayment, InvoiceStatus.PartiallyPaid, InvoiceStatus.Cancelled })
        {
            var unpaid = await Paid(doctor); var invoice = await db.Invoices.SingleAsync(i => i.VisitId == unpaid.VisitId); invoice.Status = invoiceStatus; invoice.PaidAmount = 0;
            await db.SaveChangesAsync(); db.ChangeTracker.Clear(); authenticate(RoleCodes.Dentist, doctor.UserId);
            using var response = await Post(unpaid, Guid.NewGuid()); check(response.StatusCode == HttpStatusCode.Conflict && !await db.PostTreatmentNotes.AnyAsync(n => n.VisitId == unpaid.VisitId), "Non-Paid invoice blocks addendum409");
        }
        PostTreatmentNoteService Service(DentalDbContext context)
            => new(new VisitRepository(context), new PatientRepository(context), new InvoiceRepository(context), new PostTreatmentNoteRepository(context),
                new UnitOfWork(context), new AuditLogger(context, NullLogger<AuditLogger>.Instance));
        var fault = await Paid(doctor); var faultKey = Guid.NewGuid(); var faulted = false;
        await using (var context = new DentalDbContext(new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new FailAudit()).Options))
        { try { await Service(context).AddAsync(fault.VisitId, new("Synthetic fault note", faultKey), doctor.UserId, RoleCodes.Dentist, null); }
          catch (InvalidOperationException error) when (error.Message == "Synthetic addendum audit fault") { faulted = true; } }
        check(faulted && !await db.PostTreatmentNotes.AnyAsync(n => n.RequestId == faultKey) &&
            !await db.AuditLogs.AnyAsync(a => a.Action == AuditActions.PostTreatmentNoteCreated && a.EntityId == fault.VisitId), "Audit failure rolls back saved note and event together");
        var race = await Paid(doctor); var raceKey = Guid.NewGuid();
        var raceOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new SaveBarrier()).Options;
        async Task<bool> Append()
        { await using var context = new DentalDbContext(raceOptions); return (await Service(context).AddAsync(race.VisitId, new("Synthetic race note", raceKey), doctor.UserId, RoleCodes.Dentist, null)).IsSuccess; }
        check((await Task.WhenAll(Append(), Append())).Count(success => success) == 1, "Concurrent identical key yields one commit and controlled409");
        check(await db.PostTreatmentNotes.CountAsync(n => n.RequestId == raceKey) == 1 && await db.AuditLogs.CountAsync(a => a.Action == AuditActions.PostTreatmentNoteCreated && a.EntityId == race.VisitId) == 1, "Race persists one immutable note and one audit");
        authenticate(RoleCodes.Dentist, doctor.UserId);
        using (var response = await Post(race, raceKey, "Synthetic race note")) check(response.StatusCode == HttpStatusCode.Created, "Explicit retry after key conflict safely replays committed note");
    }
    private sealed class FailAudit : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (data.Context!.ChangeTracker.Entries<AuditLog>().Any(e => e.State == EntityState.Added && e.Entity.Action == AuditActions.PostTreatmentNoteCreated))
                throw new InvalidOperationException("Synthetic addendum audit fault");
            return ValueTask.FromResult(result);
        }
    }
    private sealed class SaveBarrier : SaveChangesInterceptor
    {
        private readonly ConcurrentDictionary<Guid, byte> seen = new();
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        { if (seen.TryAdd(data.Context!.ContextId.InstanceId, 0)) { if (seen.Count == 2) ready.TrySetResult(); await ready.Task.WaitAsync(TimeSpan.FromSeconds(15), ct); } return result; }
    }
}
