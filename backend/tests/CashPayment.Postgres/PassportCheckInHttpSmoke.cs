using System.Net;
using System.Net.Http.Json;
using Dental.Application.Features.Queue.DTOs;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

internal static class PassportCheckInHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options,
        Action<string> authenticate, Action<bool, string> check)
    {
        await using var db = new DentalDbContext(options);
        if (!db.Database.GetDbConnection().Database.StartsWith("dental_cash_test_", StringComparison.Ordinal))
            throw new InvalidOperationException("Owned test database required");

        var patientRole = await db.Roles.SingleOrDefaultAsync(role => role.RoleCode == RoleCodes.Patient);
        if (patientRole is null)
        {
            patientRole = new Role { RoleCode = RoleCodes.Patient, RoleName = "Synthetic patient" };
            db.Roles.Add(patientRole);
            await db.SaveChangesAsync();
        }

        var hasher = new KeyedIdentityHasher(new byte[32]);
        const string passport = "B1234567";
        var bound = new Patient
        {
            FullName = "Synthetic passport patient", DateOfBirth = new(1990, 1, 1), Phone = "0987654377",
            IsActive = true, CitizenIdHash = hasher.Hash(passport, IdentityDocumentType.Passport, "US"),
            CitizenIdMasked = hasher.Mask(passport, IdentityDocumentType.Passport)
        };
        var unbound = new Patient
        {
            FullName = "Synthetic new passport patient", DateOfBirth = new(1991, 1, 1), Phone = "0987654388", IsActive = true
        };
        db.Patients.AddRange(bound, unbound);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        const string endpoint = "/api/queue/check-in";
        CheckInRequest Request(Patient patient, string identifier, string? country) =>
            new(patient.PatientId, CitizenId: identifier, Phone: patient.Phone,
                DocumentType: IdentityDocumentType.Passport, NationalityCode: country);

        client.DefaultRequestHeaders.Authorization = null;
        using (var response = await client.PostAsJsonAsync(endpoint, Request(bound, passport, "US")))
            check(response.StatusCode == HttpStatusCode.Unauthorized, "F_CHK_01 passport check-in requires JWT");
        foreach (var role in new[] { RoleCodes.Patient, RoleCodes.Dentist, RoleCodes.Assistant })
        {
            authenticate(role);
            using var response = await client.PostAsJsonAsync(endpoint, Request(bound, passport, "US"));
            check(response.StatusCode == HttpStatusCode.Forbidden, $"F_CHK_01 passport check-in forbids {role}");
        }

        authenticate(RoleCodes.Receptionist);
        using (var response = await client.PostAsJsonAsync(endpoint, Request(bound, passport, null)))
            check(response.StatusCode == HttpStatusCode.BadRequest, "F_CHK_01 passport requires issuing country");
        using (var response = await client.PostAsJsonAsync(endpoint, Request(bound, passport, "VN")))
            check(response.StatusCode == HttpStatusCode.BadRequest, "F_CHK_01 wrong issuing country rejects stored passport");
        check(!await db.QueueEntries.AnyAsync(entry => entry.PatientId == bound.PatientId) &&
            !await db.Users.AnyAsync(user => user.Phone == bound.Phone), "Rejected passport attempts create no queue or account");

        using (var response = await client.PostAsJsonAsync(endpoint, Request(bound, passport, "us")))
        {
            var body = await response.Content.ReadAsStringAsync();
            check(response.StatusCode == HttpStatusCode.Created,
                $"F_CHK_01 correct passport creates waiting ticket ({response.StatusCode})");
            check(response.Headers.CacheControl?.NoStore == true, "F_CHK_01 passport response is NoStore");
            check(!body.Contains(passport, StringComparison.OrdinalIgnoreCase),
                "F_CHK_01 ticket response excludes raw passport");
        }
        var persistedBound = await db.Patients.AsNoTracking().SingleAsync(patient => patient.PatientId == bound.PatientId);
        var account = await db.Users.AsNoTracking().SingleAsync(user => user.UserId == persistedBound.UserId);
        check(account.RoleId == patientRole.RoleId && account.RequiresPhoneActivation && account.LoginByCitizenId &&
            BCrypt.Net.BCrypt.Verify(bound.Phone, account.PasswordHash), "Verified passport creates one pending personal Patient account");
        check(await db.QueueEntries.AsNoTracking().CountAsync(entry => entry.PatientId == bound.PatientId &&
            entry.Status == QueueStatus.Waiting && entry.Visit != null && entry.Visit.Status == VisitStatuses.Created) == 1,
            "Verified passport creates one waiting ticket and Visit");

        using (var response = await client.PostAsJsonAsync(endpoint, Request(unbound, "C9876543", "GB")))
            check(response.StatusCode == HttpStatusCode.Created, "F_CHK_01 verified new passport is accepted");
        var persistedUnbound = await db.Patients.AsNoTracking().SingleAsync(patient => patient.PatientId == unbound.PatientId);
        check(persistedUnbound.CitizenIdHash == hasher.Hash("C9876543", IdentityDocumentType.Passport, "GB") &&
            persistedUnbound.CitizenIdMasked == "C9****43" && persistedUnbound.CitizenIdVersion == 1 &&
            persistedUnbound.UserId.HasValue, "F_PAT_01 stores country-bound HMAC and masked passport only");

        var duplicate = new Patient
        {
            FullName = "Synthetic duplicate passport patient", DateOfBirth = new(1992, 1, 1), Phone = "0987654399", IsActive = true
        };
        db.Patients.Add(duplicate);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        using (var response = await client.PostAsJsonAsync(endpoint, Request(duplicate, passport, "US")))
            check(response.StatusCode == HttpStatusCode.BadRequest, "F_PAT_03 duplicate passport and country cannot bind another profile");
        check(!await db.QueueEntries.AnyAsync(entry => entry.PatientId == duplicate.PatientId) &&
            !await db.Users.AnyAsync(user => user.Phone == duplicate.Phone), "Duplicate passport attempt creates no queue or account");
        check(await db.AuditLogs.AsNoTracking().Where(log =>
                (log.EntityType == "Patient" && (log.EntityId == unbound.PatientId || log.EntityId == bound.PatientId)) ||
                (log.EntityType == "User" && log.EntityId == account.UserId))
            .AllAsync(log => log.Detail == null || !log.Detail.Contains(passport) && !log.Detail.Contains("C9876543") &&
                !log.Detail.Contains(bound.Phone) && !log.Detail.Contains(unbound.Phone)),
            "F_CHK_01 passport audit excludes raw identifiers and phone");
    }
}
