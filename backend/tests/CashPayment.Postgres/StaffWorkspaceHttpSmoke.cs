using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_MST.DTOs;
using Dental.Application.Features.MOD_MST.Services;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Repositories;
using Dental.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

internal static class StaffWorkspaceHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int baseActorId,
        Action<string,int> authenticate, Action<bool,string> check)
    {
        await using var db = new DentalDbContext(options);
        if (!db.Database.GetDbConnection().Database.StartsWith("dental_cash_test_",StringComparison.Ordinal)) throw new InvalidOperationException("Owned test database required");
        async Task<User> Staff(string code)
        {
            var role=await db.Roles.SingleOrDefaultAsync(r=>r.RoleCode==code);
            if(role is null) { role=new Role {RoleCode=code,RoleName="Workspace "+code}; db.Roles.Add(role); await db.SaveChangesAsync(); }
            var user=new User {RoleId=role.RoleId,FullName="Synthetic workspace staff",IsActive=true,Phone="WS"+Guid.NewGuid().ToString("N")[..14],PasswordHash=BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString())};
            db.Users.Add(user); await db.SaveChangesAsync(); db.ChangeTracker.Clear(); return user;
        }
        async Task<DentalChair> Chair(string room="Phòng 3",DentalChairStatus status=DentalChairStatus.Available)
        {
            var chair=new DentalChair {ChairNumber="WS-"+Guid.NewGuid().ToString("N"),RoomName=room,Status=status,IsActive=status!=DentalChairStatus.Inactive,CreatedByUserId=baseActorId};
            db.DentalChairs.Add(chair); await db.SaveChangesAsync(); db.ChangeTracker.Clear(); return chair;
        }
        StaffWorkspaceService Service(DentalDbContext context)=>new(new StaffWorkspaceRepository(context),new DentalChairRepository(context),new UserRepository(context),
            new VisitRepository(context),new QueueRepository(context),new UnitOfWork(context),new AuditLogger(context,NullLogger<AuditLogger>.Instance),new VietnamClock(TimeProvider.System));
        DentalChairService Catalog(DentalDbContext context)=>new(new DentalChairRepository(context),new UnitOfWork(context),new AuditLogger(context,NullLogger<AuditLogger>.Instance),new VietnamClock(TimeProvider.System));
        const string endpoint="/api/workspaces";
        var admin=await Staff(RoleCodes.Admin); var doctor=await Staff(RoleCodes.Dentist); var other=await Staff(RoleCodes.Dentist);
        var assistant=await Staff(RoleCodes.Assistant); var assistant2=await Staff(RoleCodes.Assistant);
        var chair=await Chair(); var second=await Chair("Phòng 4"); var maintenance=await Chair("Bảo trì",DentalChairStatus.Maintenance);
        client.DefaultRequestHeaders.Authorization=null;
        using(var r=await client.GetAsync(endpoint+"/me")) check(r.StatusCode==HttpStatusCode.Unauthorized,"Workspace me anonymous401");
        using(var r=await client.GetAsync(endpoint+"/options")) check(r.StatusCode==HttpStatusCode.Unauthorized,"Workspace options anonymous401");
        using(var r=await client.PutAsJsonAsync(endpoint+"/"+doctor.UserId,new ChangeWorkspaceRequest(chair.Id,null,0,true))) check(r.StatusCode==HttpStatusCode.Unauthorized,"Workspace write anonymous401");
        foreach(var role in new[]{RoleCodes.Receptionist,RoleCodes.Patient})
        {
            var actor=await Staff(role); authenticate(role,actor.UserId);
            using(var r=await client.GetAsync(endpoint+"/me")) check(r.StatusCode==HttpStatusCode.Forbidden,$"Workspace read {role}403");
            using(var r=await client.GetAsync(endpoint+"/options")) check(r.StatusCode==HttpStatusCode.Forbidden,$"Workspace options {role}403");
            using(var r=await client.PutAsJsonAsync(endpoint+"/"+actor.UserId,new ChangeWorkspaceRequest(null,null,0,true))) check(r.StatusCode==HttpStatusCode.Forbidden,$"Workspace write {role}403");
        }
        authenticate(RoleCodes.Dentist,doctor.UserId);
        using(var r=await client.GetAsync(endpoint+"/me")) { var json=await r.Content.ReadFromJsonAsync<JsonElement>(); check(r.StatusCode==HttpStatusCode.OK && r.Headers.CacheControl?.NoStore==true && json.GetProperty("version").GetInt32()==0 && json.GetProperty("chairId").ValueKind==JsonValueKind.Null,"Unassigned dentist reads own actual empty location/no-store"); }
        using(var r=await client.GetAsync(endpoint+"/"+other.UserId)) check(r.StatusCode==HttpStatusCode.Forbidden,"Dentist cannot inspect other workspace403");
        using(var r=await client.GetAsync(endpoint+"/staff")) check(r.StatusCode==HttpStatusCode.Forbidden,"Dentist cannot list admin targets403");
        using(var r=await client.PutAsJsonAsync(endpoint+"/"+other.UserId,new ChangeWorkspaceRequest(chair.Id,null,0,true))) check(r.StatusCode==HttpStatusCode.Forbidden,"Dentist cannot change other workspace403");
        using(var r=await client.PutAsJsonAsync(endpoint+"/"+doctor.UserId,new ChangeWorkspaceRequest(chair.Id,null,0,false))) check(r.StatusCode==HttpStatusCode.BadRequest,"Workspace confirmation required400");
        using(var r=await client.PutAsJsonAsync(endpoint+"/"+doctor.UserId,new ChangeWorkspaceRequest(chair.Id,other.UserId,0,true))) check(r.StatusCode==HttpStatusCode.BadRequest,"Workspace cannot set chair and dentist together400");
        using(var r=await client.PutAsJsonAsync(endpoint+"/"+doctor.UserId,new ChangeWorkspaceRequest(maintenance.Id,null,0,true))) check(r.StatusCode==HttpStatusCode.Conflict,"Maintenance chair cannot be selected409");
        using(var r=await client.PutAsJsonAsync(endpoint+"/"+doctor.UserId,new ChangeWorkspaceRequest(chair.Id,null,0,true))) check(r.StatusCode==HttpStatusCode.OK,"Dentist selects own chair200");
        check(await db.StaffWorkspaces.AnyAsync(w=>w.UserId==doctor.UserId && w.ChairId==chair.Id && w.DentistId==null && w.Version==1) &&
            await db.DentalChairs.AnyAsync(c=>c.Id==chair.Id && c.Status==DentalChairStatus.Available),"Workspace claim preserves Available until clinical Visit starts");
        authenticate(RoleCodes.Assistant,assistant.UserId);
        using(var r=await client.PutAsJsonAsync(endpoint+"/"+assistant.UserId,new ChangeWorkspaceRequest(chair.Id,null,0,true))) check(r.StatusCode==HttpStatusCode.BadRequest,"Assistant cannot independently select chair400");
        using(var r=await client.PutAsJsonAsync(endpoint+"/"+assistant.UserId,new ChangeWorkspaceRequest(null,other.UserId,0,true))) check(r.StatusCode==HttpStatusCode.OK,"Assistant may link before dentist selects a room200");
        using(var r=await client.PutAsJsonAsync(endpoint+"/"+assistant.UserId,new ChangeWorkspaceRequest(null,doctor.UserId,1,true))) { var json=await r.Content.ReadFromJsonAsync<JsonElement>(); check(r.StatusCode==HttpStatusCode.OK && json.GetProperty("roomName").GetString()=="Phòng 3","Assistant selects dentist and immediately inherits room"); }
        check(await db.StaffWorkspaces.AnyAsync(w=>w.UserId==assistant.UserId && w.DentistId==doctor.UserId && w.ChairId==null),"Assistant persists only dentist link, never copied chair");
        authenticate(RoleCodes.Assistant,assistant2.UserId);
        using(var r=await client.PutAsJsonAsync(endpoint+"/"+assistant2.UserId,new ChangeWorkspaceRequest(null,doctor.UserId,0,true))) check(r.StatusCode==HttpStatusCode.OK,"One dentist may have a second assistant200");
        check(await db.StaffWorkspaces.CountAsync(w=>w.DentistId==doctor.UserId)==2 &&
            await db.AuditLogs.CountAsync(a=>a.EntityType=="StaffWorkspace" && (a.EntityId==assistant.UserId || a.EntityId==assistant2.UserId))==3,
            "Both assistant links persist with required audit");
        authenticate(RoleCodes.Dentist,other.UserId);
        using(var r=await client.PutAsJsonAsync(endpoint+"/"+other.UserId,new ChangeWorkspaceRequest(chair.Id,null,0,true))) check(r.StatusCode==HttpStatusCode.Conflict,"One chair cannot be claimed by another dentist409");
        authenticate(RoleCodes.Dentist,doctor.UserId);
        using(var r=await client.PutAsJsonAsync(endpoint+"/"+doctor.UserId,new ChangeWorkspaceRequest(second.Id,null,1,true))) check(r.StatusCode==HttpStatusCode.OK,"Dentist changes to new room200");
        authenticate(RoleCodes.Assistant,assistant.UserId);
        using(var r=await client.GetAsync(endpoint+"/me")) { var json=await r.Content.ReadFromJsonAsync<JsonElement>(); check(json.GetProperty("roomName").GetString()=="Phòng 4" && json.GetProperty("chairId").GetInt32()==second.Id && json.GetProperty("version").GetInt32()==2,"Assistant follows moved dentist without rewriting own workspace/version"); }
        using(var r=await client.GetAsync(endpoint+"/options")) { var json=await r.Content.ReadFromJsonAsync<JsonElement>(); check(r.Headers.CacheControl?.NoStore==true && !json.ToString().Contains("password",StringComparison.OrdinalIgnoreCase) && !json.ToString().Contains("phone",StringComparison.OrdinalIgnoreCase),"Workspace options no-store and no contact/credential data"); }
        authenticate(RoleCodes.Admin,admin.UserId);
        using(var r=await client.GetAsync(endpoint+"/staff?page=0")) check(r.StatusCode==HttpStatusCode.BadRequest,"Admin staff query bounded400");
        using(var r=await client.GetAsync(endpoint+"/staff")) { var json=await r.Content.ReadFromJsonAsync<JsonElement>(); check(r.StatusCode==HttpStatusCode.OK && r.Headers.CacheControl?.NoStore==true && json.GetProperty("items").EnumerateArray().All(x=>x.EnumerateObject().Count()==4),"Admin staff lookup uses minimal four-field DTO/no-store"); }
        using(var r=await client.PutAsJsonAsync($"/api/master/chairs/{second.Id}",new UpdateDentalChairRequest("Changed",3,null,1,true))) check(r.StatusCode==HttpStatusCode.Conflict,"Catalog cannot move/deactivate a claimed chair409");
        var patient=new Patient {FullName="Synthetic workspace clinical",Phone="0999999940"};
        var visit=new Visit {Patient=patient,CreatedByUserId=baseActorId,DentistId=doctor.UserId,Status=VisitStatuses.InProgress,StartedAt=DateTime.UtcNow};
        db.Visits.Add(visit); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        authenticate(RoleCodes.Dentist,doctor.UserId);
        using(var r=await client.PutAsJsonAsync(endpoint+"/"+doctor.UserId,new ChangeWorkspaceRequest(chair.Id,null,2,true))) check(r.StatusCode==HttpStatusCode.Conflict,"Active clinical Visit blocks dentist moving409");
        authenticate(RoleCodes.Assistant,assistant.UserId);
        using(var r=await client.GetAsync(endpoint+"/me")) { var json=await r.Content.ReadFromJsonAsync<JsonElement>(); check(json.GetProperty("canChange").GetBoolean(),"Assistant may change support link while dentist is busy"); }
        using(var r=await client.PutAsJsonAsync(endpoint+"/"+assistant.UserId,new ChangeWorkspaceRequest(null,null,2,true))) check(r.StatusCode==HttpStatusCode.OK,"Assistant may detach during active clinical Visit200");
        check(await db.Visits.AnyAsync(v=>v.VisitId==visit.VisitId && v.DentistId==doctor.UserId && v.Status==VisitStatuses.InProgress),
            "Changing assistant link leaves active patient visit with dentist");
        authenticate(RoleCodes.Admin,admin.UserId);
        using(var r=await client.PutAsJsonAsync(endpoint+"/"+doctor.UserId,new ChangeWorkspaceRequest(null,null,2,true))) check(r.StatusCode==HttpStatusCode.Conflict,"Admin cannot bypass active Visit restriction409");
        var tracked=await db.Visits.SingleAsync(v=>v.VisitId==visit.VisitId); tracked.Status=VisitStatuses.Completed; tracked.EndedAt=DateTime.UtcNow; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var queue=new QueueEntry {PatientId=patient.PatientId,VisitId=visit.VisitId,DentistId=doctor.UserId,QueueDate=new VietnamClock(TimeProvider.System).Today,QueueNumber=1,Status=QueueStatus.Calling,CheckInTime=DateTime.UtcNow};
        db.QueueEntries.Add(queue); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        using(var r=await client.PutAsJsonAsync(endpoint+"/"+doctor.UserId,new ChangeWorkspaceRequest(null,null,2,true))) check(r.StatusCode==HttpStatusCode.Conflict,"Pending Calling blocks workspace change409");
        var pending=await db.QueueEntries.SingleAsync(q=>q.QueueEntryId==queue.QueueEntryId); pending.Status=QueueStatus.Cancelled; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        using(var r=await client.PutAsJsonAsync(endpoint+"/"+doctor.UserId,new ChangeWorkspaceRequest(chair.Id,null,2,true))) check(r.StatusCode==HttpStatusCode.OK,"Admin may intervene after busy state ends");
        using(var r=await client.PutAsJsonAsync(endpoint+"/"+doctor.UserId,new ChangeWorkspaceRequest(second.Id,null,2,true))) check(r.StatusCode==HttpStatusCode.Conflict,"Stale workspace version409");
        var faultDoctor=await Staff(RoleCodes.Dentist); var faultChair=await Chair("Fault");
        var faultOptions=new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new FailAudit()).Options;
        await using(var context=new DentalDbContext(faultOptions))
        {
            var threw=false; try { await Service(context).ChangeAsync(faultDoctor.UserId,RoleCodes.Dentist,faultDoctor.UserId,new(faultChair.Id,null,0,true)); }
            catch(InvalidOperationException e) when(e.Message=="Synthetic workspace audit failure") { threw=true; }
            check(threw && !await db.StaffWorkspaces.AnyAsync(w=>w.UserId==faultDoctor.UserId) && !await db.AuditLogs.AnyAsync(a=>a.EntityType=="StaffWorkspace" && a.EntityId==faultDoctor.UserId),"Failed required audit rolls back new workspace and history");
        }
        await using(var context=new DentalDbContext(options)) check((await Service(context).ChangeAsync(faultDoctor.UserId,RoleCodes.Dentist,faultDoctor.UserId,new(faultChair.Id,null,0,true))).IsSuccess,"Workspace may be retried after full audit rollback");
        var faultSecond=await Chair("Fault second");
        await using(var context=new DentalDbContext(faultOptions))
        {
            var threw=false; try { await Service(context).ChangeAsync(faultDoctor.UserId,RoleCodes.Dentist,faultDoctor.UserId,new(faultSecond.Id,null,1,true)); }
            catch(InvalidOperationException e) when(e.Message=="Synthetic workspace audit failure") { threw=true; }
            check(threw && await db.StaffWorkspaces.AsNoTracking().AnyAsync(w=>w.UserId==faultDoctor.UserId && w.ChairId==faultChair.Id && w.Version==1) &&
                await db.AuditLogs.CountAsync(a=>a.EntityType=="StaffWorkspace" && a.EntityId==faultDoctor.UserId)==1,"Existing workspace audit failure preserves prior claim/version/audit");
        }
        // Hai nha sĩ tranh cùng một ghế: chỉ một giao dịch được ghi.
        var left=await Staff(RoleCodes.Dentist); var right=await Staff(RoleCodes.Dentist); var shared=await Chair("Shared");
        var raceOptions=new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new Rendezvous()).Options;
        async Task<Result<WorkspaceSnapshot>> Claim(User user,int chairId)
        { await using var context=new DentalDbContext(raceOptions); return await Service(context).ChangeAsync(user.UserId,RoleCodes.Dentist,user.UserId,new(chairId,null,0,true)); }
        var outcomes=await Task.WhenAll(Claim(left,shared.Id),Claim(right,shared.Id));
        check(outcomes.Count(r=>r.IsSuccess)==1 && outcomes.Count(r=>r.Error.Code=="MST_061")==1,"Concurrent chair claim has one commit and one conflict");
        var winner=outcomes.Single(r=>r.IsSuccess).Value!;
        check(await db.StaffWorkspaces.CountAsync(w=>w.ChairId==shared.Id)==1 && await db.AuditLogs.CountAsync(a=>a.EntityType=="StaffWorkspace" && a.EntityId==winner.UserId)==1,"Claim race leaves one owner and audit");
        var sameDoctor=await Staff(RoleCodes.Dentist); var firstChoice=await Chair("Same user A"); var secondChoice=await Chair("Same user B");
        raceOptions=new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new Rendezvous()).Options;
        outcomes=await Task.WhenAll(Claim(sameDoctor,firstChoice.Id),Claim(sameDoctor,secondChoice.Id));
        check(outcomes.Count(r=>r.IsSuccess)==1 && outcomes.Count(r=>r.Error.Code=="MST_061")==1,"Two initial choices for same user resolve primary-key race to409");
        check(await db.StaffWorkspaces.CountAsync(w=>w.UserId==sameDoctor.UserId)==1 && await db.AuditLogs.CountAsync(a=>a.EntityType=="StaffWorkspace" && a.EntityId==sameDoctor.UserId)==1,"Initial workspace race creates one row and one audit");
        // Hai phụ tá có thể cùng liên kết với một nha sĩ khi thao tác đồng thời.
        var a1=await Staff(RoleCodes.Assistant); var a2=await Staff(RoleCodes.Assistant);
        raceOptions=new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new Rendezvous()).Options;
        async Task<Result<WorkspaceSnapshot>> Pair(User user)
        { await using var context=new DentalDbContext(raceOptions); return await Service(context).ChangeAsync(user.UserId,RoleCodes.Assistant,user.UserId,new(null,winner.UserId,0,true)); }
        outcomes=await Task.WhenAll(Pair(a1),Pair(a2));
        check(outcomes.Count(r=>r.IsSuccess)>=1 && outcomes.All(r=>r.IsSuccess || r.Error.Code=="MST_061"),
            "Concurrent assistant links commit or return a retryable serialization conflict");
        if(outcomes.Count(r=>r.IsSuccess)==1)
        {
            raceOptions=options;
            check((await Pair(outcomes[0].IsSuccess ? a2 : a1)).IsSuccess,"Conflicted assistant can retry after reloading");
        }
        check(await db.StaffWorkspaces.CountAsync(w=>w.DentistId==winner.UserId)==2 &&
            await db.AuditLogs.CountAsync(a=>a.EntityType=="StaffWorkspace" && (a.EntityId==a1.UserId || a.EntityId==a2.UserId))==2,
            "Both assistant links persist with required audit after contention");
        // Claim versus maintenance has a read/write cycle and must not leave an unusable assigned chair.
        var racingDoctor=await Staff(RoleCodes.Dentist); var racingChair=await Chair("Maintenance race");
        raceOptions=new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(new Rendezvous()).Options;
        async Task<bool> Select()
        { await using var context=new DentalDbContext(raceOptions); var result=await Service(context).ChangeAsync(racingDoctor.UserId,RoleCodes.Dentist,racingDoctor.UserId,new(racingChair.Id,null,0,true)); return result.IsSuccess; }
        async Task<bool> Maintain()
        { await using var context=new DentalDbContext(raceOptions); var result=await Catalog(context).UpdateAsync(racingChair.Id,admin.UserId,RoleCodes.Admin,new("Maintenance race",3,null,1,true)); return result.IsSuccess; }
        var maintained=await Task.WhenAll(Select(),Maintain()); check(maintained.Count(x=>x)==1,"Chair claim versus maintenance commits one outcome");
        var claimed=await db.StaffWorkspaces.AnyAsync(w=>w.ChairId==racingChair.Id);
        check(await db.DentalChairs.AnyAsync(c=>c.Id==racingChair.Id && (claimed ? c.Status==DentalChairStatus.Available : c.Status==DentalChairStatus.Maintenance)),"Maintenance race cannot create an assigned unusable chair");
        var disabled=await db.Users.SingleAsync(u=>u.UserId==doctor.UserId); disabled.IsActive=false; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        authenticate(RoleCodes.Dentist,doctor.UserId);
        using(var r=await client.GetAsync(endpoint+"/me")) check(r.StatusCode==HttpStatusCode.Forbidden,"Stale JWT for inactive staff denied403");
        authenticate(RoleCodes.Admin,admin.UserId);
        using(var r=await client.PutAsJsonAsync(endpoint+"/"+doctor.UserId,new ChangeWorkspaceRequest(null,null,3,true))) check(r.StatusCode==HttpStatusCode.OK,"Admin clears inactive dentist claim");
        authenticate(RoleCodes.Assistant,assistant.UserId);
        using(var r=await client.GetAsync(endpoint+"/me")) { var json=await r.Content.ReadFromJsonAsync<JsonElement>(); check(json.GetProperty("chairId").ValueKind==JsonValueKind.Null && !json.GetProperty("isLocationUsable").GetBoolean(),"Assistant never keeps stale copied room after dentist claim cleared"); }
        check(!await db.StaffWorkspaces.AnyAsync(w=>w.ChairId!=null && w.DentistId!=null),"Workspace never stores chair and dentist link together");
    }
    private sealed class Rendezvous : SaveChangesInterceptor
    {
        private int arrivals; private readonly TaskCompletionSource ready=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default)
        { if(Interlocked.Increment(ref arrivals)==2) ready.TrySetResult(); await ready.Task.WaitAsync(TimeSpan.FromSeconds(15),ct); return result; }
    }
    private sealed class FailAudit : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default)
        { if(data.Context!.ChangeTracker.Entries<AuditLog>().Any(e=>e.State==EntityState.Added)) throw new InvalidOperationException("Synthetic workspace audit failure"); return ValueTask.FromResult(result); }
    }
}
