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
builder.Services.AddScoped<Dental.Application.Features.MOD_MST.Services.ISchedulingSettingsRepository, SchedulingSettingsRepository>();
builder.Services.AddScoped<Dental.Application.Features.MOD_MST.Services.SchedulingSettingsService>();
builder.Services.AddScoped<Dental.Application.Features.MOD_INV.IStockIssueRepository, StockIssueRepository>();
builder.Services.AddScoped<Dental.Application.Features.MOD_INV.StockIssueService>();
builder.Services.AddScoped<Dental.Application.Features.MOD_RX.IMedicineStockRepository, MedicineStockRepository>();
builder.Services.AddScoped<Dental.Application.Features.MOD_RX.MedicineStockService>();
builder.Services.AddScoped<Dental.Application.Features.MOD_INV.IStockReceiptRepository, StockReceiptRepository>();
builder.Services.AddScoped<Dental.Application.Features.MOD_INV.StockReceiptService>();
builder.Services.AddScoped<Dental.Application.Features.MOD_IMG.IDentalImageRepository, DentalImageRepository>();
builder.Services.AddScoped<Dental.Application.Features.MOD_IMG.DentalImageService>();
builder.Services.AddScoped<IDentalImageClinicalRepository, DentalImageClinicalRepository>();
builder.Services.AddScoped<Dental.Application.Features.MOD_IMG.DentalImageClinicalService>();
builder.Services.AddScoped<Dental.Application.Features.MOD_IMG.IDentalImageDeletionRepository, DentalImageDeletionRepository>();
builder.Services.AddScoped<Dental.Application.Features.MOD_IMG.DentalImageDeletionService>();
builder.Services.AddScoped<Dental.Application.Features.MOD_IMG.IDentalImageMetadataRepository, DentalImageMetadataRepository>();
builder.Services.AddScoped<Dental.Application.Features.MOD_IMG.DentalImageMetadataService>();
builder.Services.AddSingleton<Dental.Application.Features.MOD_IMG.IDentalImageStorage>(new DentalImageStorage(builder.Environment.ContentRootPath));
builder.Services.AddScoped<IPrescriptionRepository, PrescriptionRepository>();
builder.Services.AddScoped<IPrescriptionHandoverRepository, PrescriptionHandoverRepository>();
builder.Services.AddScoped<Dental.Application.Features.MOD_RX.Services.PrescriptionHandoverService>();
builder.Services.AddScoped<Dental.Application.Features.MOD_RX.Services.PrescriptionService>();
builder.Services.AddScoped<IMedicineCatalogRepository, MedicineCatalogRepository>();
builder.Services.AddScoped<Dental.Application.Features.MOD_MST.Services.MedicineCatalogService>();
builder.Services.AddScoped<IRevenueReportRepository, RevenueReportRepository>();
builder.Services.AddScoped<Dental.Application.Features.MOD_RPT.Services.RevenueReportService>();
builder.Services.AddScoped<IMedicalSupplyCatalogRepository, MedicalSupplyCatalogRepository>();
builder.Services.AddScoped<Dental.Application.Features.MOD_MST.Services.MedicalSupplyCatalogService>();
builder.Services.AddScoped<IRolePermissionMatrixProvider, RolePermissionMatrixProvider>();
builder.Services.AddScoped<RolePermissionMatrixService>();
builder.Services.AddScoped<IInvoiceDiscountRepository, InvoiceDiscountRepository>();
        builder.Services.AddScoped<IInvoiceRefundRepository, InvoiceRefundRepository>();
        builder.Services.AddScoped<IInvoiceRefundProposalRepository, InvoiceRefundProposalRepository>();
        builder.Services.AddScoped<Dental.Application.Features.MOD_BIL.Services.InvoiceRefundProposalService>();
        builder.Services.AddScoped<Dental.Application.Features.MOD_BIL.Services.InvoiceRefundService>();
