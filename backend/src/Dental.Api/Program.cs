using System.Text;
using Dental.Api.Middleware;
using Dental.Api.Policies;
using Dental.Api.RateLimiting;
using Dental.Application.Common;
using Dental.Application.Features.Auth.Services;
using Dental.Application.Features.Auth.Validators;
using Dental.Application.Features.MedicalHistory.Services;
using Dental.Application.Features.MedicalHistory.Validators;
using Dental.Application.Features.Patients.Services;
using Dental.Application.Features.Patients.Validators;
using Dental.Application.Features.Staff.Services;
using Dental.Application.Features.ServiceCatalog.Services;
using Dental.Application.Features.ServiceCatalog.Validators;
using Dental.Application.Features.Visits.Services;
using Dental.Application.Features.Visits.Validators;
using Dental.Application.Features.VitalSigns.Services;
using Dental.Application.Features.VitalSigns.Validators;
using Dental.Application.Interfaces;
using Dental.Application.Features.Appointments.Services;
using Dental.Application.Features.Appointments.Validators;
using Dental.Application.Features.Queue.Services;
using Dental.Application.Features.Queue.Validators;

using Dental.Infrastructure.Data;
using Dental.Infrastructure.Data.Seeders;
using Dental.Infrastructure.Repositories;
using Dental.Infrastructure.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddScoped<Dental.Application.Features.MOD_FDI.Services.FdiServiceAssignmentService>();
builder.Services.AddScoped<Dental.Application.Features.MOD_FDI.Validators.AssignServicesRequestValidator>();
builder.Services.AddScoped<IVisitServiceRepository, VisitServiceRepository>();
builder.Services.AddScoped<Dental.Application.Features.MOD_FDI.Services.ToothConditionService>();
builder.Services.AddScoped<Dental.Application.Features.MOD_FDI.Validators.ToothConditionRequestValidator>();
builder.Services.AddScoped<IToothConditionRepository, ToothConditionRepository>();

// ===== 1. Database =====
builder.Services.AddDbContext<DentalDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// ===== 2. Repositories & UnitOfWork =====
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IPatientRepository, PatientRepository>();
builder.Services.AddScoped<IVisitRepository, VisitRepository>();
builder.Services.AddScoped<IMedicalHistoryRepository, MedicalHistoryRepository>();
builder.Services.AddScoped<IVitalSignRepository, VitalSignRepository>();
builder.Services.AddScoped<IServiceCatalogRepository, ServiceCatalogRepository>();
builder.Services.AddScoped<IAppointmentRepository, AppointmentRepository>();
builder.Services.AddScoped<IQueueRepository, QueueRepository>();

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// ===== 3. Infrastructure Services =====
builder.Services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddScoped<IAuditLogger, AuditLogger>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<VietnamClock>();

// ===== 4. Application Services =====
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AuditLogService>();
builder.Services.AddScoped<StaffService>();
builder.Services.AddScoped<PatientsService>();
builder.Services.AddScoped<PatientTimelineService>();
builder.Services.AddScoped<PatientSafetyAlertsService>();
builder.Services.AddScoped<VisitService>();
builder.Services.AddScoped<MedicalHistoryService>();
builder.Services.AddScoped<VitalSignService>();
builder.Services.AddScoped<ServiceCatalogService>();
builder.Services.AddScoped<AppointmentService>();
builder.Services.AddScoped<QueueService>();

