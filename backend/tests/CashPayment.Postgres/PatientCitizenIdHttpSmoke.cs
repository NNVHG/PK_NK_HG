using System.Net;
using System.Net.Http.Json;
using Dental.Application.Features.MOD_PAT;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

internal static class PatientCitizenIdHttpSmoke
{
 public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int cashierId, Action<string, int> authenticate, Action<bool, string> check)
 {
  await using var db = new DentalDbContext(options);
  async Task<User> Actor(string code)
  {
   var role = await db.Roles.SingleOrDefaultAsync(r => r.RoleCode == code);
   if (role is null) { role = new Role { RoleCode = code, RoleName = code }; db.Roles.Add(role); await db.SaveChangesAsync(); }
   var u = new User { FullName = "Synthetic ID actor", Phone = "ID" + Guid.NewGuid().ToString("N")[..14], RoleId = role.RoleId, PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()) };
   db.Users.Add(u); await db.SaveChangesAsync(); return u;
  }
  var owner = await Actor(RoleCodes.Patient); var other = await Actor(RoleCodes.Patient); var admin = await Actor(RoleCodes.Admin); var doctor = await Actor(RoleCodes.Dentist); var assistant = await Actor(RoleCodes.Assistant);
  var patient = new Patient { FullName = "Synthetic ID patient", Phone = "0990000661", DateOfBirth = new(1990, 1, 1), UserId = owner.UserId };
  var second = new Patient { FullName = "Synthetic ID second", Phone = "0990000662", DateOfBirth = new(1990, 1, 1) };
  db.Patients.AddRange(patient, second); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
  var path = $"/api/patients/{patient.PatientId}/citizen-id"; var body = new CitizenIdRequest("987654321098", 0);
  using (var r = await client.GetAsync(path)) check(r.StatusCode == HttpStatusCode.Unauthorized, "Anonymous cannot read CCCD status401");
  using (var r = await client.PutAsJsonAsync(path, body)) check(r.StatusCode == HttpStatusCode.Unauthorized, "Anonymous cannot register CCCD401");
  foreach (var a in new[] { (RoleCodes.Patient, owner.UserId), (RoleCodes.Assistant, assistant.UserId), (RoleCodes.Dentist, doctor.UserId) })
  { authenticate(a.Item1, a.Item2); using var r = await client.PutAsJsonAsync(path, body); check(r.StatusCode == HttpStatusCode.Forbidden, "Clinical/patient roles cannot change identity403"); }
  authenticate(RoleCodes.Receptionist, cashierId);
  using (var r = await client.GetAsync(path)) { var s = await r.Content.ReadFromJsonAsync<CitizenIdStatus>(); check(s!.Masked is null && s.Version == 0 && r.Headers.CacheControl?.NoStore == true, "Missing identity has honest null and private NoStore"); }
  foreach (var input in new[] { "", "123", "987654321098 ", "9876543210987", "１２３４５６７８９０１２" })
  { using var r = await client.PutAsJsonAsync(path, new CitizenIdRequest(input, 0)); check(r.StatusCode == HttpStatusCode.BadRequest, "Invalid identity400 without write"); }
  using (var r = await client.PutAsJsonAsync(path, body))
  { var text = await r.Content.ReadAsStringAsync(); var status = await r.Content.ReadFromJsonAsync<CitizenIdStatus>(); check(r.StatusCode == HttpStatusCode.OK && status!.Masked == "987******098" && status.Version == 1 && !text.Contains(body.CitizenId) && !text.Contains(CitizenIdIdentity.Hash(body.CitizenId)), "Cashier saves identity with masked-only response200"); }
  var stored = await db.Patients.AsNoTracking().SingleAsync(p => p.PatientId == patient.PatientId);
  check(stored.CitizenIdHash == CitizenIdIdentity.Hash(body.CitizenId) && stored.CitizenIdHash.Length == 64, "Only SHA256 and masked identity persisted");
  check(await db.AuditLogs.AnyAsync(a => a.EntityId == patient.PatientId && a.Action == "MOD_PAT_CITIZEN_ID_UPDATED") &&
   await db.AuditLogs.AllAsync(a => a.Detail == null || !a.Detail.Contains(body.CitizenId) && !a.Detail.Contains(stored.CitizenIdHash!)), "Atomic audit contains no identity or hash");
  using (var r = await client.PutAsJsonAsync(path, body)) check(r.StatusCode == HttpStatusCode.Conflict, "Lost response replay fails version guard409");
  using (var r = await client.PutAsJsonAsync($"/api/patients/{second.PatientId}/citizen-id", body)) check(r.StatusCode == HttpStatusCode.Conflict, "Duplicate identity across patients rejected409");
  authenticate(RoleCodes.Patient, owner.UserId); using (var r = await client.GetAsync(path)) check(r.StatusCode == HttpStatusCode.OK, "Patient reads own masked identity200");
  authenticate(RoleCodes.Patient, other.UserId); using (var r = await client.GetAsync(path)) check(r.StatusCode == HttpStatusCode.NotFound, "Foreign patient identity read IDOR404");
  var faultOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new FailIdentityAudit()).Options; var failed = false;
  await using (var fault = new DentalDbContext(faultOptions))
   try { await new PatientCitizenIdRepository(fault).SaveAsync(patient.PatientId, cashierId, RoleCodes.Receptionist, new("987654321099", 1), null, default); }
   catch (InvalidOperationException e) when (e.Message == "Synthetic identity audit fault") { failed = true; }
  check(failed && await db.Patients.AnyAsync(p => p.PatientId == patient.PatientId && p.CitizenIdVersion == 1 && p.CitizenIdHash == stored.CitizenIdHash), "Required audit failure rolls back identity and version");
  async Task<bool> Race(string citizenId)
  { await using var ctx = new DentalDbContext(options); return (await new PatientCitizenIdRepository(ctx).SaveAsync(patient.PatientId, admin.UserId, RoleCodes.Admin, new(citizenId, 1), null, default)).IsSuccess; }
  check((await Task.WhenAll(Race("987654321096"), Race("987654321097"))).Count(x => x) == 1, "Concurrent identity replacements have one winner");
  check(await db.Patients.AnyAsync(p => p.PatientId == patient.PatientId && p.CitizenIdVersion == 2) && await db.AuditLogs.CountAsync(a => a.Action == "MOD_PAT_CITIZEN_ID_UPDATED") == 2, "Only committed identity replacement has new version and audit");
  db.ChangeTracker.Clear(); var actor = await db.Users.FindAsync(cashierId); actor!.IsActive = false; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
  authenticate(RoleCodes.Receptionist, cashierId); using (var r = await client.PutAsJsonAsync(path, new CitizenIdRequest("987654321095", 2))) check(r.StatusCode == HttpStatusCode.Forbidden, "Disabled actor cannot mutate identity using stale JWT403");
 }
 private sealed class FailIdentityAudit : SaveChangesInterceptor
 {
  public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData e, InterceptionResult<int> result, CancellationToken ct = default)
  { if (e.Context!.ChangeTracker.Entries<AuditLog>().Any(x => x.State == EntityState.Added && x.Entity.Action == "MOD_PAT_CITIZEN_ID_UPDATED")) throw new InvalidOperationException("Synthetic identity audit fault"); return ValueTask.FromResult(result); }
 }
}
