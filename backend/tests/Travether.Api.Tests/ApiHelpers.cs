using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Travether.Api.Auth;

namespace Travether.Api.Tests;

/// <summary>Shared helpers for HTTP-level tests.</summary>
public static class ApiHelpers
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"{(int)res.StatusCode}: {body}");
        }

        return JsonSerializer.Deserialize<T>(body, Json)!;
    }

    /// <summary>The <c>code</c> of an error response.</summary>
    public static async Task<string?> ErrorCodeAsync(this HttpResponseMessage res)
    {
        var doc = await res.Content.ReadFromJsonAsync<JsonElement>();
        return doc.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    public static Task<HttpResponseMessage> PostJsonAsync(this HttpClient client, string url, object body) =>
        client.PostAsJsonAsync(url, body, Json);

    public static Task<HttpResponseMessage> PatchJsonAsync(this HttpClient client, string url, object body) =>
        client.PatchAsJsonAsync(url, body, Json);

    public static Task<HttpResponseMessage> PutJsonAsync(this HttpClient client, string url, object body) =>
        client.PutAsJsonAsync(url, body, Json);

    public static string NewEmail(string name) => $"{name.ToLowerInvariant()}-{Guid.NewGuid():N}@example.com";

    /// <summary>Registers a new adult traveler with email + password and returns their signed-in client.</summary>
    public static async Task<(HttpClient Client, AuthResultDto Auth)> SignUpAsync(this ApiFactory factory, string name, string country = "IL")
    {
        var client = factory.CreateApiClient();
        var res = await client.PostJsonAsync("/api/auth/register", new
        {
            email = NewEmail(name),
            password = "correct horse battery",
            displayName = name,
            fullName = $"{name} Example",
            dateOfBirth = "1995-05-05",
            countryCode = country,
            acceptTerms = true,
        });
        return (client, await res.ReadAsync<AuthResultDto>());
    }
}
