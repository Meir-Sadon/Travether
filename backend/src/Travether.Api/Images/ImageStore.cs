namespace Travether.Api.Images;

/// <summary>
/// Stores uploaded images (profile photos, card covers) and returns a public URL.
/// Cloudinary in production; a local folder served at /uploads when Cloudinary isn't configured.
/// </summary>
public interface IImageStore
{
    /// <param name="folder">Logical folder, e.g. "avatars" or "covers".</param>
    Task<string> SaveAsync(Stream content, ImageKind kind, string folder, CancellationToken ct = default);

    /// <summary>Best effort: removes an image this store created. Unknown URLs are ignored.</summary>
    Task DeleteAsync(string url, CancellationToken ct = default);
}

public enum ImageKind { Jpeg, Png, Webp }

public static class ImageRules
{
    public const long MaxBytes = 5 * 1024 * 1024;

    /// <summary>Identifies the format from the file's first bytes; the client's content type isn't trusted.</summary>
    public static ImageKind? Sniff(ReadOnlySpan<byte> head)
    {
        if (head.Length >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF)
        {
            return ImageKind.Jpeg;
        }

        if (head.Length >= 8 && head[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return ImageKind.Png;
        }

        if (head.Length >= 12 && head[..4].SequenceEqual("RIFF"u8) && head[8..12].SequenceEqual("WEBP"u8))
        {
            return ImageKind.Webp;
        }

        return null;
    }

    public static string Extension(ImageKind kind) => kind switch
    {
        ImageKind.Png => "png",
        ImageKind.Webp => "webp",
        _ => "jpg",
    };

    /// <summary>
    /// Reads an upload into memory after checking size and format. Returns null with an error code
    /// (<c>ImageTooLarge</c>, <c>UnsupportedImage</c>) when it isn't acceptable.
    /// </summary>
    public static async Task<(MemoryStream? Data, ImageKind Kind, string? Error)> ReadAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return (null, default, "ImageRequired");
        }

        if (file.Length > MaxBytes)
        {
            return (null, default, "ImageTooLarge");
        }

        var data = new MemoryStream();
        await using (var input = file.OpenReadStream())
        {
            await input.CopyToAsync(data, ct).ConfigureAwait(false);
        }

        var kind = Sniff(data.GetBuffer().AsSpan(0, (int)Math.Min(16, data.Length)));
        if (kind is null)
        {
            await data.DisposeAsync().ConfigureAwait(false);
            return (null, default, "UnsupportedImage");
        }

        data.Position = 0;
        return (data, kind.Value, null);
    }
}
