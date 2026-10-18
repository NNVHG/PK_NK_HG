using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dental.Application.Features.MOD_PAT;
using Dental.Application.Features.MOD_RX;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

internal static class PublicPrescriptionHttpSmoke
{
 public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int cashierId, Action<string, int> authenticate, Action<bool, string> check)
 {
  await using var db = new DentalDbContext(options);
  var role = await db.Roles.SingleOrDefaultAsync(r => r.RoleCode == RoleCodes.Dentist);
  if (role is null) { role = new() { RoleCode = RoleCodes.Dentist, RoleName = "Synthetic dentist" }; db.Roles.Add(role); await db.SaveChangesAsync(); }
  var doctor = new User { RoleId = role.RoleId, FullName = "Synthetic public doctor", Phone = "PUB" + Guid.NewGuid().ToString("N")[..13], PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()) };
  var p = new Patient { FullName = "Synthetic public patient", Phone = "0990000551", DateOfBirth = new(1990, 1, 1), CitizenIdHash = CitizenIdIdentity.Hash("987654321098"), CitizenIdMasked = "987******098", CitizenIdVersion = 1 };
  var noId = new Patient { FullName = "Synthetic no identity", Phone = "0990000552", DateOfBirth = new(1990, 1, 1) };
  db.AddRange(doctor, p, noId); await db.SaveChangesAsync();
  Prescription Make(int number, Patient owner, bool cancelled = false) => new() {
   PrescriptionCode = $"RX-20261017-{number:D4}", DoctorId = doctor.UserId, CreatedByUserId = doctor.UserId, CreatedAt = DateTime.UtcNow, IsCancelled = cancelled,
   Visit = new Visit { Patient = owner, CreatedByUserId = cashierId, Status = VisitStatuses.Completed, IsLocked = true },
   Diagnosis = "Synthetic private diagnosis", DoctorAdvice = "Synthetic private advice",
   Items = [new() { MedicineId = 0, MedicineCode = "SYN", MedicineName = "Synthetic private medicine", Unit = "Viên", UnitPrice = 100, Quantity = 2, UsageInstructions = "Synthetic private instructions", AllergyOverrideReason = "Must never appear in public DTO" }]
  };
  var drug = new Medicine { Code = "PUB-MED", Name = "Synthetic private medicine", Unit = "Viên", UnitPrice = 100, IsActive = true };
  db.Medicines.Add(drug); await db.SaveChangesAsync();
  var a = Make(11, p); var b = Make(12, p); var missing = Make(13, noId); var cancelled = Make(14, p, true);
  foreach (var rx in new[] { a, b, missing, cancelled }) rx.Items.Single().MedicineId = drug.Id;
  db.Prescriptions.AddRange(a, b, missing, cancelled); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
  string Path(Prescription rx) => "/api/public/prescriptions/" + rx.PrescriptionCode;
  using (var r = await client.GetAsync(Path(a)))
  {
   var raw = await r.Content.ReadAsStringAsync(); var obj = JsonDocument.Parse(raw).RootElement;
   check(r.StatusCode == HttpStatusCode.OK && obj.EnumerateObject().Count() == 2 && r.Headers.CacheControl?.NoStore == true, "Anonymous metadata returns only code and date with NoStore");
   check(!raw.Contains(p.FullName) && !raw.Contains("diagnosis") && !raw.Contains(p.CitizenIdHash!) && !raw.Contains(p.Phone), "Public metadata exposes no clinical or identity fields");
  }
  using (var r = await client.GetAsync(Path(cancelled))) check(r.StatusCode == HttpStatusCode.NotFound, "Cancelled prescription has no public metadata404");
  using (var r = await client.GetAsync(Path(a) + "/details")) check(r.StatusCode == HttpStatusCode.Forbidden, "Anonymous detail read needs restricted view token403");
  using (var r = await client.PostAsJsonAsync(Path(a) + "/verify", new PublicPrescriptionVerifyRequest("123"))) check(r.StatusCode == HttpStatusCode.BadRequest, "Invalid CCCD400");
  using (var r = await client.PostAsJsonAsync(Path(missing) + "/verify", new PublicPrescriptionVerifyRequest("987654321098"))) check(r.StatusCode == HttpStatusCode.Forbidden, "No stored CCCD cannot unlock via phone DOB or fallback403");
  for (var i = 0; i < 5; i++)
  { using var r = await client.PostAsJsonAsync(Path(a) + "/verify", new PublicPrescriptionVerifyRequest("987654321099")); check(r.StatusCode == (i == 4 ? HttpStatusCode.TooManyRequests : HttpStatusCode.Forbidden), "Fifth wrong identity locks prescription429"); }
  using (var r = await client.PostAsJsonAsync(Path(a) + "/verify", new PublicPrescriptionVerifyRequest("987654321098"))) check(r.StatusCode == HttpStatusCode.TooManyRequests, "Correct identity cannot bypass active15minute lock");
  PublicPrescriptionSession session;
  using (var r = await client.PostAsJsonAsync(Path(b) + "/verify", new PublicPrescriptionVerifyRequest("987654321098")))
  { session = (await r.Content.ReadFromJsonAsync<PublicPrescriptionSession>())!; check(r.StatusCode == HttpStatusCode.OK && session.ExpiresAt == SmokeTestTimeProvider.Instance.GetUtcNow().AddMinutes(15), "Different prescription matches CCCD without account login and issues15minute restricted token"); }
  client.DefaultRequestHeaders.Add("X-Prescription-View-Token", session.Token);
  using (var r = await client.GetAsync(Path(b) + "/details"))
  {
   var raw = await r.Content.ReadAsStringAsync();
   check(r.StatusCode == HttpStatusCode.OK && raw.Contains("Synthetic private diagnosis") && raw.Contains("Synthetic private instructions") && r.Headers.CacheControl?.NoStore == true, "Verified public details include actual persisted prescription with NoStore");
   check(!raw.Contains("987654321098") && !raw.Contains("citizenIdHash") && !raw.Contains("allergyOverrideReason") && !raw.Contains("unitPrice") && !raw.Contains(p.Phone), "Verified DTO still excludes identity hash phone allergy reason and financial internals");
  }
  using (var r = await client.GetAsync(Path(a) + "/details")) check(r.StatusCode == HttpStatusCode.Forbidden, "View token cannot read another prescription403");
  client.DefaultRequestHeaders.Remove("X-Prescription-View-Token"); client.DefaultRequestHeaders.Add("X-Prescription-View-Token", session.Token + "x");
  using (var r = await client.GetAsync(Path(b) + "/details")) check(r.StatusCode == HttpStatusCode.Forbidden, "Tampered public view token rejected403");
  client.DefaultRequestHeaders.Remove("X-Prescription-View-Token"); client.DefaultRequestHeaders.Add("X-Prescription-View-Token", session.Token);
  using (var r = await client.GetAsync("/api/patients")) check(r.StatusCode == HttpStatusCode.Unauthorized, "Public view token never authorizes internal API401");
  authenticate(RoleCodes.Receptionist, cashierId);
  using (var r = await client.PutAsJsonAsync($"/api/patients/{p.PatientId}/citizen-id", new CitizenIdRequest("987654321097", 1))) check(r.StatusCode == HttpStatusCode.OK, "Authorized clinic identity update200");
  client.DefaultRequestHeaders.Authorization = null;
  using (var r = await client.GetAsync(Path(b) + "/details")) check(r.StatusCode == HttpStatusCode.Forbidden, "Changing CCCD revokes previously issued public view token403");
  using (var r = await client.PostAsJsonAsync(Path(b) + "/verify", new PublicPrescriptionVerifyRequest("987654321098"))) check(r.StatusCode == HttpStatusCode.Forbidden, "Previous CCCD no longer matches403");
  using (var r = await client.PostAsJsonAsync(Path(b) + "/verify", new PublicPrescriptionVerifyRequest("987654321097")))
  { session = (await r.Content.ReadFromJsonAsync<PublicPrescriptionSession>())!; check(r.StatusCode == HttpStatusCode.OK, "Current CCCD unlocks a fresh session200"); }
  client.DefaultRequestHeaders.Remove("X-Prescription-View-Token"); client.DefaultRequestHeaders.Add("X-Prescription-View-Token", session.Token);
  var tracked = await db.Prescriptions.FindAsync(b.Id); tracked!.IsCancelled = true; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
  using (var r = await client.GetAsync(Path(b) + "/details")) check(r.StatusCode == HttpStatusCode.Forbidden, "Cancellation revokes detail access even with valid view token403");
  var throttled = false; var privateThrottle = true;
  for (var i = 0; i < 31; i++) { using var r = await client.GetAsync(Path(a)); if (r.StatusCode == HttpStatusCode.TooManyRequests) { throttled = true; privateThrottle &= r.Headers.CacheControl?.NoStore == true; } }
  check(throttled, "Real ASP.NET IP limiter blocks excessive anonymous lookup429");
  check(privateThrottle, "Even middleware IP rejection responses have NoStore");
 }
}
