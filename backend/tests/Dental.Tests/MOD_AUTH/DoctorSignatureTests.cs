using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Dental.Application.Common;
using Dental.Application.Features.MOD_AUTH;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using NSubstitute;
using Xunit;
namespace Dental.Tests.MOD_AUTH;
public sealed class DoctorSignatureTests
{
    // Synthetic pixels only, never a person's signature or production seed.
    public static byte[] Png(int width = 2, int height = 2, byte color = 6, byte filter = 0, byte interlace = 0, int extra = 0)
    {
        using var output = new MemoryStream(); output.Write(new byte[] {137,80,78,71,13,10,26,10});
        void Chunk(string type, byte[] data)
        {
            Span<byte> integer = stackalloc byte[4]; BinaryPrimitives.WriteInt32BigEndian(integer, data.Length); output.Write(integer);
            var bytes = Encoding.ASCII.GetBytes(type).Concat(data).ToArray(); output.Write(bytes);
            uint crc = uint.MaxValue; foreach (var b in bytes) { crc ^= b; for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0); }
            BinaryPrimitives.WriteUInt32BigEndian(integer, ~crc); output.Write(integer);
        }
        var header = new byte[13]; BinaryPrimitives.WriteInt32BigEndian(header, width); BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height); header[8] = 8; header[9] = color; header[12] = interlace; Chunk("IHDR", header);
        using var raw = new MemoryStream();
        using (var zlib = new ZLibStream(raw, CompressionMode.Compress, true))
        {
            var channels = color switch { 0 => 1, 2 => 3, 4 => 2, _ => 4 };
            for (var y = 0; y < Math.Min(height, 1025); y++) { zlib.WriteByte(filter); zlib.Write(new byte[Math.Max(0, Math.Min(width, 2049)) * channels]); }
            zlib.Write(new byte[extra]);
        }
        Chunk("IDAT", raw.ToArray()); Chunk("IEND", []); return output.ToArray();
    }
    [Theory] [InlineData(0)] [InlineData(2)] [InlineData(4)] [InlineData(6)]
    public void AcceptSupportedRealPng(byte color)
    {
        var bytes = Png(color: color); Assert.True(SignaturePng.Valid(bytes)); Assert.Equal(bytes, SignaturePng.Decode(Convert.ToBase64String(bytes)));
    }
    [Theory] [InlineData(0,2,6,0,0,0)] [InlineData(2,0,6,0,0,0)] [InlineData(2049,2,6,0,0,0)] [InlineData(2,1025,6,0,0,0)]
    [InlineData(2,2,3,0,0,0)] [InlineData(2,2,6,5,0,0)] [InlineData(2,2,6,0,1,0)] [InlineData(2,2,6,0,0,1)]
    public void RejectUnsupportedOrDecompressedLength(int w, int h, byte color, byte filter, byte interlace, int extra) => Assert.False(SignaturePng.Valid(Png(w,h,color,filter,interlace,extra)));
    [Fact] public void RejectCorruptionTruncationTrailingAndOversize()
    {
        var valid = Png(); var corrupt = valid.ToArray(); corrupt[42] ^= 1;
        foreach (var bytes in new[] { corrupt, valid[..^1], valid.Concat(new byte[] {0}).ToArray(), new byte[SignaturePng.MaxBytes+1], Encoding.UTF8.GetBytes("<svg onload='bad'/>") }) Assert.False(SignaturePng.Valid(bytes));
    }
    [Theory] [InlineData(null)] [InlineData("")] [InlineData("invalid!")] [InlineData("data:image/png;base64,eA==")]
    public void InvalidEncoding(string? value) => Assert.Null(SignaturePng.Decode(value));
    [Fact] public void BoundsAndValidator()
    {
        Assert.Null(SignaturePng.Decode(new string('A',349529)));
        Assert.False(new DoctorSignatureValidator().Validate(new DoctorSignatureRequest(-1,"abc")).IsValid);
        Assert.True(new DoctorSignatureValidator().Validate(new DoctorSignatureRequest(0,Convert.ToBase64String(Png()))).IsValid);
    }
    [Theory] [InlineData(RoleCodes.Patient)] [InlineData(RoleCodes.Receptionist)] [InlineData(RoleCodes.Assistant)] [InlineData(null)]
    public async Task RolesDeniedBeforeRepository(string? role)
    {
        var repo = Substitute.For<IDoctorSignatureRepository>(); var service = new DoctorSignatureService(repo);
        Assert.Equal(Error.Forbidden, (await service.ReadAsync(3,3,role)).Error);
        Assert.Equal(Error.Forbidden, (await service.FileAsync(3,1,3,role)).Error);
        Assert.Equal(Error.Forbidden, (await service.SaveAsync(3,3,role,new(0,Convert.ToBase64String(Png())),null)).Error);
        Assert.Empty(repo.ReceivedCalls());
    }
    [Fact] public async Task ForeignDentistAndInvalidInputNeverReachRepository()
    {
        var repo=Substitute.For<IDoctorSignatureRepository>(); var service=new DoctorSignatureService(repo);
        Assert.Equal(Error.NotFound,(await service.ReadAsync(4,3,RoleCodes.Dentist)).Error);
        Assert.True((await service.FileAsync(3,0,3,RoleCodes.Dentist)).IsFailure);
        Assert.True((await service.ReadAsync(0,3,RoleCodes.Admin)).IsFailure);
        Assert.True((await service.ReadAsync(3,0,RoleCodes.Admin)).IsFailure);
        Assert.True((await service.SaveAsync(3,3,RoleCodes.Dentist,new(0,"bad"),null)).IsFailure);
        Assert.Empty(repo.ReceivedCalls());
    }
    [Theory] [InlineData(RoleCodes.Admin,9)] [InlineData(RoleCodes.Dentist,3)]
    public async Task ApprovedActorDelegatesDecodedBytes(string role,int actor)
    {
        var repo=Substitute.For<IDoctorSignatureRepository>(); var png=Png(); var status=new DoctorSignatureStatus(3,"Synthetic doctor",1,"/api/doctors/3/signature/file?version=1",DateTime.UtcNow);
        repo.SaveAsync(3,actor,role,0,Arg.Any<byte[]>(),null,default).Returns(Result<DoctorSignatureStatus>.Success(status));
        Assert.True((await new DoctorSignatureService(repo).SaveAsync(3,actor,role,new(0,Convert.ToBase64String(png)),null)).IsSuccess);
        await repo.Received().SaveAsync(3,actor,role,0,Arg.Is<byte[]>(b=>b.SequenceEqual(png)),null,default);
    }
}
