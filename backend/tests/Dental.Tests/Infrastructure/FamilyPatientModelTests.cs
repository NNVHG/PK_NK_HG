using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace Dental.Tests.Infrastructure;
public sealed class FamilyPatientModelTests
{
    private static DentalDbContext CreateDb() => new(new DbContextOptionsBuilder<DentalDbContext>().UseNpgsql("Host=localhost;Database=model_only").Options);
    [Fact] public void UserPatientRelationshipIsOneToOneWithRestrictDelete()
    {
        using var db = CreateDb();
        var entity = db.Model.FindEntityType(typeof(Patient))!;
        var fk = entity.GetForeignKeys().Single(f => f.Properties.Single().Name == nameof(Patient.UserId));
        Assert.True(fk.IsUnique); Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
    }
    [Fact] public void PatientOwnerIndexIsUniqueAndPatientNumberStillIs()
    {
        using var db = CreateDb(); var indexes = db.Model.FindEntityType(typeof(Patient))!.GetIndexes();
        Assert.True(indexes.Single(i => i.Properties.Single().Name == nameof(Patient.UserId)).IsUnique);
        Assert.True(indexes.Single(i => i.Properties.Single().Name == nameof(Patient.PatientNumber)).IsUnique);
    }
    [Fact] public void PatientPhoneIsUnique()
    {
        using var db = CreateDb();
        Assert.True(db.Model.FindEntityType(typeof(Patient))!.GetIndexes().Single(i => i.Properties.Single().Name == nameof(Patient.Phone)).IsUnique);
    }
    [Fact] public void UserPhoneRemainsUnique()
    {
        using var db = CreateDb();
        Assert.True(db.Model.FindEntityType(typeof(User))!.GetIndexes().Single(i => i.Properties.Single().Name == nameof(User.Phone)).IsUnique);
    }
}
