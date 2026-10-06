using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Travether.Api.Images;

/// <summary>Credentials parsed from <c>cloudinary://api_key:api_secret@cloud_name</c>.</summary>
public sealed record CloudinaryAccount(string CloudName, string ApiKey, string ApiSecret)
{
    public static CloudinaryAccount? Parse(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "cloudinary")
        {
            return null;
        }

        var parts = Uri.UnescapeDataString(uri.UserInfo).Split(':', 2);
        return parts.Length == 2 && uri.Host.Length > 0 ? new CloudinaryAccount(uri.Host, parts[0], parts[1]) : null;
    }
}

/// <summary>Signed uploads through Cloudinary's REST API (no SDK). Images are capped at 1600 px on upload.</summary>
public sealed class CloudinaryImageStore(HttpClient http, CloudinaryAccount account, TimeProvider clock) : IImageStore
{
    private const string Transformation = "c_limit,w_1600,h_1600";

    public async Task<string> SaveAsync(Stream content, ImageKind kind, string folder, CancellationToken ct = default)
    {
        var timestamp = clock.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var fullFolder = $"travether/{folder}";
        var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["folder"] = fullFolder,
            ["timestamp"] = timestamp,
            ["transformation"] = Transformation,
        };

        using var form = new MultipartFormDataContent();
        foreach (var (key, value) in parameters)
        {
            form.Add(new StringContent(value), key);
        }

        form.Add(new StringContent(account.ApiKey), "api_key");
        form.Add(new StringContent(Sign(parameters)), "signature");
        form.Add(new StreamContent(content), "file", $"upload.{ImageRules.Extension(kind)}");

        using var res = await http.PostAsync(new Uri($"https://api.cloudinary.com/v1_1/{account.CloudName}/image/upload"), form, ct).ConfigureAwait(false);
        res.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await res.Content.ReadAsStreamAsync(ct).ConfigureAwait(false), cancellationToken: ct).ConfigureAwait(false);
        return doc.RootElement.GetProperty("secure_url").GetString()!;
    }

    public async Task DeleteAsync(string url, CancellationToken ct = default)
    {
        var publicId = PublicIdFrom(url);
        if (publicId is null)
        {
            return;
        }

        var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["public_id"] = publicId,
            ["timestamp"] = clock.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
        };
        using var form = new FormUrlEncodedContent(parameters
            .Append(new("api_key", account.ApiKey))
            .Append(new("signature", Sign(parameters))));
        using var res = await http.PostAsync(new Uri($"https://api.cloudinary.com/v1_1/{account.CloudName}/image/destroy"), form, ct).ConfigureAwait(false);
    }

    /// <summary>https://res.cloudinary.com/&lt;cloud&gt;/image/upload/v123/travether/avatars/abc.jpg → travether/avatars/abc.</summary>
    public string? PublicIdFrom(string url)
    {
        var marker = $"res.cloudinary.com/{account.CloudName}/image/upload/";
        var at = url.IndexOf(marker, StringComparison.Ordinal);
        if (at < 0)
        {
            return null;
        }

        var path = url[(at + marker.Length)..];
        var segments = path.Split('/').SkipWhile(s => !s.StartsWith("travether", StringComparison.Ordinal)).ToArray();
        if (segments.Length == 0)
        {
            return null;
        }

        var joined = string.Join('/', segments);
        var dot = joined.LastIndexOf('.');
        return dot > 0 ? joined[..dot] : joined;
    }

    private string Sign(IEnumerable<KeyValuePair<string, string>> parameters)
    {
        var toSign = string.Join('&', parameters.Select(p => $"{p.Key}={p.Value}")) + account.ApiSecret;
#pragma warning disable CA5350 // Cloudinary's signature scheme is SHA-1; it isn't our choice of hash.
        return Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes(toSign)));
#pragma warning restore CA5350
    }
}

public static class ImageSetup
{
    public static IServiceCollection AddTravetherImages(this IServiceCollection services, IConfiguration config, IHostEnvironment env)
    {
        var account = CloudinaryAccount.Parse(config["Cloudinary:Url"]);
        if (account is not null)
        {
            services.AddSingleton(account);
            services.AddHttpClient<IImageStore, CloudinaryImageStore>();
        }
        else
        {
            var root = config["Images:LocalPath"] ?? Path.Combine(env.ContentRootPath, "uploads");
            services.AddSingleton(new LocalImageStore(root));
            services.AddSingleton<IImageStore>(sp => sp.GetRequiredService<LocalImageStore>());
        }

        return services;
    }

    /// <summary>Serves the local fallback folder at /uploads.</summary>
    public static void UseLocalImages(this WebApplication app)
    {
        if (app.Services.GetService<LocalImageStore>() is { } local)
        {
            Directory.CreateDirectory(local.Root);
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(local.Root),
                RequestPath = LocalImageStore.RequestPath,
            });
        }
    }
}
