namespace Travether.Api.Images;

/// <summary>
/// Development fallback: writes images under <c>Images:LocalPath</c> (default <c>uploads/</c> next to the
/// app) and serves them at <c>/uploads/…</c>. Render's disk is ephemeral, so production uses Cloudinary.
/// </summary>
public sealed class LocalImageStore(string root) : IImageStore
{
    public const string RequestPath = "/uploads";

    public string Root { get; } = root;

    public async Task<string> SaveAsync(Stream content, ImageKind kind, string folder, CancellationToken ct = default)
    {
        var dir = Path.Combine(Root, folder);
        Directory.CreateDirectory(dir);
        var name = $"{Guid.NewGuid():N}.{ImageRules.Extension(kind)}";
        await using (var file = File.Create(Path.Combine(dir, name)))
        {
            await content.CopyToAsync(file, ct).ConfigureAwait(false);
        }

        return $"{RequestPath}/{folder}/{name}";
    }

    public Task DeleteAsync(string url, CancellationToken ct = default)
    {
        if (!url.StartsWith(RequestPath + "/", StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        var relative = url[(RequestPath.Length + 1)..];
        var full = Path.GetFullPath(Path.Combine(Root, relative));
        if (full.StartsWith(Path.GetFullPath(Root), StringComparison.Ordinal) && File.Exists(full))
        {
            File.Delete(full);
        }

        return Task.CompletedTask;
    }
}
