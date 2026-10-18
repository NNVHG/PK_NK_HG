using System.Threading.Channels;
using Dental.Application.Features.MOD_AUTH;
using Microsoft.Extensions.Hosting;

namespace Dental.Infrastructure.Services;

/// <summary>Bounded, single-clinic SMS dispatch; no provider call or delivery status is exposed by the public request.</summary>
public sealed class PasswordRecoveryDelivery(IPasswordRecoverySms sms, IPasswordRecoveryCodes codes)
    : BackgroundService, IPasswordRecoveryDelivery
{
    private sealed record Delivery(string Phone, RecoveryCode Code);
    private readonly Channel<Delivery> queue = Channel.CreateBounded<Delivery>(new BoundedChannelOptions(64)
        { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    public bool IsConfigured => sms.IsConfigured;
    public bool Enqueue(string phone, RecoveryCode code) => queue.Writer.TryWrite(new(phone, code));
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in queue.Reader.ReadAllAsync(stoppingToken))
        {
            // A delayed or superseded message must not send a stale recovery code.
            if (!codes.IsCurrent(item.Phone, item.Code.ChallengeId)) continue;
            try
            {
                if (!await sms.SendAsync(item.Phone, item.Code.Code, stoppingToken)) codes.Revoke(item.Phone, item.Code.ChallengeId);
            }
            catch (Exception) when (!stoppingToken.IsCancellationRequested)
            {
                codes.Revoke(item.Phone, item.Code.ChallengeId);
            }
        }
    }
}
