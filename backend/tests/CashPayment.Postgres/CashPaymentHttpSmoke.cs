using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using Dental.Api.Controllers;
using Dental.Api.Policies;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Services;
using Dental.Application.Features.MOD_BIL.Validators;
using Microsoft.AspNetCore.Authentication;
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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

internal static class CashPaymentHttpSmoke
{
    public static async Task RunAsync(DbContextOptions<DentalDbContext> options, int cashierId, int patientId, Action<bool, string> check,
        bool queueCallNextOnly = false, bool invoiceDiscountsOnly = false, bool serviceCatalogOnly = false, bool queueImagingOnly = false, bool queueCallingOnly = false, bool queueAbsenceOnly = false, bool queueCleanupOnly = false, bool chairCatalogOnly = false, bool staffWorkspacesOnly = false, bool queueLocationsOnly = false, bool visitCorrectionsOnly = false, bool physicalChairsOnly = false, bool visitCancellationOnly = false, bool postTreatmentNotesOnly = false, bool diagnosisCatalogOnly = false, bool revenueReportOnly = false, bool medicineCatalogOnly = false, bool prescriptionOnly = false, bool invoiceRefundsOnly = false, bool invoiceDraftCancellationOnly = false, bool medicalSupplyCatalogOnly = false)
    {
        // Dedicated test host validates ephemeral JWTs only. Never read clinic JWT keys/demo credentials.
        var signingKey = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(64));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], EnvironmentName = "Development" });
        var webhookKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        builder.Configuration["Payment:WebhookSecretKey"] = webhookKey;
        builder.Configuration["Payment:WebhookActorUserId"] = cashierId.ToString();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddControllers().AddApplicationPart(typeof(CashPaymentsController).Assembly);
        builder.Services.AddScoped(_ => new DentalDbContext(options));
        builder.Services.AddScoped<Dental.Application.Features.Staff.Services.StaffService>();
        builder.Services.AddScoped<Dental.Application.Features.Staff.Validators.StaffQueryRequestValidator>();
        builder.Services.AddScoped<Dental.Application.Features.Staff.Validators.CreateStaffRequestValidator>();
        builder.Services.AddScoped<Dental.Application.Features.Staff.Validators.UpdateStaffRequestValidator>();
        builder.Services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        builder.Services.AddScoped<IClinicPrintSettingsRepository, ClinicPrintSettingsRepository>();
        builder.Services.AddScoped<Dental.Application.Features.MOD_MST.Services.ClinicPrintSettingsService>();
        builder.Services.AddScoped<IDoctorSignatureRepository, DoctorSignatureRepository>();
        builder.Services.AddScoped<Dental.Application.Features.MOD_AUTH.DoctorSignatureService>();
        builder.Services.AddScoped<IPrescriptionRepository, PrescriptionRepository>();
        builder.Services.AddScoped<IPrescriptionHandoverRepository, PrescriptionHandoverRepository>();
        builder.Services.AddScoped<Dental.Application.Features.MOD_RX.Services.PrescriptionHandoverService>();
        builder.Services.AddScoped<IPublicPrescriptionRepository, PublicPrescriptionRepository>();
        builder.Services.AddScoped<Dental.Application.Features.MOD_RX.PublicPrescriptionService>();
        builder.Services.AddSingleton<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>(new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider());
        builder.Services.AddSingleton<IPublicPrescriptionAccess, Dental.Infrastructure.Services.PublicPrescriptionAccess>();
        builder.Services.AddRateLimiter(o => { o.RejectionStatusCode = 429; Dental.Api.RateLimiting.PublicPrescriptionRateLimits.Configure(o); });
        builder.Services.AddScoped<IPatientCitizenIdRepository, PatientCitizenIdRepository>();
        builder.Services.AddScoped<IQrPatientIntakeRepository, QrPatientIntakeRepository>();
        builder.Services.AddScoped<Dental.Application.Features.MOD_PAT.QrPatientIntakeService>();
        builder.Services.AddScoped<Dental.Application.Features.MOD_PAT.PatientCitizenIdService>();
        builder.Services.AddScoped<Dental.Application.Features.MOD_RX.Services.PrescriptionService>();
        builder.Services.AddScoped<IMedicineCatalogRepository, MedicineCatalogRepository>();
        builder.Services.AddScoped<Dental.Application.Features.MOD_MST.Services.MedicineCatalogService>();
        builder.Services.AddScoped<IRevenueReportRepository, RevenueReportRepository>();
        builder.Services.AddScoped<Dental.Application.Features.MOD_RPT.Services.RevenueReportService>();
        builder.Services.AddScoped<IMedicalSupplyCatalogRepository, MedicalSupplyCatalogRepository>();
        builder.Services.AddScoped<Dental.Application.Features.MOD_MST.Services.MedicalSupplyCatalogService>();
        builder.Services.AddScoped<ICashPaymentRepository, CashPaymentRepository>();
        builder.Services.AddScoped<CashPaymentService>();
        builder.Services.AddScoped<CashPaymentRequestValidator>();
        builder.Services.AddScoped<IInvoiceDiscountRepository, InvoiceDiscountRepository>();
        builder.Services.AddScoped<IInvoiceRefundRepository, InvoiceRefundRepository>();
        builder.Services.AddScoped<IInvoiceRefundProposalRepository, InvoiceRefundProposalRepository>();
        builder.Services.AddScoped<Dental.Application.Features.MOD_BIL.Services.InvoiceRefundProposalService>();
        builder.Services.AddScoped<Dental.Application.Features.MOD_BIL.Services.InvoiceRefundService>();
        builder.Services.AddScoped<InvoiceDiscountService>();
        builder.Services.AddScoped<DiscountPresetRequestValidator>();
        builder.Services.AddScoped<ApplyInvoiceDiscountRequestValidator>();
        builder.Services.AddScoped<IReceiptRepository, ReceiptRepository>();
        builder.Services.AddScoped<ReceiptPrintService>();
        builder.Services.AddScoped<ReceiptRequestValidator>();
        builder.Services.AddScoped<IFdiServiceDeletionRepository, FdiServiceDeletionRepository>();
        builder.Services.AddScoped<Dental.Application.Features.MOD_FDI.Services.FdiServiceDeletionService>();
        builder.Services.AddScoped<Dental.Application.Features.MOD_FDI.Validators.DeleteVisitServiceRequestValidator>();
        builder.Services.AddScoped<IPatientRepository, PatientRepository>();
        builder.Services.AddScoped<IToothHistoryRepository, ToothHistoryRepository>();
        builder.Services.AddScoped<Dental.Application.Features.MOD_FDI.Services.ToothHistoryService>();
        builder.Services.AddScoped<Dental.Application.Features.MOD_FDI.Validators.ToothHistoryRequestValidator>();
        builder.Services.AddScoped<IVisitRepository, VisitRepository>();
        builder.Services.AddScoped<IServiceCatalogRepository, ServiceCatalogRepository>();
        builder.Services.AddScoped<Dental.Application.Features.ServiceCatalog.Services.ServiceCatalogService>();
        builder.Services.AddScoped<Dental.Application.Features.ServiceCatalog.Validators.ServiceCatalogQueryRequestValidator>();
        builder.Services.AddScoped<Dental.Application.Features.ServiceCatalog.Validators.CreateDentalServiceRequestValidator>();
        builder.Services.AddScoped<Dental.Application.Features.ServiceCatalog.Validators.UpdateDentalServiceRequestValidator>();
        builder.Services.AddScoped<Dental.Application.Features.ServiceCatalog.Validators.CreateServicePriceRequestValidator>();
        builder.Services.AddScoped<Dental.Application.Features.ServiceCatalog.Validators.ServiceIdRequestValidator>();
        builder.Services.AddScoped<IVisitServiceRepository, VisitServiceRepository>();
        builder.Services.AddScoped<Dental.Application.Features.MOD_FDI.Services.FdiServiceAssignmentService>();
        builder.Services.AddScoped<Dental.Application.Features.MOD_FDI.Validators.AssignServicesRequestValidator>();

        builder.Services.AddScoped<IToothConditionRepository, ToothConditionRepository>();
        builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
        builder.Services.AddScoped<IAuditLogger, AuditLogger>();
        builder.Services.AddScoped<Dental.Application.Features.MOD_FDI.Services.ToothConditionService>();
        builder.Services.AddScoped<Dental.Application.Features.MOD_FDI.Validators.ToothConditionRequestValidator>();
        builder.Services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        builder.Services.AddScoped<IQueueRepository, QueueRepository>();
        builder.Services.AddScoped<IDiagnosisCatalogRepository, DiagnosisCatalogRepository>();
        builder.Services.AddScoped<Dental.Application.Features.MOD_MST.Services.DiagnosisCatalogService>();
        builder.Services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        builder.Services.AddScoped<Dental.Application.Features.MOD_RX.IMedicineStockRepository, MedicineStockRepository>();
        builder.Services.AddScoped<Dental.Application.Features.MOD_RX.MedicineStockService>();
        builder.Services.AddScoped<IPostTreatmentNoteRepository, PostTreatmentNoteRepository>();
        builder.Services.AddScoped<Dental.Application.Features.Visits.Services.PostTreatmentNoteService>();
        builder.Services.AddScoped<Dental.Application.Features.Visits.Validators.CreatePostTreatmentNoteRequestValidator>();
        builder.Services.AddScoped<IInvoiceDraftGenerator, InvoiceDraftService>();
        builder.Services.AddScoped<InvoiceDraftService>();
        builder.Services.AddScoped<InvoiceVisitRequestValidator>();
        builder.Services.AddScoped<IVisitReopenRepository, VisitReopenRepository>();
        builder.Services.AddScoped<VisitReopenService>();
        builder.Services.AddScoped<UnlockVisitRequestValidator>();
        builder.Services.AddScoped<Dental.Application.Features.Visits.Services.VisitService>();
        builder.Services.AddScoped<Dental.Application.Features.Visits.Validators.VisitQueryRequestValidator>();
        builder.Services.AddScoped<Dental.Application.Features.Visits.Validators.UpdateVisitDiagnosisRequestValidator>();
        builder.Services.AddScoped<IVisitChairCoordinator, Dental.Application.Features.MOD_MST.Services.VisitChairService>();
        builder.Services.AddScoped<Dental.Application.Features.Queue.Services.QueueService>();
        builder.Services.AddScoped<Dental.Application.Features.Queue.Validators.CheckInRequestValidator>();
        builder.Services.AddScoped<Dental.Application.Features.Queue.Validators.CheckInDebtRequestValidator>();
        builder.Services.AddScoped<Dental.Application.Features.Queue.Validators.QueueQueryRequestValidator>();
        builder.Services.AddScoped<Dental.Application.Features.Queue.Validators.UpdateQueueStatusRequestValidator>();
        builder.Services.AddScoped<Dental.Application.Features.Queue.Validators.CallNextRequestValidator>();
        builder.Services.AddScoped<Dental.Application.Features.Queue.Validators.CancelWaitingRequestValidator>();
        builder.Services.AddScoped<Dental.Application.Features.Queue.Validators.QueueAbsenceRequestValidator>();
        builder.Services.AddScoped<Dental.Application.Features.Queue.Validators.TransferDentistRequestValidator>();
        builder.Services.AddScoped<Dental.Application.Features.Queue.Validators.QueueCleanupRequestValidator>();
        builder.Services.AddScoped<Dental.Application.Features.Queue.Services.QueueCleanupService>();
        builder.Services.AddScoped<IDentalChairRepository, DentalChairRepository>();
        builder.Services.AddScoped<IStaffWorkspaceRepository, StaffWorkspaceRepository>();
        builder.Services.AddScoped<Dental.Application.Features.MOD_MST.Services.StaffWorkspaceService>();
        builder.Services.AddScoped<Dental.Application.Features.MOD_MST.Services.DentalChairService>();
        builder.Services.AddScoped<IAppointmentReminderRepository, AppointmentReminderRepository>();
        builder.Services.AddSingleton<AppointmentReminderWorkerStatus>();
        builder.Services.AddSingleton<IAppointmentReminderWorkerStatus>(sp => sp.GetRequiredService<AppointmentReminderWorkerStatus>());
        builder.Services.AddScoped<Dental.Application.Features.Appointments.Services.AppointmentReminderService>();
        builder.Services.AddScoped<Dental.Application.Features.Appointments.Validators.SimulateAppointmentRemindersValidator>();
        builder.Services.AddScoped<Dental.Application.Features.Appointments.Validators.ReminderLogQueryValidator>();
        builder.Services.AddScoped<IUserRepository, UserRepository>();
        builder.Services.AddScoped<Dental.Application.Features.Appointments.Services.AppointmentService>();
        builder.Services.AddScoped<Dental.Application.Features.Appointments.Validators.CreateAppointmentRequestValidator>();
        builder.Services.AddScoped<Dental.Application.Features.Appointments.Validators.AppointmentQueryRequestValidator>();
        builder.Services.AddScoped<Dental.Application.Features.Appointments.Validators.RescheduleAppointmentRequestValidator>();
        builder.Services.AddScoped<Dental.Application.Features.Appointments.Validators.CancelAppointmentRequestValidator>();
        builder.Services.AddScoped<Dental.Application.Features.Appointments.Validators.SelfCancellationEligibilityRequestValidator>();
        builder.Services.AddScoped<Dental.Application.Features.Patients.Services.PatientsService>();
        builder.Services.AddScoped<Dental.Application.Features.Patients.Validators.CreatePatientRequestValidator>();
        builder.Services.AddScoped<Dental.Application.Features.Patients.Validators.PatientDuplicateCheckRequestValidator>();
        builder.Services.AddScoped<Dental.Application.Features.Patients.Validators.PatientQueryRequestValidator>();
        builder.Services.AddScoped<Dental.Application.Features.Patients.Validators.UpdatePatientRequestValidator>();
        builder.Services.AddSingleton(SmokeTestTimeProvider.Instance);
        builder.Services.AddSingleton<Dental.Application.Common.VietnamClock>();
        builder.Services.AddScoped<IBankTransferRepository, BankTransferRepository>();
        builder.Services.AddScoped<BankTransferWebhookService>();
        builder.Services.AddScoped<BankTransferRequestValidator>();
        builder.Services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, BankWebhookAuthenticationHandler>(BankWebhookAuthenticationHandler.SchemeName, _ => { });
        builder.Services.AddHttpContextAccessor(); builder.Services.AddScoped<ICurrentUser, CurrentUser>();
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(jwt =>
            jwt.TokenValidationParameters = new TokenValidationParameters { ValidateIssuerSigningKey = true,
                IssuerSigningKey = signingKey, ValidateIssuer = true, ValidIssuer = "cash-test",
                ValidateAudience = true, ValidAudience = "cash-test", ValidateLifetime = true, ClockSkew = TimeSpan.Zero });
        builder.Services.AddAuthorization(policies => policies.AddApplicationPolicies());
        await using var app = builder.Build();
        app.UseRateLimiter(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
        await app.StartAsync();
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
            await using var db = new DentalDbContext(options);
            var invoice = new Invoice { Visit = new Visit { PatientId = patientId, CreatedByUserId = cashierId,
                Status = VisitStatuses.Completed, IsLocked = true }, CreatedByUserId = cashierId,
                InvoiceCode = "TEST-CASH-HTTP", TotalAmount = 100000, Status = InvoiceStatus.PendingPayment, CreatedAt = DateTime.UtcNow,
                Items = [new InvoiceItem { Code = "TEST-SERVICE", Name = "Synthetic receipt service", Quantity = 2, UnitPrice = 50000, TotalAmount = 100000 }] };
            db.Invoices.Add(invoice); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var path = $"/api/invoices/{invoice.Id}/payments";
            async Task<HttpResponseMessage> Post(CashPaymentRequest body) => await client.PostAsJsonAsync(path, body);
            void AuthenticateActor(string role, int actorUserId)
            {
                var token = new JwtSecurityToken("cash-test", "cash-test",
                    [new Claim(ClaimTypes.NameIdentifier, actorUserId.ToString()), new Claim(ClaimTypes.Role, role)],
                    expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
            }
            void Authenticate(string role) => AuthenticateActor(role, cashierId);
            if (invoiceDraftCancellationOnly) { await InvoiceDraftCancellationHttpSmoke.RunAsync(client, options, cashierId, patientId, AuthenticateActor, check); return; }
            if (invoiceRefundsOnly) { await InvoiceRefundHttpSmoke.RunAsync(client,options,cashierId,AuthenticateActor,check); return; }
            if (prescriptionOnly)
            {
                if (Environment.GetCommandLineArgs().Contains("--patient-prescription-history-only"))
                    await PatientPrescriptionHistoryHttpSmoke.RunAsync(client, options, cashierId, AuthenticateActor, check);
                else if (Environment.GetCommandLineArgs().Contains("--prescription-handover-only"))
                    await PrescriptionHandoverHttpSmoke.RunAsync(client, options, cashierId, AuthenticateActor, check);
                else if (Environment.GetCommandLineArgs().Contains("--prescription-print-only"))
                    await PrescriptionPrintHttpSmoke.RunAsync(client, options, cashierId, AuthenticateActor, check);
                else if (Environment.GetCommandLineArgs().Contains("--prescription-replacement-only"))
                    await PrescriptionReplacementHttpSmoke.RunAsync(client, options, cashierId, AuthenticateActor, check);
                else if (Environment.GetCommandLineArgs().Contains("--doctor-signature-only"))
                    await DoctorSignatureHttpSmoke.RunAsync(client, options, AuthenticateActor, check);
                else if (Environment.GetCommandLineArgs().Contains("--public-prescription-only"))
                    await PublicPrescriptionHttpSmoke.RunAsync(client, options, cashierId, AuthenticateActor, check);
                else if (Environment.GetCommandLineArgs().Contains("--patient-citizen-id-only"))
                    {
                    await PatientCitizenIdHttpSmoke.RunAsync(client, options, cashierId, AuthenticateActor, check);
                    await QrPatientIntakeHttpSmoke.RunAsync(client, options, cashierId, AuthenticateActor, check);
                    }
                else await PrescriptionHttpSmoke.RunAsync(client, options, cashierId, AuthenticateActor, check);
                return;
            }
            if (medicalSupplyCatalogOnly)
            {
                await MedicalSupplyCatalogHttpSmoke.RunAsync(client, options, cashierId, Authenticate, check);
                return;
            }
            if (medicineCatalogOnly)
            {
                await MedicineCatalogHttpSmoke.RunAsync(client, options, cashierId, Authenticate, check);
                return;
            }
            if (revenueReportOnly)
            {
                await RevenueReportHttpSmoke.RunAsync(client, options, cashierId, patientId, Authenticate, check);
                return;
            }
            if (diagnosisCatalogOnly)
            {
                await DiagnosisCatalogHttpSmoke.RunAsync(client, options, cashierId, Authenticate, check);
                return;
            }
            if (postTreatmentNotesOnly)
            {
                await PostTreatmentNoteHttpSmoke.RunAsync(client, options, cashierId, AuthenticateActor, check);
                return;
            }
            if (visitCancellationOnly)
            {
                await VisitCancellationHttpSmoke.RunAsync(client, options, cashierId, AuthenticateActor, check);
                return;
            }
            if (physicalChairsOnly)
            {
                await PhysicalChairHttpSmoke.RunAsync(client, options, cashierId, AuthenticateActor, check);
                return;
            }
            if (visitCorrectionsOnly)
            {
                await VisitCorrectionHttpSmoke.RunAsync(client, options, cashierId, patientId, AuthenticateActor, check);
                return;
            }
            if (queueLocationsOnly)
            {
                await QueueLocationHttpSmoke.RunAsync(client, options, cashierId, patientId, AuthenticateActor, check);
                return;
            }
            if (staffWorkspacesOnly)
            {
                await StaffWorkspaceHttpSmoke.RunAsync(client, options, cashierId, AuthenticateActor, check);
                return;
            }
            if (chairCatalogOnly)
            {
                await DentalChairHttpSmoke.RunAsync(client, options, cashierId, Authenticate, check);
                return;
            }
            if (queueCleanupOnly)
            {
                await QueueCleanupHttpSmoke.RunAsync(client, options, cashierId, Authenticate, AuthenticateActor, check);
                return;
            }
            if (queueAbsenceOnly)
            {
                await QueueAbsenceHttpSmoke.RunAsync(client, options, cashierId, Authenticate, AuthenticateActor, check);
                return;
            }
            if (queueCallingOnly)
            {
                await QueueCallingHttpSmoke.RunAsync(client, options, cashierId, Authenticate, AuthenticateActor, check);
                return;
            }
            if (queueImagingOnly)
            {
                await QueueImagingHttpSmoke.RunAsync(client, options, cashierId, Authenticate, AuthenticateActor, check);
                return;
            }
            if (serviceCatalogOnly)
            {
                await ServiceCatalogHttpSmoke.RunAsync(client, options, Authenticate, check);
                return;
            }
            if (invoiceDiscountsOnly)
            {
                await InvoiceDiscountHttpSmoke.RunAsync(client, options, cashierId, patientId, Authenticate, check);
                return;
            }
            if (queueCallNextOnly)
            {
                await DailyQueueHttpSmoke.RunAsync(client, options, cashierId, Authenticate, check);
                await QueueCallNextHttpSmoke.RunAsync(client, options, cashierId, Authenticate, AuthenticateActor, check);
                await QueueWaitingCancellationHttpSmoke.RunAsync(client, options, cashierId, Authenticate, check);
            await QueueSelectedStartHttpSmoke.RunAsync(client, options, cashierId, Authenticate, AuthenticateActor, check);
            await FdiServiceSearchHttpSmoke.RunAsync(client, options, cashierId, patientId, Authenticate, check);
                return;
            }
            using (var response = await client.GetAsync(path)) check(response.StatusCode == HttpStatusCode.Unauthorized, "HTTP anonymous 401");
            foreach (var role in new[] { RoleCodes.Patient, RoleCodes.Dentist, RoleCodes.Assistant })
            {
                Authenticate(role);
                using var response = await Post(new(1, 1, Guid.NewGuid()));
                check(response.StatusCode == HttpStatusCode.Forbidden, $"HTTP {role} payment 403");
                using var historyResponse = await client.GetAsync(path);
                check(historyResponse.StatusCode == HttpStatusCode.Forbidden, $"HTTP {role} payment history 403");
            }
            Authenticate(RoleCodes.Receptionist);
            using (var response = await client.GetAsync("/api/invoices/2147483647/payments")) check(response.StatusCode == HttpStatusCode.NotFound, "HTTP history missing invoice 404");
            using (var response = await Post(new(10, 9, Guid.NewGuid()))) check(response.StatusCode == HttpStatusCode.BadRequest, "HTTP tendered validation 400");
            using (var response = await Post(new(100001, 110000, Guid.NewGuid()))) check(response.StatusCode == HttpStatusCode.BadRequest, "HTTP over-debt 400");
            var first = new CashPaymentRequest(40000, 50000, Guid.NewGuid());
            CashPaymentResponse receipt;
            using (var response = await Post(first))
            {
                check(response.StatusCode == HttpStatusCode.OK, "HTTP partial cash 200");
                receipt = (await response.Content.ReadFromJsonAsync<CashPaymentResponse>())!;
                check(receipt.Amount == 40000 && receipt.ChangeAmount == 10000 && receipt.CashierId == cashierId, "HTTP receipt amount/change/JWT cashier");
            }
            using (var response = await Post(first)) check((await response.Content.ReadFromJsonAsync<CashPaymentResponse>())!.Id == receipt.Id, "HTTP replay same receipt");
            using (var response = await client.GetAsync(path + $"/{receipt.Id}/receipt"))
            {
                var preview = await response.Content.ReadFromJsonAsync<ReceiptPrintDto>();
                check(response.StatusCode == HttpStatusCode.OK && preview?.RemainingAmount == 60000 && preview.PaidAmount == 40000 && preview.PrintCount == 0,
                    "Receipt partial payment preview preserves outstanding debt");
            }
            Authenticate(RoleCodes.Admin);
            using (var response = await Post(new(60000, 100000, Guid.NewGuid()))) check(response.StatusCode == HttpStatusCode.OK, "HTTP Admin full cash 200");
            using (var response = await client.GetAsync(path))
            {
                check(response.StatusCode == HttpStatusCode.OK && (await response.Content.ReadFromJsonAsync<CashPaymentResponse[]>())!.Length == 2, "HTTP payment history two receipts");
                check(response.Headers.CacheControl?.NoStore == true, "HTTP history Cache-Control no-store");
            }
            var paid = await db.Invoices.AsNoTracking().SingleAsync(x => x.Id == invoice.Id);
            check(paid.PaidAmount == 100000 && paid.Status == InvoiceStatus.Paid, "HTTP persisted Paid balance");
            check(await db.AuditLogs.CountAsync(x => x.EntityId == invoice.Id && x.Action == "MOD_BIL_CASH_RECEIVED") == 2, "HTTP committed audit count");
            await ReceiptHttpSmoke.RunAsync(client, options, invoice.Id, receipt.Id, Authenticate, check);
            await FdiServiceDeletionHttpSmoke.RunAsync(client, options, cashierId, patientId, Authenticate, check);
            await ToothHistoryHttpSmoke.RunAsync(client, options, cashierId, patientId, Authenticate, check);
            await ToothClinicalNoteHttpSmoke.RunAsync(client, options, cashierId, patientId, Authenticate, check);
            await CounterBookingHttpSmoke.RunAsync(client, options, cashierId, Authenticate, check);
            await DailyQueueHttpSmoke.RunAsync(client, options, cashierId, Authenticate, check);
            await QueueCallNextHttpSmoke.RunAsync(client, options, cashierId, Authenticate, AuthenticateActor, check);
            await QueueWaitingCancellationHttpSmoke.RunAsync(client, options, cashierId, Authenticate, check);
            await QueueSelectedStartHttpSmoke.RunAsync(client, options, cashierId, Authenticate, AuthenticateActor, check);
            await FdiServiceSearchHttpSmoke.RunAsync(client, options, cashierId, patientId, Authenticate, check);
            await AppointmentCancelHttpSmoke.RunAsync(client, options, cashierId, Authenticate, check);
            await PatientAppointmentReadHttpSmoke.RunAsync(client, options, cashierId, Authenticate, check);
            await PatientSelfCancellationHttpSmoke.RunAsync(client, options, cashierId, Authenticate, check);
            await DeskReschedulingHttpSmoke.RunAsync(client, options, cashierId, Authenticate, check);
            await PatientSelfReschedulingHttpSmoke.RunAsync(client, options, cashierId, Authenticate, check);
            await DentistBookingHttpSmoke.RunAsync(client, options, cashierId, Authenticate, check);
            await AppointmentReminderHttpSmoke.RunAsync(client, options, cashierId, Authenticate, check);
            await AutomaticReminderHttpSmoke.RunAsync(client, options, cashierId, Authenticate, app.Services.GetRequiredService<AppointmentReminderWorkerStatus>(), check);
            await BankTransferHttpSmoke.RunAsync(client, options, cashierId, patientId, webhookKey, Authenticate, check);
        }
        finally { await app.StopAsync(); }
    }
}
