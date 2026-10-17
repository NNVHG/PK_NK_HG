using System.Buffers.Binary;
using System.IO.Compression;
using Dental.Application.Common;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using FluentValidation;
namespace Dental.Application.Features.MOD_AUTH;
public sealed record DoctorSignatureStatus(int DoctorId, string DoctorName, int Version, string? SignatureImageUrl, DateTime? UpdatedAt);
public sealed record DoctorSignatureRequest(int ExpectedVersion, string ImageBase64);
public sealed class DoctorSignatureValidator : AbstractValidator<DoctorSignatureRequest>
{
    public DoctorSignatureValidator()
    {
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ImageBase64).NotEmpty().MaximumLength(349528);
    }
}
public static class SignaturePng
{
    public const int MaxBytes = 256 * 1024;
    public static byte[]? Decode(string? encoded)
    {
        if (encoded is null || encoded.Length > 349528) return null;
        byte[] bytes;
        try { bytes = Convert.FromBase64String(encoded); } catch (FormatException) { return null; }
        return Valid(bytes) ? bytes : null;
    }
    public static bool Valid(byte[] data)
    {
        if (data.Length < 57 || data.Length > MaxBytes || !data.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return false;
        var offset = 8; var width = 0; var height = 0; var channels = 0; var sawIdat = false; var idatEnded = false;
        using var compressed = new MemoryStream();
        while (offset <= data.Length - 12)
        {
            var size = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, 4));
            if (size > (uint)(data.Length - offset - 12)) return false;
            var length = (int)size; var type = System.Text.Encoding.ASCII.GetString(data, offset + 4, 4);
            if (data.AsSpan(offset + 4, 4).ToArray().Any(b => b is not (>= 65 and <= 90) and not (>= 97 and <= 122))) return false;
            var chunk = data.AsSpan(offset + 4, length + 4);
            if (Crc(chunk) != BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset + 8 + length, 4))) return false;
            var payload = data.AsSpan(offset + 8, length);
            if (offset == 8 && type != "IHDR") return false;
            if (type == "IHDR")
            {
                if (offset != 8 || length != 13) return false;
                var w = BinaryPrimitives.ReadUInt32BigEndian(payload[..4]); var h = BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(4, 4));
                if (w is 0 or > 2048 || h is 0 or > 1024 || payload[8] != 8 || payload[10] != 0 || payload[11] != 0 || payload[12] != 0) return false;
                width = (int)w; height = (int)h;
                channels = payload[9] switch { 0 => 1, 2 => 3, 4 => 2, 6 => 4, _ => 0 };
                if (channels == 0) return false;
            }
            else if (type == "IDAT")
            {
                if (idatEnded) return false;
                sawIdat = true; compressed.Write(payload);
            }
            else if (type == "IEND")
            {
                if (length != 0 || offset + 12 != data.Length || !sawIdat) return false;
                try
                {
                    compressed.Position = 0;
                    using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
                    var row = new byte[1 + width * channels];
                    for (var y = 0; y < height; y++) { zlib.ReadExactly(row); if (row[0] > 4) return false; }
                    return zlib.ReadByte() == -1;
                }
                catch (Exception error) when (error is IOException or InvalidDataException) { return false; }
            }
            else
            {
                if (type == "acTL" || type[0] is >= 'A' and <= 'Z') return false;
                if (sawIdat) idatEnded = true;
            }
            offset += length + 12;
        }
        return false;
    }
    private static uint Crc(ReadOnlySpan<byte> bytes)
    {
        var crc = uint.MaxValue;
        foreach (var b in bytes) { crc ^= b; for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 1 ? 0xedb88320u : 0u); }
        return ~crc;
    }
}
public sealed class DoctorSignatureService(IDoctorSignatureRepository repository)
{
    private static Error? Guard(int doctorId, int actorId, string? role) =>
        actorId <= 0 || role is not (RoleCodes.Admin or RoleCodes.Dentist) ? Error.Forbidden :
        doctorId <= 0 ? Error.Validation : role == RoleCodes.Dentist && doctorId != actorId ? Error.NotFound : null;
    public Task<Result<DoctorSignatureStatus>> ReadAsync(int doctorId, int actorId, string? role, CancellationToken ct = default) =>
        Guard(doctorId, actorId, role) is { } error ? Task.FromResult(Result<DoctorSignatureStatus>.Failure(error)) : repository.ReadAsync(doctorId, actorId, role!, ct);
    public Task<Result<byte[]>> FileAsync(int doctorId, int version, int actorId, string? role, CancellationToken ct = default) =>
        Guard(doctorId, actorId, role) is { } error ? Task.FromResult(Result<byte[]>.Failure(error)) :
        version <= 0 ? Task.FromResult(Result<byte[]>.Failure(Error.Validation)) : repository.FileAsync(doctorId, version, actorId, role!, ct);
    public Task<Result<DoctorSignatureStatus>> SaveAsync(int doctorId, int actorId, string? role, DoctorSignatureRequest request, string? ip, CancellationToken ct = default)
    {
        if (Guard(doctorId, actorId, role) is { } error) return Task.FromResult(Result<DoctorSignatureStatus>.Failure(error));
        if (!new DoctorSignatureValidator().Validate(request).IsValid || SignaturePng.Decode(request.ImageBase64) is not { } bytes)
            return Task.FromResult(Result<DoctorSignatureStatus>.Failure(Error.Validation));
        return repository.SaveAsync(doctorId, actorId, role!, request.ExpectedVersion, bytes, ip, ct);
    }
}
