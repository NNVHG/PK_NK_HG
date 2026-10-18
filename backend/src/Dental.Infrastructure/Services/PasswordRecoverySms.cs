using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Dental.Application.Features.MOD_AUTH;
using Microsoft.Extensions.Configuration;

namespace Dental.Infrastructure.Services;

/// <summary>Twilio REST adapter, no SDK. Credentials only from server configuration; SMS bodies never enter application logs.</summary>
public sealed class PasswordRecoverySms(HttpClient client, IConfiguration configuration) : IPasswordRecoverySms
{
    private string Value(string key) => configuration["PasswordRecovery:Sms:" + key]?.Trim() ?? "";
    public bool IsConfigured => configuration.GetValue<bool>("PasswordRecovery:Sms:Enabled") &&
        Regex.IsMatch(Value("AccountSid"), "^AC[0-9a-fA-F]{32}$") && Value("AuthToken").Length >= 32 &&
        Regex.IsMatch(Value("From"), @"^\+[1-9][0-9]{7,14}$");

    public async Task<bool> SendAsync(string phone, string code, CancellationToken ct)
    {
        if (!IsConfigured) return false;
        using var request = new HttpRequestMessage(HttpMethod.Post,
            "https://api.twilio.com/2010-04-01/Accounts/" + Value("AccountSid") + "/Messages.json");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes(Value("AccountSid") + ":" + Value("AuthToken"))));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["To"] = "+84" + phone[1..], ["From"] = Value("From"),
            ["Body"] = "Ma khoi phuc mat khau phong kham: " + code + ". Hieu luc 5 phut. Khong chia se ma nay."
        });
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException) { return false; }
        catch (OperationCanceledException) { return false; }
    }
}