builder.Services.AddScoped<CreatePatientRequestValidator>();
builder.Services.AddScoped<PatientDuplicateCheckRequestValidator>();
builder.Services.AddScoped<PatientQueryRequestValidator>();
builder.Services.AddScoped<UpdatePatientRequestValidator>();
builder.Services.AddScoped<PatientTimelineQueryRequestValidator>();
builder.Services.AddScoped<LoginRequestValidator>();
builder.Services.AddScoped<RegisterRequestValidator>();
builder.Services.AddScoped<VisitQueryRequestValidator>();
builder.Services.AddScoped<UpdateVisitDiagnosisRequestValidator>();
builder.Services.AddScoped<RecordMedicalHistoryRequestValidator>();
builder.Services.AddScoped<MedicalHistoryItemRequestValidator>();
builder.Services.AddScoped<MedicalHistoryQueryRequestValidator>();
builder.Services.AddScoped<RecordVitalSignsRequestValidator>();
builder.Services.AddScoped<VitalSignQueryRequestValidator>();
builder.Services.AddScoped<ServiceCatalogQueryRequestValidator>();
builder.Services.AddScoped<CreateDentalServiceRequestValidator>();
builder.Services.AddScoped<UpdateDentalServiceRequestValidator>();
builder.Services.AddScoped<CreateServicePriceRequestValidator>();
builder.Services.AddScoped<ServiceIdRequestValidator>();
builder.Services.AddScoped<AuditLogQueryRequestValidator>();

builder.Services.AddScoped<ChangePasswordRequestValidator>();
builder.Services.AddScoped<CreateAppointmentRequestValidator>();
builder.Services.AddScoped<AppointmentQueryRequestValidator>();
builder.Services.AddScoped<RescheduleAppointmentRequestValidator>();
builder.Services.AddScoped<CancelAppointmentRequestValidator>();
builder.Services.AddScoped<CheckInRequestValidator>();
builder.Services.AddScoped<QueueQueryRequestValidator>();
builder.Services.AddScoped<UpdateQueueStatusRequestValidator>();


// ===== 5. FluentValidation =====
builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();

// ===== 6. JWT Authentication =====
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException(
        "Jwt:Secret chưa được cấu hình. Dùng 'dotnet user-secrets set Jwt:Secret <giá_trị>'");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"] ?? "DentalClinic",
            ValidAudience            = builder.Configuration["Jwt:Audience"] ?? "DentalClinicClient",
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew                = TimeSpan.Zero, // không cho phép slack thời gian
        };
    });

// ===== 7. Authorization Policies =====
builder.Services.AddAuthorization(opts => opts.AddApplicationPolicies());

// [CẦN XÁC NHẬN] Ngưỡng mặc định là 10 lần/phút cho đăng nhập và 5 lần/phút cho đăng ký.
builder.Services.AddRateLimiter(options => AuthRateLimitPolicies.Configure(options, builder.Configuration));

// ===== 8. CORS (chỉ cho phép frontend dev) =====
builder.Services.AddCors(opts =>
{
    opts.AddPolicy("FrontendDev", policy =>
        policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

// ===== 9. Controllers + Swagger =====
builder.Services.AddControllers();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title   = "Dental Clinic API",
            Version = "v1",
            Description = "Hệ thống quản lý phòng khám nha khoa — đồ án tốt nghiệp",
        });

        // Hỗ trợ nhập JWT token trong Swagger UI
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "Nhập token dạng: Bearer {token}",
            Name        = "Authorization",
            In          = ParameterLocation.Header,
            Type        = SecuritySchemeType.ApiKey,
            Scheme      = "Bearer",
        });
        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
                Array.Empty<string>()
            }
        });
    });
}

// ===== Build =====
var app = builder.Build();

// ===== Middleware pipeline =====
app.UseMiddleware<GlobalExceptionMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/api/dev"))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await next();
    });
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Dental API v1"));
}
else
{
    app.UseHttpsRedirection();
}

app.UseCors("FrontendDev");
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ===== Auto-migrate + Seed khi khởi động =====
using (var scope = app.Services.CreateScope())
{
    var db     = scope.ServiceProvider.GetRequiredService<DentalDbContext>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    await db.Database.MigrateAsync();

    await DatabaseSeeder.SeedAsync(
        db,
        config,
        logger,
        isDevelopment: app.Environment.IsDevelopment());
}

app.Run();

// Expose Program cho integration tests
public partial class Program { }
