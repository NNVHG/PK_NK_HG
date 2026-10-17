using Dental.Application.Features.MOD_RX;
namespace Dental.Application.Interfaces;
public interface IPublicPrescriptionRepository
{
    Task<PublicPrescriptionSummary?> SummaryAsync(string code, CancellationToken ct);
    Task<PublicPrescriptionProof?> ProofAsync(string code, CancellationToken ct);
    Task<PublicPrescriptionDetails?> DetailsAsync(string code, int citizenIdVersion, CancellationToken ct);
}
public interface IPublicPrescriptionAccess
{
    bool IsLocked(string code);
    bool RecordResult(string code, bool success);
    PublicPrescriptionSession Issue(string code, int prescriptionId, int citizenIdVersion);
    bool Validate(string code, int prescriptionId, int citizenIdVersion, string token);
}
