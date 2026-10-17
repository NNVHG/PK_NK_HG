using Dental.Application.Features.MOD_RX;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Repositories;
public sealed class PublicPrescriptionRepository(DentalDbContext db) : IPublicPrescriptionRepository
{
    private IQueryable<Prescription> Eligible(string code) => db.Prescriptions.AsNoTracking()
        .Where(p => p.PrescriptionCode == code && !p.IsCancelled && p.Visit.Status != VisitStatuses.Cancelled && p.Visit.Patient.IsActive);
    public Task<PublicPrescriptionSummary?> SummaryAsync(string code, CancellationToken ct) =>
        Eligible(code).Select(p => new PublicPrescriptionSummary(p.PrescriptionCode, p.CreatedAt)).SingleOrDefaultAsync(ct);
    public Task<PublicPrescriptionProof?> ProofAsync(string code, CancellationToken ct) =>
        Eligible(code).Select(p => new PublicPrescriptionProof(p.Id, p.Visit.Patient.CitizenIdVersion, p.Visit.Patient.CitizenIdHash)).SingleOrDefaultAsync(ct);
    public Task<PublicPrescriptionDetails?> DetailsAsync(string code, int citizenIdVersion, CancellationToken ct) =>
        Eligible(code).Where(p => p.Visit.Patient.CitizenIdVersion == citizenIdVersion && p.Visit.Patient.CitizenIdHash != null)
            .Select(p => new PublicPrescriptionDetails(p.PrescriptionCode, p.CreatedAt, p.Visit.Patient.FullName,
                db.Users.Where(u => u.UserId == p.DoctorId).Select(u => u.FullName).First(),
                p.Diagnosis, p.DoctorAdvice, p.RevisitDate,
                p.Items.OrderBy(i => i.Id).Select(i => new PublicPrescriptionMedicine(i.MedicineName, i.Strength, i.Unit, i.Quantity, i.UsageInstructions, i.TreatmentDays)).ToList()))
            .SingleOrDefaultAsync(ct);
}
