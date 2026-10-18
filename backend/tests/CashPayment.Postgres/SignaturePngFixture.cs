using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
// Synthetic pixels for isolated tests only.
internal static class SignaturePngFixture
{
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
}