builder.Services.AddScoped<Dental.Application.Features.MOD_BIL.Services.InvoiceDiscountService>();
builder.Services.AddScoped<Dental.Application.Features.MOD_BIL.Validators.DiscountPresetRequestValidator>();
builder.Services.AddScoped<Dental.Application.Features.MOD_BIL.Validators.ApplyInvoiceDiscountRequestValidator>();
builder.Services.AddScoped<Dental.Application.Features.MOD_BIL.Services.InvoiceDraftService>();
builder.Services.AddScoped<IInvoiceDraftGenerator>(services => services.GetRequiredService<Dental.Application.Features.MOD_BIL.Services.InvoiceDraftService>());
builder.Services.AddScoped<Dental.Application.Features.MOD_BIL.Validators.InvoiceVisitRequestValidator>();
builder.Services.AddScoped<IInvoiceRepository, InvoiceRepository>();
builder.Services.AddScoped<ICashPaymentRepository, CashPaymentRepository>();
builder.Services.AddScoped<IReceiptRepository, ReceiptRepository>();
builder.Services.AddScoped<Dental.Application.Features.MOD_BIL.Services.ReceiptPrintService>();
builder.Services.AddScoped<Dental.Application.Features.MOD_BIL.Validators.ReceiptRequestValidator>();
builder.Services.AddScoped<IBankTransferRepository, BankTransferRepository>();
builder.Services.AddScoped<Dental.Application.Features.MOD_BIL.Services.BankTransferWebhookService>();
builder.Services.AddScoped<Dental.Application.Features.MOD_BIL.Services.ManualBankTransferService>();
builder.Services.AddScoped<Dental.Application.Features.MOD_BIL.Validators.BankTransferRequestValidator>();
builder.Services.AddAuthentication().AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, BankWebhookAuthenticationHandler>(BankWebhookAuthenticationHandler.SchemeName, _ => { });
builder.Services.AddScoped<Dental.Application.Features.MOD_BIL.Services.CashPaymentService>();
builder.Services.AddScoped<Dental.Application.Features.MOD_BIL.Validators.CashPaymentRequestValidator>();
builder.Services.AddScoped<IVisitReopenRepository, VisitReopenRepository>();
builder.Services.AddScoped<Dental.Application.Features.MOD_BIL.Services.VisitReopenService>();
builder.Services.AddScoped<Dental.Application.Features.MOD_BIL.Validators.UnlockVisitRequestValidator>();
builder.Services.AddScoped<Dental.Application.Features.MOD_FDI.Services.FdiServiceAssignmentService>();
builder.Services.AddScoped<Dental.Application.Features.MOD_FDI.Validators.AssignServicesRequestValidator>();
builder.Services.AddScoped<IVisitServiceRepository, VisitServiceRepository>();
builder.Services.AddScoped<IFdiServiceDeletionRepository, FdiServiceDeletionRepository>();
builder.Services.AddScoped<Dental.Application.Features.MOD_FDI.Services.FdiServiceDeletionService>();
builder.Services.AddScoped<Dental.Application.Features.MOD_FDI.Validators.DeleteVisitServiceRequestValidator>();
builder.Services.AddScoped<Dental.Application.Features.MOD_FDI.Services.ToothConditionService>();
builder.Services.AddScoped<Dental.Application.Features.MOD_FDI.Validators.ToothConditionRequestValidator>();
builder.Services.AddScoped<IToothConditionRepository, ToothConditionRepository>();
builder.Services.AddScoped<IToothHistoryRepository, ToothHistoryRepository>();
builder.Services.AddScoped<Dental.Application.Features.MOD_FDI.Services.ToothHistoryService>();
builder.Services.AddScoped<Dental.Application.Features.MOD_FDI.Validators.ToothHistoryRequestValidator>();

