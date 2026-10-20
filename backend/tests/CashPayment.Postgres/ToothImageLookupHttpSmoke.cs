using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Dental.Api.Controllers;
using Dental.Api.Policies;
using Dental.Api.RateLimiting;
using Dental.Application.Features.MOD_IMG;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Repositories;
using Dental.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

internal static class ToothImageLookupHttpSmoke
{
    private sealed class FailAudit : SaveChangesInterceptor
    {
        public bool Fail { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (Fail && data.Context!.ChangeTracker.Entries<AuditLog>().Any(x => x.State == EntityState.Added && x.Entity.Action.StartsWith("MOD_IMG"))) throw new InvalidOperationException("Synthetic audit failure");
            return base.SavingChangesAsync(data, result, ct);
        }
    }
    public static async Task RunAsync(DbContextOptions<DentalDbContext> options, Action<bool, string> check)
    {
        var root = Path.Combine(Path.GetTempPath(), "dental-images-http-" + Guid.NewGuid().ToString("N")); var failure = new FailAudit();
        var dbOptions = new DbContextOptionsBuilder<DentalDbContext>(options).AddInterceptors(failure).Options;
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], EnvironmentName = "Testing" });
        builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)); builder.Configuration["Jwt:Secret"] = secret;
        builder.Services.AddScoped(_ => new DentalDbContext(dbOptions)); builder.Services.AddHttpContextAccessor(); builder.Services.AddScoped<ICurrentUser, CurrentUser>();
        builder.Services.AddSingleton<IDentalImageStorage>(new DentalImageStorage(root)); builder.Services.AddScoped<IDentalImageRepository, DentalImageRepository>(); builder.Services.AddScoped<DentalImageService>();
        builder.Services.AddControllers().AddApplicationPart(typeof(DentalImagesController).Assembly);
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(jwt => { jwt.TokenValidationParameters = new TokenValidationParameters { ValidateIssuer = true, ValidIssuer = "DentalClinic", ValidateAudience = true, ValidAudience = "DentalClinicClient", IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), ClockSkew = TimeSpan.Zero }; jwt.Events = new JwtBearerEvents { OnTokenValidated = AccessTokenVersionValidation.ValidateAsync }; });
        builder.Services.AddAuthorization(x => x.AddApplicationPolicies());
        builder.Services.AddRateLimiter(o => { o.RejectionStatusCode = 429; DentalImageRateLimits.Configure(o); });
        await using var app = builder.Build(); app.Use(async (c, next) => { try { await next(c); } catch { c.Response.StatusCode = 500; } });
        app.UseAuthentication(); app.UseRateLimiter(); app.UseAuthorization(); app.MapControllers(); await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        var tokens = new JwtTokenService(builder.Configuration); await using var db = new DentalDbContext(dbOptions);
        var users = new Dictionary<string, User>();
        foreach (var role in new[] { RoleCodes.Admin, RoleCodes.Dentist, RoleCodes.Assistant, RoleCodes.Receptionist, RoleCodes.Patient })
        { var user = new User { Role = await db.Roles.FirstAsync(r=>r.RoleCode==role), FullName = "Synthetic image account", Phone = "096400000" + users.Count, PasswordHash = "synthetic-not-used" }; users[role] = user; db.Users.Add(user); }
        await db.SaveChangesAsync();
        var patient = new Patient { FullName = "Synthetic image patient", DateOfBirth = new DateOnly(1990, 1, 1), Phone = "0964000008", UserId = users[RoleCodes.Patient].UserId };
        var other = new Patient { FullName = "Synthetic other image patient", DateOfBirth = new DateOnly(1990, 1, 1), Phone = "0964000009" }; db.Patients.AddRange(patient, other); await db.SaveChangesAsync();
        async Task<Visit> Visit(string state = VisitStatuses.InProgress, int? patientId = null, int? dentist = null)
        { var v = new Visit { PatientId = patientId ?? patient.PatientId, Status = state, CreatedByUserId = users[RoleCodes.Admin].UserId, DentistId = dentist ?? users[RoleCodes.Dentist].UserId }; db.Visits.Add(v); await db.SaveChangesAsync(); return v; }
        void As(string? role) => client.DefaultRequestHeaders.Authorization = role is null ? null : new AuthenticationHeaderValue("Bearer", tokens.GenerateAccessToken(users[role]));
        async Task<DentalImage> Image(int? visit,int? tooth,bool deleted=false,int? owner=null){var i=new DentalImage {PatientId=owner??patient.PatientId,VisitId=visit,ToothNumber=tooth,IsDeleted=deleted,StoragePath=$"synthetic/{Guid.NewGuid():N}.png",OriginalFileName="synthetic.png",ContentType="image/png",SizeBytes=20,Width=1,Height=1,UploadedByUserId=users[RoleCodes.Admin].UserId,CreatedAt=DateTime.UtcNow};db.DentalImages.Add(i);await db.SaveChangesAsync();return i;}
        string List(int patientId,int tooth,int before=0,int? visit=null)=>$"/api/dental-images?patientId={patientId}&toothNumber={tooth}&beforeId={before}"+(visit is int id?$"&visitId={id}":"");
        string Context(int id)=>$"/api/dental-images/visit-context/{id}";
        async Task Status(string url,HttpStatusCode status,string label){using var r=await client.GetAsync(url);check(r.StatusCode==status,label+" ("+(int)r.StatusCode+")");}
        try{
            var visits=new List<Visit>();for(var n=0;n<8;n++)visits.Add(await Visit(VisitStatuses.Completed));
            for(var n=0;n<22;n++)await Image(visits[n/3].VisitId,85);
            var removed=await Image(visits.Last().VisitId,85,true);await Image(null,46);await Image(visits[0].VisitId,46);await Image(null,null);
            var foreignVisit=await Visit(patientId:other.PatientId);await Image(foreignVisit.VisitId,85,owner:other.PatientId);
            As(null);await Status(Context(visits[0].VisitId),HttpStatusCode.Unauthorized,"Anonymous cannot resolve image visit");await Status(List(patient.PatientId,85),HttpStatusCode.Unauthorized,"Anonymous cannot filter images");
            As(RoleCodes.Assistant);await Status(Context(visits[0].VisitId),HttpStatusCode.Forbidden,"Assistant read scope remains forbidden");await Status(List(patient.PatientId,85),HttpStatusCode.Forbidden,"Assistant cannot fetch filtered image gallery");
            foreach(var role in new[]{RoleCodes.Admin,RoleCodes.Dentist,RoleCodes.Receptionist,RoleCodes.Patient}){
                As(role);using var context=await client.GetAsync(Context(visits[0].VisitId));var json=await context.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();check(context.StatusCode==HttpStatusCode.OK&&context.Headers.CacheControl?.NoStore==true&&json.GetProperty("patientId").GetInt32()==patient.PatientId&&json.GetProperty("visitId").GetInt32()==visits[0].VisitId,"Visit image context authorized and NoStore "+role);check(json.EnumerateObject().Count()==2,"Visit image context contains no clinical or identity payload "+role);
                using var first=await client.GetAsync(List(patient.PatientId,85));var page=(await first.Content.ReadFromJsonAsync<DentalImagePage>())!;check(first.StatusCode==HttpStatusCode.OK&&first.Headers.CacheControl?.NoStore==true&&page.Items.Length==20&&page.HasMore&&page.Items.All(i=>i.PatientId==patient.PatientId&&i.ToothNumber==85&&i.Id!=removed.Id),"FDI85 filters across historical Visits with bounded page "+role);
                using var second=await client.GetAsync(List(patient.PatientId,85,page.Items.Last().Id));var tail=(await second.Content.ReadFromJsonAsync<DentalImagePage>())!;check(second.StatusCode==HttpStatusCode.OK&&tail.Items.Length==2&&!tail.HasMore&&tail.Items.All(i=>i.ToothNumber==85&&!page.Items.Any(old=>old.Id==i.Id)),"Cursor retains selected FDI without duplicates "+role);
            }
            As(RoleCodes.Patient);await Status(Context(foreignVisit.VisitId),HttpStatusCode.NotFound,"Patient cannot resolve another patient Visit");await Status(List(other.PatientId,85),HttpStatusCode.NotFound,"Patient cannot filter another patient images");
            var forged=await client.GetFromJsonAsync<DentalImagePage>(List(patient.PatientId,85,visit:foreignVisit.VisitId));check(forged!.Items.Length==0,"Forged visit filter cannot cross patient scope");
            var adult=await client.GetFromJsonAsync<DentalImagePage>(List(patient.PatientId,46));check(adult!.Items.Length==2&&adult.Items.All(i=>i.ToothNumber==46),"Adult FDI includes matching independent and linked images only");
            var empty=await client.GetFromJsonAsync<DentalImagePage>(List(patient.PatientId,51));check(empty!.Items.Length==0&&!empty.HasMore,"Valid tooth without images has explicit empty page");
            var narrowed=await client.GetFromJsonAsync<DentalImagePage>(List(patient.PatientId,85,visit:visits[0].VisitId));check(narrowed!.Items.Length==3&&narrowed.Items.All(i=>i.VisitId==visits[0].VisitId),"Existing Visit filter composes with tooth and ownership");
            await Status(List(patient.PatientId,19),HttpStatusCode.BadRequest,"Invalid adult FDI rejected");await Status(List(patient.PatientId,56),HttpStatusCode.BadRequest,"Invalid child FDI rejected");await Status(List(patient.PatientId,85,-1),HttpStatusCode.BadRequest,"Invalid cursor rejected");await Status(Context(0),HttpStatusCode.BadRequest,"Invalid context visit rejected");await Status(Context(int.MaxValue),HttpStatusCode.NotFound,"Unknown context Visit hidden");
            As(RoleCodes.Dentist);users[RoleCodes.Dentist].IsActive=false;await db.SaveChangesAsync();await Status(Context(visits[0].VisitId),HttpStatusCode.NotFound,"Inactive actor cannot resolve Visit with stale JWT");await Status(List(patient.PatientId,85),HttpStatusCode.NotFound,"Inactive actor cannot fetch images");users[RoleCodes.Dentist].IsActive=true;users[RoleCodes.Dentist].Role=users[RoleCodes.Assistant].Role;await db.SaveChangesAsync();await Status(Context(visits[0].VisitId),HttpStatusCode.NotFound,"Actual DB role controls visit context beyond stale token");
            As(RoleCodes.Admin);patient.IsActive=false;await db.SaveChangesAsync();await Status(Context(visits[0].VisitId),HttpStatusCode.NotFound,"Inactive patient context is hidden");await Status(List(patient.PatientId,85),HttpStatusCode.NotFound,"Inactive patient filtered gallery is hidden");
        }finally{await app.StopAsync();}
    }
}
