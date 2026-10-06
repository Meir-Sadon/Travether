using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Travether.Api.Auth;
using Travether.Api.Data;
using Travether.Api.Domain;

namespace Travether.Api.Safety;

/// <summary>
/// Keeps banned people from simply signing up again (PLAN.md §4.8): at ban time the account's email,
/// phone and devices are stored as keyed hashes, and sign-up refuses any match.
/// </summary>
public sealed class BanGuard(TravetherDbContext db, TokenService tokens, TimeProvider clock)
{
    public const string DeviceCookie = "tv_device";

    /// <summary>
    /// The address as one mailbox: lower-case, without a "+tag", and for Gmail without dots
    /// (j.o.e+x@googlemail.com and joe@gmail.com are the same inbox).
    /// </summary>
    public static string CanonicalEmail(string email)
    {
        var lower = email.Trim().ToLowerInvariant();
        var at = lower.LastIndexOf('@');
        if (at < 1)
        {
            return lower;
        }

        var local = lower[..at];
        var domain = lower[(at + 1)..];
        var plus = local.IndexOf('+', StringComparison.Ordinal);
        if (plus >= 0)
        {
            local = local[..plus];
        }

        if (domain is "gmail.com" or "googlemail.com")
        {
            local = local.Replace(".", "", StringComparison.Ordinal);
            domain = "gmail.com";
        }

        return $"{local}@{domain}";
    }

    public static string CanonicalPhone(string phone) => new([.. phone.Where(char.IsAsciiDigit)]);

    public string Hash(BannedIdentifierKind kind, string value) =>
        Convert.ToHexString(HMACSHA256.HashData(tokens.Key.Key, Encoding.UTF8.GetBytes($"ban:{kind}:{value}")));

    /// <summary>The browser's device id from its long-lived cookie, issuing one when missing.</summary>
    public static string DeviceId(HttpContext http)
    {
        if (http.Request.Cookies.TryGetValue(DeviceCookie, out var id) && id.Length is >= 16 and <= 64)
        {
            return id;
        }

        id = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        http.Response.Cookies.Append(DeviceCookie, id, new CookieOptions
        {
            HttpOnly = true,
            Secure = http.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/api/auth",
            Expires = DateTimeOffset.UtcNow.AddYears(2),
            IsEssential = true,
        });
        return id;
    }

    /// <summary>Remembers that this account used this device (for a later ban).</summary>
    public async Task RecordDeviceAsync(Guid userId, string deviceId, CancellationToken ct)
    {
        var hash = Hash(BannedIdentifierKind.Device, deviceId);
        var now = clock.GetUtcNow();
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO user_devices (user_id, device_hash, last_seen_at) VALUES ({userId}, {hash}, {now})
            ON CONFLICT (user_id, device_hash) DO UPDATE SET last_seen_at = EXCLUDED.last_seen_at
            """, ct).ConfigureAwait(false);
    }

    /// <summary>True when the email or this device belongs to a banned account.</summary>
    public async Task<bool> IsBannedAsync(string email, string deviceId, CancellationToken ct)
    {
        var hashes = new[] { Hash(BannedIdentifierKind.Email, CanonicalEmail(email)), Hash(BannedIdentifierKind.Device, deviceId) };
        return await db.BannedIdentifiers.AnyAsync(b => hashes.Contains(b.Hash), ct).ConfigureAwait(false);
    }

    public async Task<bool> IsPhoneBannedAsync(string phone, CancellationToken ct)
    {
        var hash = Hash(BannedIdentifierKind.Phone, CanonicalPhone(phone));
        return await db.BannedIdentifiers.AnyAsync(b => b.Hash == hash, ct).ConfigureAwait(false);
    }

    /// <summary>Bans the account: it counts as absent everywhere, its sessions end, and its identifiers are remembered.</summary>
    public async Task BanAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.FirstAsync(u => u.Id == userId, ct).ConfigureAwait(false);
        var now = clock.GetUtcNow();
        user.BannedAt ??= now;
        user.SessionVersion++;

        var hashes = new List<(string Hash, BannedIdentifierKind Kind)> { (Hash(BannedIdentifierKind.Email, CanonicalEmail(user.Email)), BannedIdentifierKind.Email) };
        if (!string.IsNullOrWhiteSpace(user.Phone))
        {
            hashes.Add((Hash(BannedIdentifierKind.Phone, CanonicalPhone(user.Phone)), BannedIdentifierKind.Phone));
        }

        hashes.AddRange((await db.UserDevices.Where(d => d.UserId == userId).Select(d => d.DeviceHash).ToListAsync(ct).ConfigureAwait(false))
            .Select(h => (h, BannedIdentifierKind.Device)));
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        foreach (var (hash, kind) in hashes)
        {
            await db.Database.ExecuteSqlAsync(
                $"INSERT INTO banned_identifiers (hash, kind, user_id, created_at) VALUES ({hash}, {EnumText.ToDb(kind)}, {userId}, {now}) ON CONFLICT (hash) DO NOTHING",
                ct).ConfigureAwait(false);
        }
    }

    /// <summary>Lifts a ban and forgets the identifiers it recorded.</summary>
    public async Task UnbanAsync(Guid userId, CancellationToken ct)
    {
        await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(u => u.SetProperty(x => x.BannedAt, (DateTimeOffset?)null), ct).ConfigureAwait(false);
        await db.BannedIdentifiers.Where(b => b.UserId == userId).ExecuteDeleteAsync(ct).ConfigureAwait(false);
    }
}
