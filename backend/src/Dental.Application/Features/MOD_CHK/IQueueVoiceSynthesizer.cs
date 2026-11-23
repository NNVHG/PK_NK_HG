namespace Dental.Application.Features.MOD_CHK;

public interface IQueueVoiceSynthesizer
{
    Task<byte[]?> SynthesizeAsync(string publicName, TimeSpan remaining, CancellationToken ct);
}