// ===== 1. Database =====
builder.Services.AddDbContext<DentalDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// ===== 2. Repositories & UnitOfWork =====
builder.Services.AddScoped<IDiagnosisCatalogRepository, DiagnosisCatalogRepository>();
builder.Services.AddScoped<Dental.Application.Features.MOD_MST.Services.DiagnosisCatalogService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IPatientRepository, PatientRepository>();
builder.Services.AddScoped<IPatientCitizenIdRepository, PatientCitizenIdRepository>();
builder.Services.AddScoped<Dental.Application.Features.MOD_APP.IBookingRequestRepository, BookingRequestRepository>();
builder.Services.AddScoped<Dental.Application.Features.MOD_APP.BookingRequestService>();
builder.Services.AddScoped<Dental.Application.Features.MOD_APP.IBookingApprovalRepository, BookingApprovalRepository>();
builder.Services.AddScoped<Dental.Application.Features.MOD_APP.BookingApprovalService>();
builder.Services.AddHttpClient<IBookingOtpDelivery, BookingOtpSms>(client => client.Timeout=TimeSpan.FromSeconds(10)).RemoveAllLoggers();
builder.Services.AddSingleton<Dental.Application.Features.MOD_APP.IBookingOtp>(provider => new BookingOtp(provider.GetRequiredService<IBookingOtpDelivery>(),provider.GetRequiredService<TimeProvider>()));
builder.Services.AddScoped<Dental.Application.Features.MOD_PAT.IPatientAccountLinkRepository, PatientAccountLinkRepository>();
builder.Services.AddScoped<Dental.Application.Features.MOD_PAT.PatientAccountLinkService>();
builder.Services.AddScoped<IQrPatientIntakeRepository, QrPatientIntakeRepository>();
builder.Services.AddScoped<Dental.Application.Features.MOD_PAT.QrPatientIntakeService>();
builder.Services.AddScoped<IClinicPrintSettingsRepository, ClinicPrintSettingsRepository>();
builder.Services.AddScoped<Dental.Application.Features.MOD_MST.Services.ClinicPrintSettingsService>();
builder.Services.AddScoped<IDoctorSignatureRepository, DoctorSignatureRepository>();
builder.Services.AddScoped<Dental.Application.Features.MOD_AUTH.DoctorSignatureService>();
builder.Services.AddScoped<IPublicPrescriptionRepository, PublicPrescriptionRepository>();
builder.Services.AddScoped<Dental.Application.Features.MOD_RX.PublicPrescriptionService>();
builder.Services.AddDataProtection();
builder.Services.AddSingleton<IPublicPrescriptionAccess, PublicPrescriptionAccess>();
builder.Services.AddScoped<Dental.Application.Features.MOD_PAT.PatientCitizenIdService>();
builder.Services.AddScoped<IVisitRepository, VisitRepository>();
builder.Services.AddScoped<IPostTreatmentNoteRepository, PostTreatmentNoteRepository>();
builder.Services.AddScoped<IMedicalHistoryRepository, MedicalHistoryRepository>();
builder.Services.AddScoped<IVitalSignRepository, VitalSignRepository>();
builder.Services.AddScoped<IServiceCatalogRepository, ServiceCatalogRepository>();
builder.Services.AddScoped<IAppointmentRepository, AppointmentRepository>();
builder.Services.AddScoped<IAppointmentReminderRepository, AppointmentReminderRepository>();
builder.Services.AddScoped<AppointmentReminderService>();
builder.Services.AddScoped<SimulateAppointmentRemindersValidator>();
builder.Services.AddScoped<ReminderLogQueryValidator>();
builder.Services.AddSingleton<AppointmentReminderWorkerStatus>();
builder.Services.AddSingleton<IAppointmentReminderWorkerStatus>(sp => sp.GetRequiredService<AppointmentReminderWorkerStatus>());
builder.Services.AddHostedService<AppointmentReminderWorker>();
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
builder.Services.AddScoped<Dental.Application.Features.MOD_AUTH.PasswordRecoveryService>();
builder.Services.AddScoped<Dental.Application.Features.MOD_AUTH.IPasswordRecoveryRepository, PasswordRecoveryRepository>();
builder.Services.AddSingleton<Dental.Application.Features.MOD_AUTH.IPasswordRecoveryCodes, PasswordRecoveryCodes>();
builder.Services.AddSingleton<PasswordRecoveryDelivery>();
builder.Services.AddSingleton<Dental.Application.Features.MOD_AUTH.IPasswordRecoveryDelivery>(sp => sp.GetRequiredService<PasswordRecoveryDelivery>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<PasswordRecoveryDelivery>());
builder.Services.AddHttpClient<Dental.Application.Features.MOD_AUTH.IPasswordRecoverySms, PasswordRecoverySms>(client => client.Timeout = TimeSpan.FromSeconds(10))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false }).RemoveAllLoggers();
builder.Services.AddScoped<Dental.Application.Features.MOD_AUTH.IPatientActivationRepository, PatientActivationRepository>();
builder.Services.AddScoped<Dental.Application.Features.MOD_AUTH.PatientActivationService>();
builder.Services.AddSingleton<Dental.Application.Features.MOD_AUTH.IPatientActivationProofs, PatientActivationProofs>();
builder.Services.AddSingleton<PatientActivationDelivery>();
builder.Services.AddSingleton<Dental.Application.Features.MOD_AUTH.IPatientActivationDelivery>(sp => sp.GetRequiredService<PatientActivationDelivery>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<PatientActivationDelivery>());
builder.Services.AddHttpClient<Dental.Application.Features.MOD_AUTH.IPatientActivationSms, PatientActivationSms>(client => client.Timeout = TimeSpan.FromSeconds(10))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false }).RemoveAllLoggers();
builder.Services.AddScoped<AuditLogService>();
builder.Services.AddScoped<Dental.Application.Features.Staff.IStaffPhoneChangeRepository, StaffPhoneChangeRepository>();
builder.Services.AddScoped<Dental.Application.Features.Staff.StaffPhoneChangeService>();
builder.Services.AddSingleton<Dental.Application.Features.Staff.IStaffPhoneChangeProofs, StaffPhoneChangeProofs>();
builder.Services.AddHttpClient<Dental.Application.Features.Staff.IStaffPhoneChangeSms, StaffPhoneChangeSms>(client => client.Timeout = TimeSpan.FromSeconds(10))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false }).RemoveAllLoggers();
builder.Services.AddScoped<Dental.Application.Features.MOD_AUTH.IPatientPhoneChangeRepository, PatientPhoneChangeRepository>();
builder.Services.AddScoped<Dental.Application.Features.MOD_AUTH.PatientPhoneChangeService>();
builder.Services.AddScoped<Dental.Application.Features.MOD_AUTH.ICounterPatientPhoneRepository, CounterPatientPhoneRepository>();
builder.Services.AddScoped<Dental.Application.Features.MOD_AUTH.CounterPatientPhoneService>();
builder.Services.AddSingleton<Dental.Application.Features.MOD_AUTH.IPatientPhoneChangeProofs, PatientPhoneChangeProofs>();
builder.Services.AddHttpClient<Dental.Application.Features.MOD_AUTH.IPatientPhoneChangeSms, PatientPhoneChangeSms>(client => client.Timeout = TimeSpan.FromSeconds(10))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false }).RemoveAllLoggers();
builder.Services.AddScoped<StaffService>();
builder.Services.AddScoped<PatientsService>();
builder.Services.AddScoped<PatientTimelineService>();
builder.Services.AddScoped<PatientSafetyAlertsService>();
builder.Services.AddScoped<VisitService>();
builder.Services.AddScoped<PostTreatmentNoteService>();
builder.Services.AddScoped<MedicalHistoryService>();
builder.Services.AddScoped<VitalSignService>();
builder.Services.AddScoped<ServiceCatalogService>();
builder.Services.AddScoped<AppointmentService>();
builder.Services.AddScoped<IDentalChairRepository, DentalChairRepository>();
builder.Services.AddScoped<IChairStatusRepository, ChairStatusRepository>();
builder.Services.AddScoped<Dental.Application.Features.MOD_ASS.Services.ChairStatusService>();
builder.Services.AddScoped<IStaffWorkspaceRepository, StaffWorkspaceRepository>();
builder.Services.AddScoped<Dental.Application.Features.MOD_MST.Services.StaffWorkspaceService>();
builder.Services.AddScoped<Dental.Application.Features.MOD_MST.Services.DentalChairService>();
builder.Services.AddScoped<IVisitChairCoordinator, Dental.Application.Features.MOD_MST.Services.VisitChairService>();
builder.Services.AddScoped<QueueService>();
builder.Services.AddScoped<QueueCleanupService>();
builder.Services.AddScoped<QueueCleanupRequestValidator>();
builder.Services.AddHostedService<QueueCleanupWorker>();

