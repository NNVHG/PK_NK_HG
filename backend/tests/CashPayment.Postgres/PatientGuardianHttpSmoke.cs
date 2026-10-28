using System.Net;
using System.Net.Http.Json;
using Dental.Application.Features.Patients.DTOs;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

internal static class PatientGuardianHttpSmoke
{
    public static async Task RunAsync(
        HttpClient client,
        DbContextOptions<DentalDbContext> options,
        int receptionistId,
        Action<string, int> authenticate,
        Action<bool, string> check)
    {
        await using var db = new DentalDbContext(options);
        if (!db.Database.GetDbConnection().Database.StartsWith("dental_cash_test_", StringComparison.Ordinal))
            throw new InvalidOperationException("Owned test database required");

        var nextNumber = (await db.Patients.MaxAsync(p => (int?)p.PatientNumber) ?? 0) + 1;
        var firstGuardian = new Patient
        {
            PatientNumber = nextNumber,
            FullName = "Synthetic guardian one",
            DateOfBirth = new DateOnly(1985, 1, 1),
            Phone = "0999999941"
        };
        var secondGuardian = new Patient
        {
            PatientNumber = nextNumber + 1,
            FullName = "Synthetic guardian two",
            DateOfBirth = new DateOnly(1986, 1, 1),
            Phone = "0999999942"
        };
        var ward = new Patient
        {
            PatientNumber = nextNumber + 2,
            FullName = "Synthetic ward",
            DateOfBirth = new DateOnly(2015, 1, 1),
            Phone = "0999999943"
        };
        db.Patients.AddRange(firstGuardian, secondGuardian, ward);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var activePath = $"/api/patient-guardians/wards/{ward.PatientId}/active";
        var revokePath = $"/api/patient-guardians/wards/{ward.PatientId}/revoke";
        client.DefaultRequestHeaders.Authorization = null;
        using (var response = await client.GetAsync(activePath))
            check(response.StatusCode == HttpStatusCode.Unauthorized, "Guardian active read requires JWT401");
        using (var response = await client.PostAsJsonAsync(revokePath, new RevokePatientGuardianRequest(1, DateTime.UtcNow, "Synthetic")))
            check(response.StatusCode == HttpStatusCode.Unauthorized, "Guardian revoke requires JWT401");

        authenticate(RoleCodes.Receptionist, receptionistId);
        async Task<PatientGuardianResponse> Assign(int guardianPatientId)
        {
            using var response = await client.PostAsJsonAsync("/api/patient-guardians/assign",
                new AssignPatientGuardianRequest(guardianPatientId, ward.PatientId, "Người giám hộ"));
            check(response.StatusCode == HttpStatusCode.OK && response.Headers.CacheControl?.NoStore == true,
                "Guardian assignment returns 200/no-store");
            return (await response.Content.ReadFromJsonAsync<PatientGuardianResponse>())!;
        }

        var first = await Assign(firstGuardian.PatientId);
        using (var response = await client.PostAsJsonAsync(revokePath,
            new RevokePatientGuardianRequest(first.PatientGuardianId, first.AssignedAt, "Synthetic revoke")))
            check(response.StatusCode == HttpStatusCode.OK,
                $"Second screen revokes original relation200 (actual {(int)response.StatusCode})");
        var second = await Assign(secondGuardian.PatientId);
        var auditsBeforeStale = await db.AuditLogs.CountAsync(a => a.EntityType == "PatientGuardian");

        using (var response = await client.PostAsJsonAsync(revokePath,
            new RevokePatientGuardianRequest(first.PatientGuardianId, first.AssignedAt, "Stale screen")))
            check(response.StatusCode == HttpStatusCode.Conflict &&
                (await response.Content.ReadAsStringAsync()).Contains("PAT_GUARDIAN_CONFLICT", StringComparison.Ordinal),
                "Stale screen cannot revoke replacement relation409");
        check(await db.PatientGuardians.AsNoTracking().AnyAsync(g =>
                g.PatientGuardianId == second.PatientGuardianId && g.IsActive) &&
            await db.AuditLogs.CountAsync(a => a.EntityType == "PatientGuardian") == auditsBeforeStale,
            "Stale rejection preserves new relation and adds no audit write");

        using (var response = await client.PostAsJsonAsync(revokePath,
            new RevokePatientGuardianRequest(second.PatientGuardianId, first.AssignedAt, "Wrong version")))
            check(response.StatusCode == HttpStatusCode.Conflict,
                "Correct relation ID with stale version is rejected409");
        using (var response = await client.GetAsync(activePath))
        {
            var active = await response.Content.ReadFromJsonAsync<PatientGuardianResponse>();
            check(response.StatusCode == HttpStatusCode.OK && response.Headers.CacheControl?.NoStore == true &&
                active?.PatientGuardianId == second.PatientGuardianId,
                "Reload returns current relation after stale rejection");
        }
        using (var response = await client.PostAsJsonAsync(revokePath,
            new RevokePatientGuardianRequest(second.PatientGuardianId, second.AssignedAt, "Synthetic revoke")))
            check(response.StatusCode == HttpStatusCode.OK,
                "Reloaded relation can be revoked with current ID and version200");
        check(!await db.PatientGuardians.AsNoTracking().AnyAsync(g => g.WardPatientId == ward.PatientId && g.IsActive),
            "Final revoke leaves no active guardian");
    }
}
