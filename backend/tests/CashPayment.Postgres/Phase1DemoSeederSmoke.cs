using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Data.Seeders;
using Microsoft.EntityFrameworkCore;

internal static class Phase1DemoSeederSmoke
{
    private const string SharedPhone = "0900000091";
    public static async Task RunAsync(DbContextOptions<DentalDbContext> options, Action<bool, string> check)
    {
        await using var db = new DentalDbContext(options);
        var adminRole = await RoleAsync(db, RoleCodes.Admin);
        var patientRole = await RoleAsync(db, RoleCodes.Patient);
        var dentistRole = await RoleAsync(db, RoleCodes.Dentist);
        var receptionistRole = await RoleAsync(db, RoleCodes.Receptionist);
        var admin = new User { Role = adminRole, FullName = "Synthetic seed admin", Phone = "0999999091", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Synthetic123") };
        var dentist = new User { Role = dentistRole, FullName = "Synthetic seed dentist", Phone = "0900000003", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Synthetic123") };
        var receptionist = new User { Role = receptionistRole, FullName = "Synthetic seed receptionist", Phone = "0900000002", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Synthetic123") };
        db.Users.AddRange(admin, dentist, receptionist);
        await db.SaveChangesAsync();

        await Phase1DemoSeeder.SeedAsync(db);
        await Phase1DemoSeeder.SeedAsync(db);

        var today = new Dental.Application.Common.VietnamClock(TimeProvider.System).Today;
        var patients = await db.Patients.AsNoTracking().Where(p => p.Phone == SharedPhone).ToListAsync();
        var patientIds = patients.Select(p => p.PatientId).ToArray();
        var accounts = await db.Users.AsNoTracking().Where(u => u.Phone == SharedPhone && u.LoginByCitizenId).ToListAsync();
        check(patients.Count == 6 && accounts.Count == 6 && patients.Select(p => p.UserId).Distinct().Count() == 6,
            "Phase 1 seed creates six individual synthetic patients with shared contact phone");
        check(await db.Appointments.CountAsync(a => patientIds.Contains(a.PatientId) && a.AppointmentDate == today) == 6 &&
            await db.Appointments.Where(a => patientIds.Contains(a.PatientId) && a.AppointmentDate == today).Select(a => a.Status).Distinct().CountAsync() == 4,
            "Phase 1 seed covers four appointment statuses for today");
        check(await db.QueueEntries.CountAsync(q => patientIds.Contains(q.PatientId) && q.QueueDate == today) == 3 &&
            await db.QueueEntries.Where(q => patientIds.Contains(q.PatientId) && q.QueueDate == today).Select(q => q.Status).Distinct().CountAsync() == 2,
            "Phase 1 seed presents one consultation and two waiting patients");
        check(await db.MedicalHistoryRecords.CountAsync(h => patientIds.Contains(h.PatientId)) == 1 &&
            await db.MedicalHistoryItems.CountAsync(h => h.Name.Contains("(demo)")) == 2 &&
            await db.VitalSignRecords.CountAsync(v => patientIds.Contains(v.PatientId)) == 1 &&
            await db.ToothConditions.CountAsync(t => db.Visits.Any(v => v.VisitId == t.VisitId && patientIds.Contains(v.PatientId))) == 1,
            "Phase 1 seed includes synthetic allergy/history, vital signs and FDI record");
        check(await db.Patients.CountAsync(p => p.Phone == SharedPhone) == 6 &&
            await db.Appointments.CountAsync(a => patientIds.Contains(a.PatientId) && a.AppointmentDate == today) == 6 &&
            await db.QueueEntries.CountAsync(q => patientIds.Contains(q.PatientId) && q.QueueDate == today) == 3,
            "Phase 1 seed is idempotent across two consecutive runs");
    }

    private static async Task<Role> RoleAsync(DentalDbContext db, string code)
    {
        var role = await db.Roles.SingleOrDefaultAsync(r => r.RoleCode == code);
        if (role is not null) return role;
        role = new Role { RoleCode = code, RoleName = "Synthetic " + code };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        return role;
    }
}
