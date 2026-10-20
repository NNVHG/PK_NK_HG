using System.Buffers.Binary;
namespace Dental.Application.Features.MOD_IMG;

// Inspect bounded file headers/container structure; never decode a large raster into server memory.
public static class DentalImageFile
{
    public const int MaxBytes = 10 * 1024 * 1024;
    public static bool Magic(ReadOnlySpan<byte> b, string type) => b.Length >= 12 && type switch
    {
        "image/png" => b[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
        "image/jpeg" => b[0] == 255 && b[1] == 216 && b[2] == 255,
        "image/webp" => b[..4].SequenceEqual("RIFF"u8) && b.Slice(8, 4).SequenceEqual("WEBP"u8),
        _ => false
    };
    public static (int Width, int Height)? Inspect(string name, string type, byte[] data)
    {
        if (data.Length > MaxBytes || !Magic(data, type)) return null;
        var ext = Path.GetExtension(name).ToLowerInvariant();
        if (!(type == "image/jpeg" && ext is ".jpg" or ".jpeg" || type == "image/png" && ext == ".png" || type == "image/webp" && ext == ".webp")) return null;
        var b = data.AsSpan();
        var size = type switch { "image/png" => Png(b), "image/jpeg" => Jpeg(b), _ => Webp(b) };
        return size is { Width: > 0 and <= 4096, Height: > 0 and <= 4096 } ? size : null;
    }
    private static (int Width, int Height)? Png(ReadOnlySpan<byte> b)
    {
        if (b.Length < 45 || BinaryPrimitives.ReadUInt32BigEndian(b.Slice(8, 4)) != 13 || !b.Slice(12, 4).SequenceEqual("IHDR"u8)) return null;
        var w = BinaryPrimitives.ReadUInt32BigEndian(b.Slice(16, 4)); var h = BinaryPrimitives.ReadUInt32BigEndian(b.Slice(20, 4));
        if (w is 0 or > 4096 || h is 0 or > 4096) return null;
        var at = 8; var idat = false;
        while (at <= b.Length - 12)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(b.Slice(at, 4));
            if (length > (uint)(b.Length - at - 12)) return null;
            var kind = b.Slice(at + 4, 4);
            if (kind.SequenceEqual("IDAT"u8) && length > 0) idat = true;
            if (kind.SequenceEqual("IEND"u8)) return idat && length == 0 && at + 12 == b.Length ? ((int)w, (int)h) : null;
            at += (int)length + 12;
        }
        return null;
    }
    private static (int Width, int Height)? Jpeg(ReadOnlySpan<byte> b)
    {
        if (b.Length < 15 || b[^2] != 255 || b[^1] != 217) return null;
        var at = 2; (int Width, int Height)? size = null;
        while (at < b.Length - 3)
        {
            if (b[at++] != 255) return null;
            while (at < b.Length && b[at] == 255) at++;
            if (at >= b.Length - 2) return null;
            var marker = b[at++];
            if (marker == 218) return size;
            if (marker is 0 or 216 or 217) return null;
            var length = BinaryPrimitives.ReadUInt16BigEndian(b.Slice(at, 2));
            if (length < 2 || length > b.Length - at) return null;
            if (marker is 192 or 193 or 194)
            {
                if (length < 8) return null;
                size = (BinaryPrimitives.ReadUInt16BigEndian(b.Slice(at + 5, 2)), BinaryPrimitives.ReadUInt16BigEndian(b.Slice(at + 3, 2)));
            }
            at += length;
        }
        return null;
    }
    private static (int Width, int Height)? Webp(ReadOnlySpan<byte> b)
    {
        if (b.Length < 30 || BinaryPrimitives.ReadUInt32LittleEndian(b.Slice(4, 4)) != b.Length - 8) return null;
        var at = 12; (int Width, int Height)? size = null; var pixels = false;
        while (at <= b.Length - 8)
        {
            var n = BinaryPrimitives.ReadUInt32LittleEndian(b.Slice(at + 4, 4));
            if (n > (uint)(b.Length - at - 8)) return null;
            var kind = b.Slice(at, 4); var p = b.Slice(at + 8, (int)n);
            if (kind.SequenceEqual("VP8X"u8) && p.Length == 10) size = (1 + U24(p.Slice(4, 3)), 1 + U24(p.Slice(7, 3)));
            if (kind.SequenceEqual("VP8 "u8) && p.Length >= 10 && p.Slice(3, 3).SequenceEqual(new byte[] {157, 1, 42}))
            {
                var dimensions = (Width: BinaryPrimitives.ReadUInt16LittleEndian(p.Slice(6, 2)) & 0x3fff, Height: BinaryPrimitives.ReadUInt16LittleEndian(p.Slice(8, 2)) & 0x3fff);
                if (dimensions is not { Width: > 0 and <= 4096, Height: > 0 and <= 4096 }) return null;
                size ??= dimensions; pixels = true;
            }
            if (kind.SequenceEqual("VP8L"u8) && p.Length >= 5 && p[0] == 47)
            {
                var bits = BinaryPrimitives.ReadUInt32LittleEndian(p.Slice(1, 4)); var dimensions = (Width: (int)(bits & 0x3fff) + 1, Height: (int)((bits >> 14) & 0x3fff) + 1);
                if (dimensions is not { Width: > 0 and <= 4096, Height: > 0 and <= 4096 }) return null;
                size ??= dimensions; pixels = true;
            }
            if (kind.SequenceEqual("ANMF"u8) && p.Length >= 16)
            {
                if (1 + U24(p.Slice(6, 3)) > 4096 || 1 + U24(p.Slice(9, 3)) > 4096) return null;
                pixels = true;
            }
            at += 8 + (int)n + (int)(n & 1);
        }
        return at == b.Length && pixels ? size : null;
    }
    private static int U24(ReadOnlySpan<byte> p) => p[0] | p[1] << 8 | p[2] << 16;
}
