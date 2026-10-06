using System.Buffers.Binary;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Travether.Api.Domain;

namespace Travether.Api.Notifications;

public sealed class PushOptions
{
    /// <summary>VAPID public key: the uncompressed P-256 point, base64url. Generate with <c>npx web-push generate-vapid-keys</c>.</summary>
    public string? VapidPublicKey { get; set; }

    /// <summary>VAPID private key: the 32-byte P-256 scalar, base64url. A secret.</summary>
    public string? VapidPrivateKey { get; set; }

    /// <summary>Contact for push services, e.g. <c>mailto:hello@travether.app</c>.</summary>
    public string Subject { get; set; } = "mailto:hello@travether.app";

    public bool Enabled => !string.IsNullOrWhiteSpace(VapidPublicKey) && !string.IsNullOrWhiteSpace(VapidPrivateKey);
}

/// <summary>What the service worker shows. <see cref="Tag"/> makes a newer push replace an older one on the device.</summary>
public sealed record PushMessage(string Title, string Body, string Url, string Tag);

public enum PushResult { Sent, Gone, Failed }

public interface IWebPushSender
{
    bool Enabled { get; }

    string? PublicKey { get; }

    Task<PushResult> SendAsync(PushSubscription subscription, PushMessage message, CancellationToken ct);
}

/// <summary>
/// Web Push with VAPID (RFC 8292) and aes128gcm payload encryption (RFC 8291, RFC 8188), using only
/// the .NET crypto primitives. Push services answer 404/410 for subscriptions that no longer exist.
/// </summary>
public sealed class WebPushSender(HttpClient http, PushOptions options) : IWebPushSender
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public bool Enabled => options.Enabled;

    public string? PublicKey => options.VapidPublicKey;

    public async Task<PushResult> SendAsync(PushSubscription subscription, PushMessage message, CancellationToken ct)
    {
        if (!Enabled || !PushEndpoints.IsAllowed(subscription.Endpoint))
        {
            return PushResult.Failed;
        }

        var endpoint = new Uri(subscription.Endpoint);
        var body = PushEncryption.Encrypt(
            JsonSerializer.SerializeToUtf8Bytes(message, Json),
            WebEncoders.Base64UrlDecode(subscription.P256dh),
            WebEncoders.Base64UrlDecode(subscription.Auth));

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = new ByteArrayContent(body) };
        request.Content.Headers.ContentType = new("application/octet-stream");
        request.Content.Headers.ContentEncoding.Add("aes128gcm");
        request.Headers.TryAddWithoutValidation("TTL", "86400");
        request.Headers.TryAddWithoutValidation("Urgency", "normal");
        request.Headers.TryAddWithoutValidation("Topic", PushEncryption.Topic(message.Tag));
        request.Headers.TryAddWithoutValidation("Authorization", $"vapid t={Vapid.Token(endpoint, options)}, k={options.VapidPublicKey}");

        try
        {
            using var response = await http.SendAsync(request, ct).ConfigureAwait(false);
            return response.StatusCode switch
            {
                HttpStatusCode.NotFound or HttpStatusCode.Gone => PushResult.Gone,
                _ when response.IsSuccessStatusCode => PushResult.Sent,
                _ => PushResult.Failed,
            };
        }
        catch (HttpRequestException)
        {
            return PushResult.Failed;
        }
    }
}

/// <summary>
/// Subscriptions come from browsers, but the endpoint is a URL the server will POST to, so only the
/// known push services are accepted (no requests to arbitrary or internal hosts).
/// </summary>
public static class PushEndpoints
{
    private static readonly string[] Hosts = ["fcm.googleapis.com", "web.push.apple.com"];
    private static readonly string[] Suffixes = [".push.services.mozilla.com", ".notify.windows.com", ".push.apple.com"];

    public static bool IsAllowed(string endpoint) =>
        Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.IsDefaultPort
        && (Hosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase) || Suffixes.Any(s => uri.Host.EndsWith(s, StringComparison.OrdinalIgnoreCase)));
}

