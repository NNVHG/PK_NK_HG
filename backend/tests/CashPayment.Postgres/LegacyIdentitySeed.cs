using Dental.Domain.Entities;
using Microsoft.EntityFrameworkCore;

// Migration fixtures intentionally write the old schema before migrating to the current model.
// Keep their identity seed independent of columns introduced by later features.
internal static class LegacyIdentitySeed
{
    private sealed class LegacyRole
    {
        public int RoleId { get; set; }
        public string RoleCode { get; set; } = "";
        public string RoleName { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }
    private sealed class LegacyUser
    {
        public int UserId { get; set; }
        public int RoleId { get; set; }
        public string FullName { get; set; } = "";
        public string Phone { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
    private sealed class LegacyContext(DbContextOptions<LegacyContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder b)
        {
            b.Entity<LegacyRole>().ToTable("Roles").HasKey(x => x.RoleId);
            b.Entity<LegacyUser>().ToTable("Users").HasKey(x => x.UserId);
        }
    }
    public static async Task UsersAsync(string connection, IEnumerable<User> users)
    {
        await using var db = new LegacyContext(new DbContextOptionsBuilder<LegacyContext>().UseNpgsql(connection).Options);
        foreach (var user in users)
        {
            var role = new LegacyRole { RoleCode = user.Role.RoleCode, RoleName = user.Role.RoleName, CreatedAt = user.Role.CreatedAt };
            db.Add(role); await db.SaveChangesAsync(); user.Role.RoleId = role.RoleId; user.RoleId = role.RoleId;
            var row = new LegacyUser { RoleId = role.RoleId, FullName = user.FullName, Phone = user.Phone,
                PasswordHash = user.PasswordHash, IsActive = user.IsActive, CreatedAt = user.CreatedAt };
            db.Add(row); await db.SaveChangesAsync(); user.UserId = row.UserId;
        }
    }
}
