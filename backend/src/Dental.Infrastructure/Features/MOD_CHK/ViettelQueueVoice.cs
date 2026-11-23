using System.Globalization;
using System.Net.Http.Json;
using System.Threading.RateLimiting;
using Dental.Application.Features.MOD_CHK;
using Microsoft.Extensions.Configuration;

namespace Dental.Infrastructure.Features.MOD_CHK;

public sealed class ViettelQueueVoice(IHttpClientFactory clients, IConfiguration configuration) : IQueueVoiceSynthesizer, IDisposable
{
    public const string ClientName = nameof(ViettelQueueVoice);
    private const int MaxAudioBytes = 256 * 1024;
    private static readonly Uri ProviderEndpoint = new("https://viettelai.vn/tts/speech_synthesis");
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(3);

    private readonly FixedWindowRateLimiter _providerLimit = new(new FixedWindowRateLimiterOptions
    {
        PermitLimit = 60,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0,
        AutoReplenishment = true
    });

    public async Task<byte[]?> SynthesizeAsync(string publicName, TimeSpan remaining, CancellationToken ct)
    {
        var token = configuration["ViettelTts:Token"]?.Trim();
        if (string.IsNullOrEmpty(token) || remaining <= TimeSpan.Zero)
            return null;

        using var lease = _providerLimit.AttemptAcquire();
        if (!lease.IsAcquired)
            return null;

        var voice = configuration["ViettelTts:Voice"]?.Trim();
        var speedText = configuration["ViettelTts:Speed"];
        var speed = double.TryParse(speedText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            && parsed is >= 0.8 and <= 1.2 ? parsed : 0.95;

        var name = string.Join(" ", publicName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(part => !part.Contains('*')));
        var announcement = name.Length == 0 || name.Equals("Bệnh nhân", StringComparison.OrdinalIgnoreCase)
            ? "Mời bệnh nhân."
            : $"Mời bệnh nhân {name}.";

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(remaining < RequestTimeout ? remaining : RequestTimeout);
        using var request = new HttpRequestMessage(HttpMethod.Post, ProviderEndpoint);
        request.Content = JsonContent.Create(new
        {
            text = announcement,
            voice = string.IsNullOrEmpty(voice) ? "hcm-diemmy" : voice,
            speed,
            tts_return_option = 3,
            token,
            without_filter = false
        });
        request.Headers.Accept.ParseAdd("*/*");

        try
        {
            using var client = clients.CreateClient(ClientName);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength is < 4 or > MaxAudioBytes)
                return null;

            await using var audio = await response.Content.ReadAsStreamAsync(deadline.Token);
            using var result = new MemoryStream();
            var buffer = new byte[8192];

            while (true)
            {
                var count = await audio.ReadAsync(buffer, deadline.Token);
                if (count == 0)
                    break;
                if (result.Length + count > MaxAudioBytes)
                    return null;
                result.Write(buffer, 0, count);
            }

            if (!LooksLikeMp3(result.GetBuffer().AsSpan(0, (int)result.Length)))
                return null;

            return result.ToArray();
        }
        catch (HttpRequestException) { return null; }
        catch (IOException) { return null; }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return null; }
    }

    private static bool LooksLikeMp3(ReadOnlySpan<byte> bytes)
        => (bytes.Length >= 10 && bytes.StartsWith("ID3"u8)) ||
            (bytes.Length >= 4 && bytes[0] == 0xff && (bytes[1] & 0xe0) == 0xe0);

    public void Dispose() => _providerLimit.Dispose();
}
