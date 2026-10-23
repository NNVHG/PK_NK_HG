using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Dental.Tests.Infrastructure;

public sealed class SeederModelTests
{
    private static DentalDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<DentalDbContext>().UseNpgsql("Host=localhost;Database=model_only").Options);

    [Fact]
    public void DentalChair_ModelConfiguredCorrectly()
    {
        using var db = CreateDb();
        var entity = db.Model.FindEntityType(typeof(DentalChair))!;
        Assert.NotNull(entity);
        var chairNumberProp = entity.FindProperty(nameof(DentalChair.ChairNumber))!;
        Assert.Equal(50, chairNumberProp.GetMaxLength());
        Assert.False(chairNumberProp.IsNullable);
    }

    [Fact]
    public void DentalService_ModelConfiguredCorrectly()
    {
        using var db = CreateDb();
        var entity = db.Model.FindEntityType(typeof(DentalService))!;
        Assert.NotNull(entity);
        var codeProp = entity.FindProperty(nameof(DentalService.Code))!;
        Assert.Equal(50, codeProp.GetMaxLength());
        Assert.False(codeProp.IsNullable);
    }

    [Fact]
    public void MedicalSupply_ModelConfiguredCorrectly()
    {
        using var db = CreateDb();
        var entity = db.Model.FindEntityType(typeof(MedicalSupply))!;
        Assert.NotNull(entity);
        var unitProp = entity.FindProperty(nameof(MedicalSupply.Unit))!;
        Assert.Equal(50, unitProp.GetMaxLength());
    }

    [Fact]
    public void DiscountPreset_ModelConfiguredCorrectly()
    {
        using var db = CreateDb();
        var entity = db.Model.FindEntityType(typeof(DiscountPreset))!;
        Assert.NotNull(entity);
        var codeProp = entity.FindProperty(nameof(DiscountPreset.Code))!;
        Assert.Equal(30, codeProp.GetMaxLength());
        var typeProp = entity.FindProperty(nameof(DiscountPreset.DiscountType))!;
        Assert.Equal(20, typeProp.GetMaxLength());
    }

    [Fact]
    public void SystemConfig_ModelConfiguredCorrectly()
    {
        using var db = CreateDb();
        var entity = db.Model.FindEntityType(typeof(SystemConfig))!;
        Assert.NotNull(entity);
        var keyProp = entity.FindProperty(nameof(SystemConfig.Key))!;
        Assert.Equal(100, keyProp.GetMaxLength());
        Assert.Equal("ConfigKey", keyProp.GetColumnName());
    }

    [Fact]
    public void FdiCodes_ValidateStandardTeethAndSurfaces()
    {
        Assert.True(FdiCodes.IsTooth(11));
        Assert.True(FdiCodes.IsTooth(36));
        Assert.True(FdiCodes.IsTooth(48));
        Assert.True(FdiCodes.IsTooth(51)); // deciduous tooth
        Assert.False(FdiCodes.IsTooth(99));

        Assert.True(FdiCodes.IsSurface("O"));
        Assert.True(FdiCodes.IsSurface("MOD"));
        Assert.True(FdiCodes.IsSurface(null));
        Assert.False(FdiCodes.IsSurface("XYZ"));
    }
}
