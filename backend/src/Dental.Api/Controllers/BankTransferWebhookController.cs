using System.Globalization;
using Dental.Api.Policies;
using AppPolicies = Dental.Api.Policies.Policies;
using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Application.Features.MOD_BIL.Services;
using Dental.Application.Interfaces;
using Dental.Domain.Enums;
using Dental.Domain.Constants;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dental.Api.Controllers;

[ApiController]
public sealed class BankTransferWebhookController(BankTransferWebhookService service, IBankTransferRepository repository,
    ICurrentUser user, IWebHostEnvironment environment) : ControllerBase
{
    [HttpPost("api/webhooks/payment/bank-transfer")]
    [Authorize(AuthenticationSchemes = BankWebhookAuthenticationHandler.SchemeName)]
    public async Task<IActionResult> Receive(SePayWebhookRequest request, CancellationToken ct)
    {
        if (request.Id <= 0 || request.TransferType != "in") return BadRequest(new { success = false, message = "Chỉ ghi nhận giao dịch tiền vào hợp lệ." });
        var settings = await repository.GetBankSettingsAsync(ct);
        if (!settings.TryGetValue("Payment:AccountNumber", out var account) || string.IsNullOrWhiteSpace(account) || account != request.AccountNumber)
            return BadRequest(new { success = false, message = "Tài khoản nhận không khớp cấu hình phòng khám." });
        return await Record(new(request.Content, request.TransferAmount, request.ReferenceCode, request.Id), false, ct, true);
    }

    [HttpPost("api/webhooks/payment/bank-transfer/simulate")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = AppPolicies.CashPaymentWrite)]
    public Task<IActionResult> Simulate(BankTransferSimulationRequest request, CancellationToken ct)
    {
        if (!environment.IsDevelopment()) return Task.FromResult<IActionResult>(NotFound());
        if (BankTransferRules.InvoiceCode($"PKNK {request.InvoiceCode}") != request.InvoiceCode)
            return Task.FromResult<IActionResult>(BadRequest(new { message = "Mã hóa đơn không hợp lệ." }));
        return Record(new($"PKNK {request.InvoiceCode}", request.Amount, request.TransactionReference), true, ct);
    }

    private async Task<IActionResult> Record(BankTransferRequest request, bool simulated, CancellationToken ct, bool providerResponse = false)
    {
        if (user.UserId is not int actorId) return Unauthorized();
        var result = await service.ReceiveAsync(request, actorId, simulated, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        if (result.IsSuccess) return providerResponse ? Ok(new { success = true, payment = result.Value }) : Ok(result.Value);
        return StatusCode(result.Error.Code switch { "GEN_001" => 404, "GEN_002" => 403, "GEN_003" or "BIL_032" => 400, _ => 409 },
            new { code = result.Error.Code, message = result.Error.Message });
    }

    [HttpGet("api/invoices/{invoiceId:int}/bank-transfer-info")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = AppPolicies.CashPaymentWrite)]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Info(int invoiceId, CancellationToken ct)
    {
        if (invoiceId <= 0) return BadRequest();
        var invoice = await repository.GetInvoiceAsync(invoiceId, ct);
        if (invoice is null) return NotFound();
        if (invoice.Status is not (InvoiceStatus.PendingPayment or InvoiceStatus.PartiallyPaid) || !invoice.Visit.IsLocked ||
            invoice.Visit.Status != VisitStatuses.Completed || invoice.TotalAmount <= invoice.PaidAmount)
            return Conflict(new { message = "Hóa đơn chưa sẵn sàng hoặc không còn nợ." });
        var content = $"PKNK {invoice.InvoiceCode}";
        var settings = await repository.GetBankSettingsAsync(ct);
        settings.TryGetValue("Payment:BankBin", out var bank); settings.TryGetValue("Payment:AccountNumber", out var account);
        settings.TryGetValue("Payment:AccountName", out var name);
        var ready = bank is not null && System.Text.RegularExpressions.Regex.IsMatch(bank, "^[0-9]{6}$") &&
            account is not null && System.Text.RegularExpressions.Regex.IsMatch(account, "^[0-9]{1,30}$") && !string.IsNullOrWhiteSpace(name);
        var remaining = invoice.TotalAmount - invoice.PaidAmount;
        var url = ready ? $"https://img.vietqr.io/image/{bank}-{account}-compact2.png?amount={remaining.ToString("0", CultureInfo.InvariantCulture)}&addInfo={Uri.EscapeDataString(content)}&accountName={Uri.EscapeDataString(name!)}" : null;
        return Ok(new BankTransferInfo(invoice.Id, invoice.InvoiceCode, remaining, content, url, ready ? name : null,
            environment.IsDevelopment(), ready ? null : "Chưa cấu hình tài khoản ngân hàng phòng khám. Không thể hiển thị VietQR thực."));
    }
    [HttpGet("api/invoices/{invoiceId:int}")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = AppPolicies.CashPaymentWrite)]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> State(int invoiceId, CancellationToken ct)
    {
        if (invoiceId <= 0) return BadRequest();
        var invoice = await repository.GetInvoiceAsync(invoiceId, ct);
        return invoice is null ? NotFound() : Ok(new InvoicePaymentState(invoice.Id, invoice.InvoiceCode,
            invoice.Status, invoice.PaidAmount, invoice.TotalAmount - invoice.PaidAmount));
    }
}
