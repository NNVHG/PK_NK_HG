using System.Threading.Channels;
using Dental.Application.Features.MOD_AUTH;
using Microsoft.Extensions.Hosting;

namespace Dental.Infrastructure.Services;

public sealed class PatientActivationDelivery(IPatientActivationSms sms, IPatientActivationProofs proofs)
    : BackgroundService, IPatientActivationDelivery
{
    private readonly Channel<ActivationDispatch> queue = Channel.CreateBounded<ActivationDispatch>(new BoundedChannelOptions(64) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    public bool IsConfigured => sms.IsConfigured;
    public bool Enqueue(ActivationDispatch dispatch) => queue.Writer.TryWrite(dispatch);
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in queue.Reader.ReadAllAsync(stoppingToken))
        {
            if (!proofs.IsCurrent(item.Phone, item.ChallengeId)) continue;
            try { if (!await sms.SendAsync(item.Phone, item.Code, stoppingToken)) proofs.Revoke(item.Phone, item.ChallengeId); }
            catch (Exception) when (!stoppingToken.IsCancellationRequested) { proofs.Revoke(item.Phone, item.ChallengeId); }
        }
    }
}
