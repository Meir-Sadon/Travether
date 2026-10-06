using System.Globalization;
using System.Text.Json;

namespace Travether.Api.Places;

/// <summary>A place picked as a meeting point. <see cref="Area"/> is the coarse public label, e.g. "Old City, Chiang Mai".</summary>
public sealed record PlaceDto(string Name, string Area, double Lat, double Lng);

/// <summary>Place search for meeting points (PLAN.md §6 "Maps &amp; places"). Swappable once a map provider is chosen.</summary>
public interface IPlaceSearch
{
    Task<IReadOnlyList<PlaceDto>> SearchAsync(string query, double? nearLat, double? nearLng, CancellationToken ct);

    Task<PlaceDto?> ReverseAsync(double lat, double lng, CancellationToken ct);
}

public sealed class PlaceOptions
{
    /// <summary>Photon (OpenStreetMap) endpoint; the public instance needs no key.</summary>
    public string PhotonUrl { get; set; } = "https://photon.komoot.io";
}

/// <summary>Photon geocoder: free, OpenStreetMap data, no API key.</summary>
public sealed class PhotonPlaceSearch(HttpClient http) : IPlaceSearch
{
    public async Task<IReadOnlyList<PlaceDto>> SearchAsync(string query, double? nearLat, double? nearLng, CancellationToken ct)
    {
        var url = $"api/?limit=6&q={Uri.EscapeDataString(query)}";
        if (nearLat is { } lat && nearLng is { } lng)
        {
            url += string.Create(CultureInfo.InvariantCulture, $"&lat={lat}&lon={lng}");
        }

        return await FetchAsync(url, ct).ConfigureAwait(false);
    }

    public async Task<PlaceDto?> ReverseAsync(double lat, double lng, CancellationToken ct)
    {
        var found = await FetchAsync(string.Create(CultureInfo.InvariantCulture, $"reverse?lat={lat}&lon={lng}"), ct).ConfigureAwait(false);
        return found.Count > 0 ? found[0] : null;
    }

    private async Task<IReadOnlyList<PlaceDto>> FetchAsync(string url, CancellationToken ct)
    {
        using var res = await http.GetAsync(new Uri(url, UriKind.Relative), ct).ConfigureAwait(false);
        res.EnsureSuccessStatusCode();
        await using var stream = await res.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        var places = new List<PlaceDto>();
        foreach (var f in doc.RootElement.GetProperty("features").EnumerateArray())
        {
            var coords = f.GetProperty("geometry").GetProperty("coordinates");
            var p = f.GetProperty("properties");
            string? Get(string key) => p.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

            var street = Get("street") is { } s ? (Get("housenumber") is { } n ? $"{s} {n}" : s) : null;
            var name = Get("name") ?? street ?? Get("city");
            if (name is null)
            {
                continue;
            }

            places.Add(new PlaceDto(name, PlaceRules.AreaLabel(Get("district") ?? Get("locality"), Get("city") ?? Get("county"), Get("state"), Get("country")), coords[1].GetDouble(), coords[0].GetDouble()));
        }

        return places;
    }
}

public static class PlaceRules
{
    /// <summary>The two most specific parts of neighbourhood, city, region and country, e.g. "Old City, Chiang Mai".</summary>
    public static string AreaLabel(params string?[] parts)
    {
        var label = string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).Take(2));
        return label.Length > 120 ? label[..120] : label;
    }

    public static bool IsValid(double lat, double lng) => lat is >= -90 and <= 90 && lng is >= -180 and <= 180;
}

public static class PlaceSetup
{
    public const string RateLimit = "places";

    public static IServiceCollection AddTravetherPlaces(this IServiceCollection services, IConfiguration config)
    {
        var options = config.GetSection("Places").Get<PlaceOptions>() ?? new PlaceOptions();
        services.AddSingleton(options);
        services.AddHttpClient<IPlaceSearch, PhotonPlaceSearch>(http =>
        {
            http.BaseAddress = new Uri(options.PhotonUrl.TrimEnd('/') + "/");
            http.Timeout = TimeSpan.FromSeconds(8);
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Travether/1.0 (+https://travether.app)");
        });
        return services;
    }
}
