using System.Buffers.Binary;
using Dental.Application.Common;
using Dental.Application.Features.MOD_IMG;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Infrastructure.Data;
using Dental.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;
namespace Dental.Tests.Images;

public sealed class DentalImageTests
{
    public static byte[] Png => Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a7KkAAAAASUVORK5CYII=");
    [Theory]
    [InlineData(null, true)] [InlineData(11, true)] [InlineData(48, true)] [InlineData(51, true)] [InlineData(85, true)]
    [InlineData(19, false)] [InlineData(56, false)] [InlineData(90, false)] [InlineData(0, false)] [InlineData(-11, false)]
    public void FdiOnly(int? tooth, bool valid) => Assert.Equal(valid, DentalImageRequestValidator.ValidTooth(tooth));
    [Theory]
    [InlineData("Intraoral", true)] [InlineData("Periapical", true)] [InlineData("Panorama", true)] [InlineData("Bitewing", true)] [InlineData("Panoramic", false)] [InlineData("Cephalometric", true)] [InlineData("Other", false)] [InlineData("XRay", false)] [InlineData("<script>", false)]
    public void CategoryIsApprovedEnum(string category, bool valid) => Assert.Equal(valid, new DentalImageRequestValidator().Validate(new DentalImageRequest(1, null, null, category)).IsValid);
    [Fact] public void PngHeaderDimensions() => Assert.Equal((1, 1), DentalImageFile.Inspect("ảnh.PNG", "image/png", Png));
    [Theory] [InlineData(500,true)] [InlineData(501,false)]
    public void NotesAreBounded(int length,bool valid) => Assert.Equal(valid,new DentalImageRequestValidator().Validate(new DentalImageRequest(1,null,null,"Intraoral",new string('a',length))).IsValid);
    [Theory]
    [InlineData("file.jpg", "image/png")] [InlineData("file.png", "image/jpeg")] [InlineData("file.svg", "image/png")] [InlineData("file.png.exe", "image/png")]
    public void MimeExtensionMustMatch(string name, string type) => Assert.Null(DentalImageFile.Inspect(name, type, Png));
    [Fact] public void TooLargeRejected() => Assert.Null(DentalImageFile.Inspect("x.png", "image/png", new byte[DentalImageFile.MaxBytes + 1]));
    [Fact] public void TruncationRejected() { for (var n = 0; n < Png.Length; n++) Assert.Null(DentalImageFile.Inspect("x.png", "image/png", Png[..n])); }
    [Theory] [InlineData(0)] [InlineData(4097)] [InlineData(int.MaxValue)]
    public void OversizedPngDimensionsRejected(int width) { var b = Png; BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(16), (uint)width); Assert.Null(DentalImageFile.Inspect("x.png", "image/png", b)); }
    [Fact] public void FakeContainerRejected() => Assert.Null(DentalImageFile.Inspect("x.webp", "image/webp", "RIFF0000WEBP00000000000000000000000000"u8.ToArray()));
    [Fact] public void JpegHeaderDimensionsAndTruncatedSegment()
    {
        byte[] b = [255,216,255,192,0,11,8,0,1,0,2,1,1,17,0,255,218,0,2,255,217];
        Assert.Equal((2,1), DentalImageFile.Inspect("x.jpg", "image/jpeg", b));
        b[5] = 250; Assert.Null(DentalImageFile.Inspect("x.jpg", "image/jpeg", b));
    }
    [Fact] public void WebpLosslessDimensionsAndContainerLength()
    {
        var b = new byte[34]; "RIFF"u8.CopyTo(b); BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(4), 26); "WEBPVP8L"u8.CopyTo(b.AsSpan(8)); BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(16),14); b[20]=47;
        Assert.Equal((1,1), DentalImageFile.Inspect("x.webp", "image/webp", b));
        b[4]=0; Assert.Null(DentalImageFile.Inspect("x.webp", "image/webp", b));
    }
    [Fact] public void ArbitraryHeadersNeverThrow()
    {
        var random = new Random(42);
        for(var i=0;i<300;i++) { var b = new byte[random.Next(0,250)]; random.NextBytes(b); foreach(var type in new[]{"image/png","image/jpeg","image/webp"}) DentalImageFile.Inspect("x." + (type == "image/png" ? "png" : type == "image/jpeg" ? "jpg" : "webp"),type,b); }
    }
    [Fact] public async Task InvalidInputDoesNotReachStorage()
    {
        var repo = Substitute.For<IDentalImageRepository>(); var service = new DentalImageService(repo, Substitute.For<IDentalImageStorage>());
        var r = await service.UploadAsync(new(1, null, 19, "Intraoral"), "x.png", "image/png", Png, 5, RoleCodes.Admin, null);
        Assert.True(r.IsFailure); Assert.Empty(repo.ReceivedCalls());
    }
    [Theory] [InlineData(RoleCodes.Patient)] [InlineData(RoleCodes.Receptionist)] [InlineData("UNKNOWN")]
    public async Task UploadRoleCannotBeBypassed(string role)
    {
        var repo = Substitute.For<IDentalImageRepository>(); var r = await new DentalImageService(repo, Substitute.For<IDentalImageStorage>()).UploadAsync(new(1, null, null, "Intraoral"), "x.png", "image/png", Png, 5, role, null);
        Assert.Equal(Error.Forbidden, r.Error); Assert.Empty(repo.ReceivedCalls());
    }
    [Fact] public async Task OriginalNameCannotBecomeStoragePath()
    {
        var repo = Substitute.For<IDentalImageRepository>(); var b = Png;
        await new DentalImageService(repo, Substitute.For<IDentalImageStorage>()).UploadAsync(new(1, null, null, "Intraoral"), "..\\..\\ảnh.png", "image/png", b, 5, RoleCodes.Admin, null);
        await repo.Received(1).UploadAsync(Arg.Any<DentalImageRequest>(), "ảnh.png", "image/png", b, 1, 1, 5, RoleCodes.Admin, null, Arg.Any<CancellationToken>());
    }
    [Fact] public void ModelHasRequiredRelationshipsAndConcurrency()
    {
        using var db = new DentalDbContext(new DbContextOptionsBuilder<DentalDbContext>().UseNpgsql("Host=localhost;Database=model_only").Options);
        var model = db.Model.FindEntityType(typeof(DentalImage))!;
        Assert.True(model.FindProperty(nameof(DentalImage.Version))!.IsConcurrencyToken);
        Assert.Equal(4, model.GetForeignKeys().Count());
        Assert.Contains(model.GetForeignKeys(), x => x.Properties.Single().Name == nameof(DentalImage.ClinicalUpdatedByUserId));
        Assert.All(model.GetForeignKeys(), x => Assert.Equal(DeleteBehavior.Restrict, x.DeleteBehavior));
    }
    [Theory] [InlineData("../outside.png")] [InlineData("1/../../outside.png")] [InlineData("1/a.png")] [InlineData("0/11111111111111111111111111111111.png")] [InlineData("1/11111111111111111111111111111111.exe")]
    public async Task StorageRejectsTraversal(string path) => await Assert.ThrowsAsync<InvalidOperationException>(() => new DentalImageStorage(Path.GetTempPath()).WriteAsync(path, Png, default));
    [Fact] public async Task StorageUsesCreateNewAndCanCleanUp()
    {
        var root = Path.Combine(Path.GetTempPath(), "dental-image-unit-" + Guid.NewGuid().ToString("N"));
        var key = "1/" + Guid.NewGuid().ToString("N") + ".png"; var storage = new DentalImageStorage(root);
        try { await storage.WriteAsync(key, Png, default); await Assert.ThrowsAsync<IOException>(() => storage.WriteAsync(key, Png, default)); await storage.RemoveAsync(key); Assert.False(File.Exists(Path.Combine(root, "uploads", "images", key))); }
        finally { var full=Path.GetFullPath(root); Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), full, StringComparison.OrdinalIgnoreCase); Assert.StartsWith("dental-image-unit-",Path.GetFileName(full)); if (Directory.Exists(full)) Directory.Delete(full, true); }
    }
}