builder.Services.AddScoped<CreatePatientRequestValidator>();
builder.Services.AddScoped<PatientDuplicateCheckRequestValidator>();
builder.Services.AddScoped<PatientQueryRequestValidator>();
builder.Services.AddScoped<UpdatePatientRequestValidator>();
builder.Services.AddScoped<PatientTimelineQueryRequestValidator>();
builder.Services.AddScoped<LoginRequestValidator>();
builder.Services.AddScoped<RegisterRequestValidator>();
builder.Services.AddScoped<VisitQueryRequestValidator>();
builder.Services.AddScoped<UpdateVisitDiagnosisRequestValidator>();
builder.Services.AddScoped<CreatePostTreatmentNoteRequestValidator>();
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
builder.Services.AddScoped<SelfCancellationEligibilityRequestValidator>();
builder.Services.AddScoped<CheckInRequestValidator>();
builder.Services.AddScoped<QueueQueryRequestValidator>();
builder.Services.AddScoped<UpdateQueueStatusRequestValidator>();
builder.Services.AddScoped<CallNextRequestValidator>();
builder.Services.AddScoped<CancelWaitingRequestValidator>();
builder.Services.AddScoped<QueueAbsenceRequestValidator>();


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
        opts.Events = new JwtBearerEvents
        {
            OnTokenValidated = AccessTokenVersionValidation.ValidateAsync
        };
    });

// ===== 7. Authorization Policies =====
builder.Services.AddAuthorization(opts => opts.AddApplicationPolicies());

// [CẦN XÁC NHẬN] Ngưỡng mặc định là 10 lần/phút cho đăng nhập và 5 lần/phút cho đăng ký.
builder.Services.AddRateLimiter(options => { AuthRateLimitPolicies.Configure(options, builder.Configuration); BookingRequestRateLimits.Configure(options); PatientActivationRateLimits.Configure(options); StaffPhoneChangeRateLimits.Configure(options); PatientPhoneChangeRateLimits.Configure(options); });
builder.Services.AddRateLimiter(PublicPrescriptionRateLimits.Configure);
builder.Services.AddRateLimiter(DentalImageRateLimits.Configure);

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
