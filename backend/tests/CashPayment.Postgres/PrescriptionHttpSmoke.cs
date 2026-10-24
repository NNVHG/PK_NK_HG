using System.Net;
using System.Net.Http.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_RX.DTOs;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

internal static class PrescriptionHttpSmoke
{
 public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int cashierId, Action<string, int> authenticate, Action<bool, string> check)
 {
  await using var db = new DentalDbContext(options); var clock = new VietnamClock(SmokeTestTimeProvider.Instance);
  async Task<User> Actor(string code)
  {
   var role = await db.Roles.SingleOrDefaultAsync(r => r.RoleCode == code);
   if (role is null) { role = new() { RoleCode = code, RoleName = code }; db.Roles.Add(role); await db.SaveChangesAsync(); }
   var user = new User { FullName = "Synthetic Rx actor", Phone = "RX" + Guid.NewGuid().ToString("N")[..14], RoleId = role.RoleId, PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()) };
   db.Users.Add(user); await db.SaveChangesAsync(); db.ChangeTracker.Clear(); return user;
  }
  var doctor = await Actor(RoleCodes.Dentist); var other = await Actor(RoleCodes.Dentist); var admin = await Actor(RoleCodes.Admin); var owner = await Actor(RoleCodes.Patient); var stranger = await Actor(RoleCodes.Patient); var assistant = await Actor(RoleCodes.Assistant);
  await TestChairFixtures.EnsureAsync(db, doctor.UserId, cashierId);
  var m = new Medicine { Code = "RXA", Name = "Synthetic medicine", ActiveIngredient = "Alpha", Strength = "Test formulation", Unit = "Viên", UnitPrice = 500, IsActive = true, CreatedAt = clock.UtcNow, SearchText = "rxa synthetic medicine alpha" };
  var duplicate = new Medicine { Code = "RXB", Name = "Synthetic compound", ActiveIngredient = "Alpha / Beta", Unit = "Viên", UnitPrice = 200, IsActive = true, CreatedAt = clock.UtcNow };
  db.Medicines.AddRange(m, duplicate, new Medicine { Code = "RX-OFF", Name = "Inactive", UnitPrice = 0, IsActive = false });
  var catalog = new DentalService { Code = "RX-SERVICE", Name = "Synthetic treatment", DurationMinutes = 15, Prices = [new() { Amount = 10000, EffectiveFrom = clock.UtcNow.AddDays(-1), CreatedByUserId = cashierId }] };
  db.DentalServices.Add(catalog); await db.SaveChangesAsync(); var priceId = catalog.Prices.Single().ServicePriceId; db.ChangeTracker.Clear();
  async Task<QueueEntry> Started(Patient? patient = null)
  {
   patient ??= new Patient { FullName = "Synthetic Rx patient", Phone = "0990000771", DateOfBirth = new(1990, 1, 1), UserId = owner.UserId };
   if (patient.PatientId > 0 && db.Entry(patient).State == EntityState.Detached) db.Patients.Attach(patient);
   var row = new QueueEntry { Patient = patient, DentistId = doctor.UserId, QueueDate = clock.Today, QueueNumber = (await db.QueueEntries.MaxAsync(q => (int?)q.QueueNumber) ?? 0) + 1,
    CheckInTime = clock.UtcNow, Status = QueueStatus.Waiting, Visit = new Visit { Patient = patient, CreatedByUserId = cashierId, Status = VisitStatuses.Created, Diagnosis = "Original visit diagnosis" } };
   db.QueueEntries.Add(row); await db.SaveChangesAsync(); db.ChangeTracker.Clear(); authenticate(RoleCodes.Dentist, doctor.UserId);
   using var start = await client.PutAsJsonAsync($"/api/queue/{row.QueueEntryId}/status", new { newStatus = 2 });
   if (!start.IsSuccessStatusCode) throw new Exception("Cannot start Rx fixture: " + await start.Content.ReadAsStringAsync());
   db.VisitServices.Add(new() { VisitId = row.VisitId!.Value, ServiceId = catalog.DentalServiceId, ServicePriceId = priceId, ServiceCode = catalog.Code, ServiceName = catalog.Name, Quantity = 1, UnitPrice = 10000, CreatedByUserId = doctor.UserId, CreatedAt = clock.UtcNow });
   await db.SaveChangesAsync(); db.ChangeTracker.Clear(); return row;
  }
  var row = await Started(); var visitId = row.VisitId!.Value; var path = $"/api/visits/{visitId}/prescription";
  db.MedicalHistoryRecords.Add(new() { PatientId = row.PatientId, VisitId = visitId, RecordedByUserId = doctor.UserId, Items = [new() { Type = MedicalHistoryTypes.Allergy, Name = "ALPHA", Detail = "Synthetic reaction", IsCritical = true }] });
  await db.SaveChangesAsync(); db.ChangeTracker.Clear();
  var request = new PrescriptionWriteRequest(0, "Prescription-specific diagnosis", [new(m.Id, 1, 2, "Synthetic clinician instructions")], "Synthetic advice");
  async Task<PrescriptionPreview> Preview(string endpoint, PrescriptionWriteRequest body)
  {
   using var response = await client.PostAsJsonAsync(endpoint + "/preview", body);
   if (!response.IsSuccessStatusCode) throw new Exception("Preview failed: " + response.StatusCode + " " + await response.Content.ReadAsStringAsync());
   check(response.Headers.CacheControl?.NoStore == true, "Preview privateNoStore"); return (await response.Content.ReadFromJsonAsync<PrescriptionPreview>())!;
  }
  async Task<PrescriptionWriteRequest> Confirm(string endpoint, PrescriptionWriteRequest body)
  {
   var p = await Preview(endpoint, body);
   return body with { PreviewToken = p.Token, DuplicateIngredientConfirmed = true, Items = body.Items.Select(l => l with { AllergyConfirmed = true, AllergyOverrideReason = "Synthetic clinician override reason" }).ToArray() };
  }
  client.DefaultRequestHeaders.Authorization = null;
  using (var r = await client.GetAsync(path)) check(r.StatusCode == HttpStatusCode.Unauthorized, "Anonymous Rx401");
  using (var r = await client.GetAsync(path + "/lookup-link")) check(r.StatusCode == HttpStatusCode.Unauthorized, "Anonymous cannot request clinic lookup link401");
  foreach (var actor in new[] { (RoleCodes.Admin, admin.UserId), (RoleCodes.Patient, owner.UserId), (RoleCodes.Assistant, assistant.UserId), (RoleCodes.Receptionist, cashierId) })
  { authenticate(actor.Item1, actor.Item2); using var r = await client.PostAsJsonAsync(path, request); check(r.StatusCode == HttpStatusCode.Forbidden, "Supporting role cannot sign/create Rx403"); }
  authenticate(RoleCodes.Dentist, other.UserId);
  using (var r = await client.GetAsync(path)) check(r.StatusCode == HttpStatusCode.NotFound, "Foreign dentist cannot read visit Rx404");
  using (var r = await client.GetAsync(path + "/lookup-link")) check(r.StatusCode == HttpStatusCode.NotFound, "Foreign dentist cannot request lookup link404");
  using (var r = await client.PostAsJsonAsync(path + "/preview", request)) check(r.StatusCode == HttpStatusCode.NotFound, "Foreign dentist cannot preview404");
  authenticate(RoleCodes.Dentist, doctor.UserId);
  using (var r = await client.GetAsync(path + "/lookup-link")) check(r.StatusCode == HttpStatusCode.NotFound, "Visit without saved prescription has no lookup link404");
  using (var r = await client.GetAsync(path + "/medicines")) { var list = (await r.Content.ReadFromJsonAsync<PagedResult<PrescriptionMedicine>>())!; check(list.TotalCount == 2 && list.Items.All(x => x.UnitPrice > 0), "Owned doctor selects only active priced medicines"); }
  using (var r = await client.GetAsync(path + "/medicines?includeInactive=true")) check(r.StatusCode == HttpStatusCode.BadRequest, "Clinical picker cannot request inactive medicines400");
  using (var r = await client.PostAsJsonAsync(path, request)) check(r.StatusCode == HttpStatusCode.BadRequest, "Save requires prior preview400");
  foreach (var bad in new[] { request with { Items = [] }, request with { Items = [request.Items[0], request.Items[0]] }, request with { Items = [request.Items[0] with { Quantity = 101 }] }, request with { Items = [request.Items[0] with { UsageInstructions = " " }] }, request with { Diagnosis = new string('a', 501) } })
  { using var r = await client.PostAsJsonAsync(path + "/preview", bad); check(r.StatusCode == HttpStatusCode.BadRequest, "Invalid Rx input400"); }
  var preview = await Preview(path, request); check(preview.AllergyWarnings.Count == 1 && preview.MedicineTotal == 1000 && preview.ServicesTotal == 10000 && preview.FinalAmount == 11000, "Preview combines invoice and allergy snapshots");
  using (var r = await client.PostAsJsonAsync(path, request with { PreviewToken = preview.Token })) check(r.StatusCode == HttpStatusCode.Conflict, "Allergy override requires explicit confirmation/reason409");
  var dupRequest = request with { Items = [request.Items[0], new(duplicate.Id, 1, 1, "Synthetic instructions")] };
  var dupPreview = await Preview(path, dupRequest); check(dupPreview.DuplicateIngredients.Count == 1, "Compound ingredient duplicate detected");
  using (var r = await client.PostAsJsonAsync(path, dupRequest with { PreviewToken = dupPreview.Token, Items = dupRequest.Items.Select(l => l with { AllergyConfirmed = true, AllergyOverrideReason = "Synthetic reason" }).ToArray() })) check(r.StatusCode == HttpStatusCode.Conflict, "Duplicate ingredient requires its own confirmation409");
  var confirmed = await Confirm(path, request); PrescriptionResponse saved;
  using (var r = await client.PostAsJsonAsync(path, confirmed)) { check(r.StatusCode == HttpStatusCode.Created, "Owned doctor creates Rx201"); saved = (await r.Content.ReadFromJsonAsync<PrescriptionResponse>())!; }
  check(saved.PrescriptionCode.StartsWith($"RX-{clock.Today:yyyyMMdd}-") && saved.Version == 1 && saved.InvoiceId > 0 && saved.Items.Single().UnitPrice == 500, "Daily code, price snapshot and shared invoice link persisted");
  using (var r = await client.GetAsync(path + "/lookup-link")) { var link = (await r.Content.ReadFromJsonAsync<PrescriptionLookupLink>())!; check(r.StatusCode == HttpStatusCode.OK && r.Headers.CacheControl?.NoStore == true && link.PrescriptionCode == saved.PrescriptionCode && link.Version == 1 && link.RelativePath == "/portal/prescriptions/" + saved.PrescriptionCode, "Lookup link uses actual saved code/version, relative route and NoStore"); }
  var invoice = await db.Invoices.AsNoTracking().Include(i => i.Items).SingleAsync(i => i.Id == saved.InvoiceId);
  check(invoice.Status == InvoiceStatus.Draft && invoice.TotalAmount == 11000 && invoice.Items.Count == 2 && invoice.Items.Single(i => i.ItemType == "Medicine").SourcePrescriptionItemId == saved.Items.Single().Id, "Shared Draft contains treatment and linked medicine item snapshots");
  using (var r = await client.PostAsJsonAsync(path, confirmed)) check(r.StatusCode == HttpStatusCode.Conflict, "Lost create response replay cannot create second Rx409");
  check(await db.Prescriptions.CountAsync(p => p.VisitId == visitId) == 1 && await db.PrescriptionNumberCounters.Where(c => c.PrescriptionDate == clock.Today).Select(c => c.LastNumber).SingleAsync() == 1, "Unique Visit and counter unaffected by replay");
  db.ChangeTracker.Clear(); var changedMedicine = await db.Medicines.FindAsync(m.Id); changedMedicine!.UnitPrice = 900; changedMedicine.Version++; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
  var edit = request with { ExpectedVersion = 1, Items = [request.Items[0] with { Quantity = 3 }] };
  using (var r = await client.PostAsJsonAsync(path + "/preview", edit)) check(r.StatusCode == HttpStatusCode.Conflict, "Changed medicine version forces review409");
  edit = edit with { Items = [edit.Items[0] with { MedicineVersion = 2 }] }; var editPreview = await Preview(path, edit); check(editPreview.MedicineTotal == 1500, "Existing prescription price fixed despite master price900");
  using (var r = await client.PutAsJsonAsync(path, await Confirm(path, edit))) check(r.StatusCode == HttpStatusCode.OK, "Existing order update200");
  check(await db.PrescriptionItems.AnyAsync(i => i.Id == saved.Items.Single().Id && i.Quantity == 3 && i.UnitPrice == 500), "Editing retains item identity and fixed price");
  var faultOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new FailAudit()).Options;
  var editAgain = edit with { ExpectedVersion = 2, Items = [edit.Items[0] with { Quantity = 4 }] }; var faulted = false;
  await using (var fault = new DentalDbContext(faultOptions))
  {
   var repo = new PrescriptionRepository(fault, new InvoiceRepository(fault), clock); var p = (await repo.PreviewAsync(visitId, doctor.UserId, editAgain, default)).Value!;
   try { await repo.SaveAsync(visitId, doctor.UserId, false, editAgain with { PreviewToken = p.Token, Items = [editAgain.Items[0] with { AllergyConfirmed = true, AllergyOverrideReason = "Synthetic reason" }] }, null, default); }
   catch (InvalidOperationException e) when (e.Message == "Synthetic Rx audit fault") { faulted = true; }
  }
  check(faulted && await db.Prescriptions.AnyAsync(p => p.Id == saved.Id && p.Version == 2) && await db.Invoices.AnyAsync(i => i.Id == saved.InvoiceId && i.TotalAmount == 11500), "Audit failure rolls back Rx, items and invoice atomically");
  using (var r = await client.PutAsync($"/api/visits/{visitId}/complete", null)) check(r.StatusCode == HttpStatusCode.OK, "Clinical completion retains Rx in finalized invoice");
  var physical = await db.Visits.AsNoTracking().SingleAsync(v => v.VisitId == visitId);
  db.ChangeTracker.Clear(); var discounted = await db.Invoices.FindAsync(saved.InvoiceId); discounted!.DiscountAmount = 11000; discounted.DiscountType = "FixedAmount"; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
  using (var r = await client.PostAsJsonAsync(path + "/preview", editAgain with { Items = [editAgain.Items[0] with { Quantity = 1 }] })) check(r.StatusCode == HttpStatusCode.Conflict, "Discount above reduced total blocks change without silently changing discount409");
  discounted = await db.Invoices.FindAsync(saved.InvoiceId); discounted!.DiscountAmount = 1000; discounted.DiscountType = "Percentage"; discounted.DiscountValue = 10; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
  using (var r = await client.PutAsJsonAsync(path, await Confirm(path, editAgain))) check(r.StatusCode == HttpStatusCode.OK, "Doctor updates existing Rx in Completed/PendingPayment without Admin unlock");
  var physicalAfter = await db.Visits.AsNoTracking().SingleAsync(v => v.VisitId == visitId); var bill = await db.Invoices.AsNoTracking().SingleAsync(i => i.Id == saved.InvoiceId);
  check(physicalAfter.Status == VisitStatuses.Completed && physicalAfter.IsLocked && physicalAfter.StartedAt == physical.StartedAt && physicalAfter.EndedAt == physical.EndedAt && bill.TotalAmount == 12000 && bill.DiscountAmount == 1000, "Pending edit changes medicine money only, not physical visit or service-only percentage discount");
  foreach (var actor in new[] { (RoleCodes.Admin, admin.UserId), (RoleCodes.Receptionist, cashierId), (RoleCodes.Assistant, assistant.UserId), (RoleCodes.Patient, owner.UserId) })
  { authenticate(actor.Item1, actor.Item2); using var r = await client.GetAsync(path); var data = await r.Content.ReadFromJsonAsync<PrescriptionContext>(); check(r.StatusCode == HttpStatusCode.OK && !data!.CanEdit && data.Allergies.Count == 0, "Support/owner reads without prescriber controls or allergy lookup"); using var link = await client.GetAsync(path + "/lookup-link"); check(link.StatusCode == HttpStatusCode.OK, "Read-authorized staff/owner can obtain lookup link without write permission"); }
  authenticate(RoleCodes.Patient, stranger.UserId); using (var r = await client.GetAsync(path)) check(r.StatusCode == HttpStatusCode.NotFound, "Foreign patient IDOR404");
  using (var r = await client.GetAsync(path + "/lookup-link")) check(r.StatusCode == HttpStatusCode.NotFound, "Foreign patient cannot request clinic lookup link404");
  authenticate(RoleCodes.Receptionist, cashierId); using (var r = await client.PostAsJsonAsync($"/api/invoices/{saved.InvoiceId}/payments", new CashPaymentRequest(1000, 1000, Guid.NewGuid()))) check(r.StatusCode == HttpStatusCode.OK, "Cashier receives partial payment on combined invoice200");
  authenticate(RoleCodes.Dentist, doctor.UserId); using (var r = await client.PostAsJsonAsync(path + "/preview", editAgain with { ExpectedVersion = 3 })) check(r.StatusCode == HttpStatusCode.Conflict, "Any payment blocks further Rx edits409");
  var guarded = false; await using (var guard = new DentalDbContext(options)) { var item = await guard.PrescriptionItems.SingleAsync(i => i.PrescriptionId == saved.Id); item.UsageInstructions = "Invalid direct mutation"; try { await guard.SaveChangesAsync(); } catch (InvalidOperationException) { guarded = true; } }
  check(guarded, "DbContext independently rejects paid-history prescription item mutation");
  authenticate(RoleCodes.Receptionist, cashierId); using (var r = await client.PostAsJsonAsync($"/api/invoices/{saved.InvoiceId}/payments", new CashPaymentRequest(10000, 10000, Guid.NewGuid()))) check(r.StatusCode == HttpStatusCode.OK, "Remaining payment completes combined invoice200");
  var patient = await db.Patients.SingleAsync(p => p.PatientId == row.PatientId); var next = await Started(patient); var nextId = next.VisitId!.Value; var nextPath = $"/api/visits/{nextId}/prescription";
  using (var r = await client.GetAsync($"/api/prescriptions/patient/{row.PatientId}/latest?targetVisitId={nextId}")) { var data = (await r.Content.ReadFromJsonAsync<RepeatPrescription>())!; check(data.SourcePrescriptionId == saved.Id && data.Items.Single().CurrentMedicine!.UnitPrice == 900, "Repeat reads prior real order but refreshes current master price"); }
  var newRequest = request with { Items = [new(m.Id, 2, 4, "Synthetic repeat instructions")] }; var oldPreview = await Preview(nextPath, newRequest);
  db.MedicalHistoryRecords.Add(new() { PatientId = row.PatientId, VisitId = nextId, RecordedByUserId = doctor.UserId, Items = [new() { Type = MedicalHistoryTypes.Allergy, Name = "Alpha", IsCritical = true }] }); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
  using (var r = await client.PostAsJsonAsync(nextPath, newRequest with { PreviewToken = oldPreview.Token, Items = [newRequest.Items[0] with { AllergyConfirmed = true, AllergyOverrideReason = "Synthetic reason" }] })) check(r.StatusCode == HttpStatusCode.Conflict, "New allergy snapshot invalidates old preview409");
  var race = await Confirm(nextPath, newRequest);
  async Task<bool> Race() { await using var ctx = new DentalDbContext(options); return (await new PrescriptionRepository(ctx, new InvoiceRepository(ctx), clock).SaveAsync(nextId, doctor.UserId, true, race, null, default)).IsSuccess; }
  check((await Task.WhenAll(Race(), Race())).Count(ok => ok) == 1 && await db.Prescriptions.CountAsync(p => p.VisitId == nextId) == 1, "Concurrent create persists one order and one daily number");
  var nextRx = await db.Prescriptions.AsNoTracking().SingleAsync(p => p.VisitId == nextId); check(await db.Invoices.AnyAsync(i => i.Id == nextRx.InvoiceId && i.TotalAmount == 13600), "Repeated order uses current price and shared new invoice");
  using (var r = await client.PutAsJsonAsync($"/api/queue/{next.QueueEntryId}/status", new { newStatus = 4, reason = "Synthetic clinical cancellation" })) check(r.StatusCode == HttpStatusCode.OK, "Clinical cancellation cancels Rx and Draft together");
  check(await db.Prescriptions.AnyAsync(p => p.Id == nextRx.Id && p.IsCancelled) && await db.Invoices.AnyAsync(i => i.Id == nextRx.InvoiceId && i.Status == InvoiceStatus.Cancelled), "Cancellation retains financial snapshots and canceled Rx");
  using (var r = await client.GetAsync(nextPath + "/lookup-link")) check(r.StatusCode == HttpStatusCode.Conflict, "Cancelled prescription cannot issue lookup QR409");
  var third = await Started(patient); var thirdId = third.VisitId!.Value; var thirdPath = $"/api/visits/{thirdId}/prescription";
  var thirdRequest = request with { Items = [new(m.Id, 2, 2, "Synthetic third instructions")] };
  using (var r = await client.PostAsJsonAsync(thirdPath, await Confirm(thirdPath, thirdRequest))) check(r.StatusCode == HttpStatusCode.Created, "New independent order created for financial race");
  using (var r = await client.PutAsync($"/api/visits/{thirdId}/complete", null)) check(r.StatusCode == HttpStatusCode.OK, "Financial race starts from completed unpaid visit");
  var thirdRx = await db.Prescriptions.AsNoTracking().SingleAsync(p => p.VisitId == thirdId);
  var reduceRequest = await Confirm(thirdPath, thirdRequest with { ExpectedVersion = 1, Items = [thirdRequest.Items[0] with { Quantity = 1 }] });
  async Task<bool> Reduce()
  {
   await using var ctx = new DentalDbContext(options);
   return (await new PrescriptionRepository(ctx, new InvoiceRepository(ctx), clock).SaveAsync(thirdId, doctor.UserId, false, reduceRequest, null, default)).IsSuccess;
  }
  async Task<bool> Pay()
  {
   await using var ctx = new DentalDbContext(options);
   return (await new CashPaymentRepository(ctx).ReceiveAsync(thirdRx.InvoiceId!.Value, cashierId, new CashPaymentRequest(11800, 11800, Guid.NewGuid()), null, default)).IsSuccess;
  }
  var financialRace = await Task.WhenAll(Reduce(), Pay());
  check(financialRace.Count(ok => ok) == 1, "Concurrent cash versus reduced prescription has one winner without server error");
  var raceBill = await db.Invoices.AsNoTracking().SingleAsync(i => i.Id == thirdRx.InvoiceId);
  var raceRx = await db.Prescriptions.AsNoTracking().Include(p => p.Items).SingleAsync(p => p.Id == thirdRx.Id);
  check(raceBill.PaidAmount <= raceBill.FinalAmount && raceBill.TotalAmount == 10000 + raceRx.Items.Sum(i => i.UnitPrice * i.Quantity) &&
   (financialRace[0] ? raceBill.PaidAmount == 0 && raceRx.Version == 2 : raceBill.Status == InvoiceStatus.Paid && raceRx.Version == 1),
   "Cash/Rx race cannot overpay, rewrite paid medicine content or detach combined invoice totals");
  check(await db.AuditLogs.Where(a => a.EntityType == "Prescription").AllAsync(a => !a.Detail!.Contains("Synthetic clinician") && !a.Detail.Contains("Prescription-specific") && !a.Detail.Contains("Synthetic reaction")), "Rx audit contains metadata only, never clinical content");
 }
 private sealed class FailAudit : SaveChangesInterceptor
 {
  public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
  { if (data.Context!.ChangeTracker.Entries<AuditLog>().Any(e => e.State == EntityState.Added && e.Entity.Action.StartsWith("MOD_RX_"))) throw new InvalidOperationException("Synthetic Rx audit fault"); return ValueTask.FromResult(result); }
 }
}