public static class Vapid
{
    /// <summary>An ES256 JWT for the push service's origin, valid for 12 hours.</summary>
    public static string Token(Uri endpoint, PushOptions options)
    {
        var publicKey = WebEncoders.Base64UrlDecode(options.VapidPublicKey!);
        using var key = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            D = WebEncoders.Base64UrlDecode(options.VapidPrivateKey!),
            Q = new ECPoint { X = publicKey[1..33], Y = publicKey[33..65] },
        });

        var header = WebEncoders.Base64UrlEncode("""{"typ":"JWT","alg":"ES256"}"""u8);
        var claims = WebEncoders.Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new
        {
            aud = endpoint.GetLeftPart(UriPartial.Authority),
            exp = DateTimeOffset.UtcNow.AddHours(12).ToUnixTimeSeconds(),
            sub = options.Subject,
        }));
        var signature = key.SignData(Encoding.ASCII.GetBytes($"{header}.{claims}"), HashAlgorithmName.SHA256);
        return $"{header}.{claims}.{WebEncoders.Base64UrlEncode(signature)}";
    }

    /// <summary>A fresh key pair (public, private), base64url; used when none is configured in Development.</summary>
    public static (string PublicKey, string PrivateKey) Generate()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = key.ExportParameters(includePrivateParameters: true);
        return (WebEncoders.Base64UrlEncode([0x04, .. p.Q.X!, .. p.Q.Y!]), WebEncoders.Base64UrlEncode(p.D!));
    }
}

/// <summary>RFC 8291 message encryption with the aes128gcm content coding, one record.</summary>
public static class PushEncryption
{
    private const int RecordSize = 4096;

    public static byte[] Encrypt(byte[] plaintext, byte[] userAgentPublicKey, byte[] authSecret, byte[]? salt = null, ECDiffieHellman? serverKey = null)
    {
        salt ??= RandomNumberGenerator.GetBytes(16);
        using var ephemeral = serverKey is null ? ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256) : null;
        var server = serverKey ?? ephemeral!;
        var serverParams = server.ExportParameters(false);
        byte[] serverPublic = [0x04, .. serverParams.Q.X!, .. serverParams.Q.Y!];

        using var userAgent = ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = userAgentPublicKey[1..33], Y = userAgentPublicKey[33..65] },
        });
        var shared = server.DeriveRawSecretAgreement(userAgent.PublicKey);

        var (key, nonce) = DeriveKeys(shared, authSecret, userAgentPublicKey, serverPublic, salt);

        // Padding delimiter 0x02 marks the last (only) record.
        byte[] padded = [.. plaintext, 0x02];
        var output = new byte[16 + 4 + 1 + serverPublic.Length + padded.Length + 16];
        salt.CopyTo(output, 0);
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(16, 4), RecordSize);
        output[20] = (byte)serverPublic.Length;
        serverPublic.CopyTo(output, 21);
        var cipherStart = 21 + serverPublic.Length;
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, padded, output.AsSpan(cipherStart, padded.Length), output.AsSpan(cipherStart + padded.Length, 16));
        return output;
    }

    /// <summary>The content-encryption key and nonce shared by sender and receiver.</summary>
    public static (byte[] Key, byte[] Nonce) DeriveKeys(byte[] sharedSecret, byte[] authSecret, byte[] userAgentPublicKey, byte[] serverPublicKey, byte[] salt)
    {
        byte[] keyInfo = [.. "WebPush: info\0"u8, .. userAgentPublicKey, .. serverPublicKey];
        var ikm = HKDF.DeriveKey(HashAlgorithmName.SHA256, sharedSecret, 32, authSecret, keyInfo);
        var key = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 16, salt, "Content-Encoding: aes128gcm\0"u8.ToArray());
        var nonce = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 12, salt, "Content-Encoding: nonce\0"u8.ToArray());
        return (key, nonce);
    }

    /// <summary>Push services allow a topic of up to 32 URL-safe characters; a newer message with the same topic replaces an undelivered one.</summary>
    public static string Topic(string tag) => WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.UTF8.GetBytes(tag)))[..32];
}
