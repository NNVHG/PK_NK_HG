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
    public static async Task RunAsync(DbContextOptions<DentalDbContext> options, int cashierId, int patientId, Action<bool, string> check)
    {
        // Dedicated test host validates ephemeral JWTs only. Never read clinic JWT keys/demo credentials.
        var signingKey = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(64));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddControllers().AddApplicationPart(typeof(CashPaymentsController).Assembly);
        builder.Services.AddScoped(_ => new DentalDbContext(options));
        builder.Services.AddScoped<ICashPaymentRepository, CashPaymentRepository>();
        builder.Services.AddScoped<CashPaymentService>();
        builder.Services.AddScoped<CashPaymentRequestValidator>();
        builder.Services.AddHttpContextAccessor(); builder.Services.AddScoped<ICurrentUser, CurrentUser>();
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(jwt =>
            jwt.TokenValidationParameters = new TokenValidationParameters { ValidateIssuerSigningKey = true,
                IssuerSigningKey = signingKey, ValidateIssuer = true, ValidIssuer = "cash-test",
                ValidateAudience = true, ValidAudience = "cash-test", ValidateLifetime = true, ClockSkew = TimeSpan.Zero });
        builder.Services.AddAuthorization(policies => policies.AddApplicationPolicies());
        await using var app = builder.Build();
        app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
        await app.StartAsync();
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
            await using var db = new DentalDbContext(options);
            var invoice = new Invoice { Visit = new Visit { PatientId = patientId, CreatedByUserId = cashierId,
                Status = VisitStatuses.Completed, IsLocked = true }, CreatedByUserId = cashierId,
                InvoiceCode = "TEST-CASH-HTTP", TotalAmount = 100000, Status = InvoiceStatus.PendingPayment, CreatedAt = DateTime.UtcNow };
            db.Invoices.Add(invoice); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var path = $"/api/invoices/{invoice.Id}/payments";
            async Task<HttpResponseMessage> Post(CashPaymentRequest body) => await client.PostAsJsonAsync(path, body);
            void Authenticate(string role)
            {
                var token = new JwtSecurityToken("cash-test", "cash-test",
                    [new Claim(ClaimTypes.NameIdentifier, cashierId.ToString()), new Claim(ClaimTypes.Role, role)],
                    expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
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
        }
        finally { await app.StopAsync(); }
    }
}
