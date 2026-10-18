using System.Net;
using System.Net.Http.Json;
using Dental.Application.Features.MOD_AUTH;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
internal static class DoctorSignatureHttpSmoke
{
 public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, Action<string,int> authenticate, Action<bool,string> check)
 {
  await using var db = new DentalDbContext(options);
  async Task<User> Actor(string code)
  {
   var role = await db.Roles.SingleOrDefaultAsync(r=>r.RoleCode==code);
   if (role is null) { role=new Role { RoleCode=code,RoleName=code }; db.Roles.Add(role); await db.SaveChangesAsync(); }
   var actor=new User { FullName="Synthetic signature actor",Phone="SIG"+Guid.NewGuid().ToString("N")[..13],RoleId=role.RoleId,PasswordHash=BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()) };
   db.Users.Add(actor); await db.SaveChangesAsync(); return actor;
  }
  var admin=await Actor(RoleCodes.Admin); var doctor=await Actor(RoleCodes.Dentist); var foreign=await Actor(RoleCodes.Dentist);
  var otherRoles=new[] { await Actor(RoleCodes.Receptionist),await Actor(RoleCodes.Assistant),await Actor(RoleCodes.Patient) };
  var png=SignaturePngFixture.Png(); var encoded=Convert.ToBase64String(png); var path=$"/api/doctors/{doctor.UserId}/signature";
  client.DefaultRequestHeaders.Authorization=null;
  using(var r=await client.GetAsync(path)) check(r.StatusCode==HttpStatusCode.Unauthorized,"Anonymous signature read401");
  using(var r=await client.PostAsJsonAsync(path,new DoctorSignatureRequest(0,encoded))) check(r.StatusCode==HttpStatusCode.Unauthorized,"Anonymous signature save401");
  using(var r=await client.GetAsync(path+"/file?version=1")) check(r.StatusCode==HttpStatusCode.Unauthorized,"Anonymous image401");
  foreach(var actor in otherRoles)
  {
   var code=(await db.Roles.FindAsync(actor.RoleId))!.RoleCode; authenticate(code,actor.UserId);
   using(var r=await client.GetAsync(path)) check(r.StatusCode==HttpStatusCode.Forbidden,"Non-manager signature read403");
   using(var r=await client.PostAsJsonAsync(path,new DoctorSignatureRequest(0,encoded))) check(r.StatusCode==HttpStatusCode.Forbidden,"Non-manager signature save403");
   using(var r=await client.GetAsync(path+"/file?version=1")) check(r.StatusCode==HttpStatusCode.Forbidden,"Non-manager image403");
  }
  authenticate(RoleCodes.Dentist,foreign.UserId);
  using(var r=await client.GetAsync(path)) check(r.StatusCode==HttpStatusCode.NotFound,"Foreign dentist status404");
  using(var r=await client.PostAsJsonAsync(path,new DoctorSignatureRequest(0,encoded))) check(r.StatusCode==HttpStatusCode.NotFound,"Foreign dentist write404");
  using(var r=await client.GetAsync(path+"/file?version=1")) check(r.StatusCode==HttpStatusCode.NotFound,"Foreign dentist image404");
  authenticate(RoleCodes.Dentist,doctor.UserId);
  using(var r=await client.GetAsync(path)) { var s=await r.Content.ReadFromJsonAsync<DoctorSignatureStatus>(); check(r.StatusCode==HttpStatusCode.OK && s!.Version==0 && s.SignatureImageUrl is null && r.Headers.CacheControl?.NoStore==true,"Own missing signature honest null NoStore"); }
  using(var r=await client.GetAsync(path+"/file?version=1")) check(r.StatusCode==HttpStatusCode.NotFound,"Missing image404");
  foreach(var bad in new[] { "", "notbase64!", Convert.ToBase64String("<svg/>"u8.ToArray()), Convert.ToBase64String(png[..^1]),Convert.ToBase64String(new byte[SignaturePng.MaxBytes+1]) })
  { using var r=await client.PostAsJsonAsync(path,new DoctorSignatureRequest(0,bad)); check(r.StatusCode==HttpStatusCode.BadRequest,"Invalid image400"); }
  using(var r=await client.PostAsJsonAsync(path,new DoctorSignatureRequest(-1,encoded))) check(r.StatusCode==HttpStatusCode.BadRequest,"Negative expected version400");
  check(!await db.DoctorSignatures.AnyAsync(),"Rejected images produce no row");
  using(var r=await client.PostAsJsonAsync(path,new DoctorSignatureRequest(0,encoded))) { var s=await r.Content.ReadFromJsonAsync<DoctorSignatureStatus>(); check(r.StatusCode==HttpStatusCode.OK && s!.Version==1 && s.DoctorId==doctor.UserId && ! (await r.Content.ReadAsStringAsync()).Contains(encoded),"Owner uploads first image metadata only"); }
  using(var r=await client.GetAsync(path+"/file?version=1")) check(r.StatusCode==HttpStatusCode.OK && (await r.Content.ReadAsByteArrayAsync()).SequenceEqual(png) && r.Content.Headers.ContentType?.MediaType=="image/png" && r.Headers.CacheControl?.NoStore==true && r.Headers.GetValues("X-Content-Type-Options").Single()=="nosniff","Protected image exact bytes NoStore nosniff");
  using(var r=await client.PostAsJsonAsync(path,new DoctorSignatureRequest(0,encoded))) check(r.StatusCode==HttpStatusCode.Conflict,"Replay obsolete version409");
  authenticate(RoleCodes.Admin,admin.UserId);
  using(var r=await client.GetAsync(path)) check(r.StatusCode==HttpStatusCode.OK,"Admin can inspect doctor signature");
  var second=SignaturePngFixture.Png(width:3);
  using(var r=await client.PostAsJsonAsync(path,new DoctorSignatureRequest(1,Convert.ToBase64String(second)))) { var s=await r.Content.ReadFromJsonAsync<DoctorSignatureStatus>();check(r.StatusCode==HttpStatusCode.OK && s!.Version==2,"Admin replaces image by adding version2"); }
  using(var r=await client.GetAsync(path+"/file?version=1")) check((await r.Content.ReadAsByteArrayAsync()).SequenceEqual(png),"Earlier image immutable after replacement");
  using(var r=await client.GetAsync(path+"/file?version=2")) check((await r.Content.ReadAsByteArrayAsync()).SequenceEqual(second),"New image accessible by explicit version");
  using(var r=await client.GetAsync($"/api/doctors/{admin.UserId}/signature")) check(r.StatusCode==HttpStatusCode.NotFound,"Non-dentist target404");
  var audit=await db.AuditLogs.Where(x=>x.Action=="MOD_AUTH_DOCTOR_SIGNATURE_ADDED").ToListAsync();
  check(audit.Count==2 && audit.Select(a=>a.UserId).ToHashSet().SetEquals(new int?[] { admin.UserId,doctor.UserId }) && audit.All(a=>!a.Detail!.Contains(encoded)),"Both actors audited with metadata only");
  var faultOptions=new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new FailSignatureAudit()).Options; var failed=false;
  await using(var fault=new DentalDbContext(faultOptions))
   try { await new DoctorSignatureRepository(fault).SaveAsync(doctor.UserId,admin.UserId,RoleCodes.Admin,2,png,null,default); }
   catch(InvalidOperationException e) when(e.Message=="Synthetic signature audit fault") { failed=true; }
  check(failed && await db.DoctorSignatures.CountAsync()==2 && await db.AuditLogs.CountAsync(a=>a.Action=="MOD_AUTH_DOCTOR_SIGNATURE_ADDED")==2,"Audit fault rolls back image and version");
  async Task<bool> Race() { await using var ctx=new DentalDbContext(options); return (await new DoctorSignatureRepository(ctx).SaveAsync(doctor.UserId,doctor.UserId,RoleCodes.Dentist,2,png,null,default)).IsSuccess; }
  check((await Task.WhenAll(Race(),Race())).Count(x=>x)==1,"Concurrent image changes one winner");
  check(await db.DoctorSignatures.CountAsync()==3 && await db.AuditLogs.CountAsync(a=>a.Action=="MOD_AUTH_DOCTOR_SIGNATURE_ADDED")==3,"Only winning change writes version and audit");
  foreach(var delete in new[] { false,true })
  {
   await using var ctx=new DentalDbContext(options); var image=await ctx.DoctorSignatures.FirstAsync(); if(delete) ctx.Remove(image); else image.ImagePng=second;
   var blocked=false; try { await ctx.SaveChangesAsync(); } catch(InvalidOperationException) { blocked=true; } check(blocked,"Image version update/delete guarded");
  }
  db.ChangeTracker.Clear(); var inactive=await db.Users.FindAsync(doctor.UserId); inactive!.IsActive=false; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
  using(var r=await client.PostAsJsonAsync(path,new DoctorSignatureRequest(3,encoded))) check(r.StatusCode==HttpStatusCode.NotFound,"Inactive target rejected");
  inactive=await db.Users.FindAsync(doctor.UserId); inactive!.IsActive=true; var adminRow=await db.Users.FindAsync(admin.UserId);adminRow!.IsActive=false;await db.SaveChangesAsync();db.ChangeTracker.Clear();
  using(var r=await client.GetAsync(path)) check(r.StatusCode==HttpStatusCode.NotFound,"Disabled Admin stale JWT cannot read");
  using(var r=await client.PostAsJsonAsync(path,new DoctorSignatureRequest(3,encoded))) check(r.StatusCode==HttpStatusCode.NotFound,"Disabled Admin stale JWT cannot upload");
  adminRow=await db.Users.FindAsync(admin.UserId);adminRow!.IsActive=true;adminRow.RoleId=otherRoles[0].RoleId;await db.SaveChangesAsync();db.ChangeTracker.Clear();
  using(var r=await client.GetAsync(path+"/file?version=1")) check(r.StatusCode==HttpStatusCode.NotFound,"Changed persisted role cannot use old Admin token");
 }
 private sealed class FailSignatureAudit : SaveChangesInterceptor
 {
  public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default)
  { if(data.Context!.ChangeTracker.Entries<AuditLog>().Any(x=>x.Entity.Action=="MOD_AUTH_DOCTOR_SIGNATURE_ADDED")) throw new InvalidOperationException("Synthetic signature audit fault"); return ValueTask.FromResult(result); }
 }
}

