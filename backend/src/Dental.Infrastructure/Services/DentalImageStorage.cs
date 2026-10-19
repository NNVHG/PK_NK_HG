using Dental.Application.Features.MOD_IMG;
namespace Dental.Infrastructure.Services;

public sealed class DentalImageStorage(string contentRoot) : IDentalImageStorage
{
    private readonly string root = Path.GetFullPath(Path.Combine(contentRoot, "uploads", "images"));
    private string Resolve(string path)
    {
        var parts = path.Split('/');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var id) || id <= 0 ||
            !Guid.TryParseExact(Path.GetFileNameWithoutExtension(parts[1]), "N", out _) ||
            Path.GetExtension(parts[1]) is not (".jpg" or ".png" or ".webp")) throw new InvalidOperationException("Invalid image storage key.");
        var full = Path.GetFullPath(Path.Combine(root, parts[0], parts[1]));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Invalid image storage path.");
        return full;
    }
    public async Task WriteAsync(string relativePath, byte[] bytes, CancellationToken ct)
    {
        var path = Resolve(relativePath); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
        await stream.WriteAsync(bytes, ct);
    }
    public Task RemoveAsync(string relativePath) { File.Delete(Resolve(relativePath)); return Task.CompletedTask; }
    public Task<Stream> OpenReadAsync(string relativePath, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<Stream>(new FileStream(Resolve(relativePath), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan));
    }
}
