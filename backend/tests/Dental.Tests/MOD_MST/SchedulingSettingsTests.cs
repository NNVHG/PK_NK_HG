using Dental.Application.Common;
using Dental.Application.Features.MOD_MST.Services;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Services;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;
namespace Dental.Tests.MOD_MST;

public sealed class SchedulingSettingsTests
{
    private static SchedulingSettingsRequest Input() => new("08:00", "12:00", "13:30", "20:00", 30, new string('A', 64));
    [Fact] public void DefaultUsesDL174AndEverySlotFitsItsSession()
    {
        var slots = ClinicSchedule.Default.Slots(); Assert.Equal(21, slots.Length); Assert.Equal(new TimeOnly(19, 30), slots[^1]);
        Assert.DoesNotContain(new TimeOnly(12, 0), slots); Assert.DoesNotContain(new TimeOnly(20, 0), slots);
        Assert.True(ClinicSchedule.TryParse("08:00", "11:30", "13:30", "16:30", 60, out var custom));
        Assert.Equal(new[] { new TimeOnly(8, 0), new(9, 0), new(10, 0), new(13, 30), new(14, 30), new(15, 30) }, custom.Slots());
    }
    [Fact] public void ConfigSchemaHasStableNumericIdUniqueKeyAndUpdaterMetadata()
    {
        using var db = new DentalDbContext(new DbContextOptionsBuilder<DentalDbContext>().UseNpgsql("Host=127.0.0.1;Database=synthetic_model_only").Options);
        var entity = db.Model.FindEntityType(typeof(SystemConfig))!;
        Assert.Equal(nameof(SystemConfig.Id), Assert.Single(entity.FindPrimaryKey()!.Properties).Name);
        Assert.Equal("ConfigKey", entity.FindProperty(nameof(SystemConfig.Key))!.GetColumnName());
        Assert.Equal("ConfigValue", entity.FindProperty(nameof(SystemConfig.Value))!.GetColumnName());
        Assert.Contains(entity.GetIndexes(), x => x.IsUnique && x.Properties.Single().Name == nameof(SystemConfig.Key));
        Assert.True(entity.FindProperty(nameof(SystemConfig.UpdatedByUserId))!.IsNullable);
        Assert.Equal(DeleteBehavior.Restrict, entity.GetForeignKeys().Single().DeleteBehavior);
    }
    [Theory]
    [InlineData("8:00", "12:00", "13:30", "20:00", 30)]
    [InlineData("08:00", "08:00", "13:30", "20:00", 30)]
    [InlineData("08:00", "13:00", "12:00", "20:00", 30)]
    [InlineData("08:00", "12:00", "13:30", "13:30", 30)]
    [InlineData("08:00", "12:00", "13:30", "24:00", 30)]
    [InlineData("08:00", "12:00", "13:30", "20:00", 0)]
    [InlineData("08:00", "12:00", "13:30", "20:00", -1)]
    [InlineData("08:00", "12:00", "13:30", "20:00", 1441)]
    [InlineData("08:00", "12:00", "13:30", "20:00", 1000)]
    public void InvalidScheduleRejected(string start, string ls, string le, string end, int duration) => Assert.False(ClinicSchedule.TryParse(start, ls, le, end, duration, out _));
    [Fact] public void RequestRequiresRevisionAndValidSchedule()
    {
        var v = new SchedulingSettingsValidator(); Assert.True(v.Validate(Input()).IsValid);
        Assert.False(v.Validate(Input() with { ExpectedRevision = "" }).IsValid); Assert.False(v.Validate(Input() with { ExpectedRevision = new string('g', 64) }).IsValid);
        Assert.False(v.Validate(Input() with { WorkStartTime = null! }).IsValid); Assert.False(v.Validate(Input() with { SlotDurationMinutes = 0 }).IsValid);
    }
    [Theory] [InlineData(RoleCodes.Patient)] [InlineData(RoleCodes.Dentist)] [InlineData(RoleCodes.Assistant)] [InlineData(RoleCodes.Receptionist)] [InlineData(null)]
    public async Task OnlyAdminReadsAndWrites(string? role)
    {
        var repo = Substitute.For<ISchedulingSettingsRepository>(); var s = new SchedulingSettingsService(repo);
        Assert.Equal(Error.Forbidden, (await s.ReadAsync(1, role)).Error); Assert.Equal(Error.Forbidden, (await s.SaveAsync(1, role, Input(), null)).Error); Assert.Empty(repo.ReceivedCalls());
    }
    [Fact] public async Task MissingActorAndBadInputNeverReadRepository()
    {
        var repo = Substitute.For<ISchedulingSettingsRepository>(); var s = new SchedulingSettingsService(repo);
        Assert.Equal(Error.Unauthorized, (await s.ReadAsync(null, RoleCodes.Admin)).Error); Assert.Equal(Error.Unauthorized, (await s.SaveAsync(0, RoleCodes.Admin, Input(), null)).Error);
        Assert.True((await s.SaveAsync(1, RoleCodes.Admin, Input() with { SlotDurationMinutes = 0 }, null)).IsFailure); Assert.Empty(repo.ReceivedCalls());
    }
    [Fact] public async Task AdminResponseUsesEffectiveScheduleAndRevision()
    {
        var repo = Substitute.For<ISchedulingSettingsRepository>(); repo.ReadAsync(1, Arg.Any<CancellationToken>()).Returns(Result<ScheduleSnapshot>.Success(new(ClinicSchedule.Default, new string('A', 64), true)));
        var result = await new SchedulingSettingsService(repo).ReadAsync(1, RoleCodes.Admin); Assert.Equal("20:00", result.Value!.WorkEndTime); Assert.True(result.Value.UsingFallback);
    }
    private static readonly DateOnly Day = new(2026, 12, 1);
    private static Appointment Booking(int hour, int minute, int duration, DateOnly? day = null) => new() { AppointmentDate = day ?? Day, SlotTime = new(hour, minute), SlotDurationMinutes = duration };
    [Fact] public void HalfOpenIntervalsPreserveOldThirtyMinuteBooking()
    {
        var old = Booking(8, 30, 30); Assert.True(BookingOccupancy.Overlaps(old, Day, new(8, 0), 60));
        Assert.False(BookingOccupancy.Overlaps(old, Day, new(9, 0), 60)); Assert.False(BookingOccupancy.Overlaps(old, Day, new(8, 0), 30));
        Assert.Equal(1, BookingOccupancy.Peak([old], Day, new(8, 0), 60));
    }
    [Fact] public void CapacityMeasuresPeakInsteadOfSummingNonoverlappingBookings()
    {
        var rows = Enumerable.Range(0, 60).Select(_ => Booking(8, 0, 30)).Concat(Enumerable.Range(0, 60).Select(_ => Booking(8, 30, 30))).ToArray();
        Assert.Equal(60, BookingOccupancy.Peak(rows, Day, new(8, 0), 60));
        Assert.Equal(100, BookingOccupancy.Peak(rows.Concat(Enumerable.Range(0, 40).Select(_ => Booking(8, 0, 60))), Day, new(8, 0), 60));
    }
    [Fact] public void EmergencyCrossingMidnightHoldsNextDayAndHandlesDateBounds()
    {
        var prior = Booking(23, 50, 30, Day.AddDays(-1)); Assert.True(BookingOccupancy.Overlaps(prior, Day, new(0, 0), 15));
        Assert.False(BookingOccupancy.Overlaps(prior, Day, new(0, 20), 15));
        Assert.Equal(1, BookingOccupancy.Peak([Booking(23, 59, 1440, DateOnly.MaxValue)], DateOnly.MaxValue, new(23, 59), 1440));
    }
}
