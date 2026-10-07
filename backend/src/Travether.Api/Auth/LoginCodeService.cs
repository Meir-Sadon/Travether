using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Travether.Api.Data;
using Travether.Api.Domain;
using Travether.Api.Email;

namespace Travether.Api.Auth;

public enum CodeCheck { Ok, Invalid, Expired, TooManyAttempts }

/// <summary>Issues and checks six-digit email codes (sign-in, email verification, password reset, account deletion).</summary>
public sealed class LoginCodeService(TravetherDbContext db, IEmailSender email, TokenService tokens, TimeProvider clock)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
    public const int MaxAttempts = 5;

    /// <summary>Per address and purpose, so a mailbox can't be flooded.</summary>
    public const int MaxCodesPerHour = 5;

    /// <summary>Sends a fresh code. Returns false when the hourly limit for this address is reached.</summary>
    public async Task<bool> SendAsync(string emailAddress, LoginCodePurpose purpose, CancellationToken ct = default)
    {
        var address = NormalizeEmail(emailAddress);
        var now = clock.GetUtcNow();
        var recent = await db.LoginCodes.CountAsync(c => c.Email == address && c.Purpose == purpose && c.CreatedAt > now.AddHours(-1), ct).ConfigureAwait(false);
        if (recent >= MaxCodesPerHour)
        {
            return false;
        }

        // Only the newest code works: older open codes for the same purpose are retired.
        await db.LoginCodes
            .Where(c => c.Email == address && c.Purpose == purpose && c.ConsumedAt == null)
            .ExecuteUpdateAsync(u => u.SetProperty(c => c.ConsumedAt, now), ct).ConfigureAwait(false);

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        db.LoginCodes.Add(new LoginCode
        {
            Id = Guid.NewGuid(),
            Email = address,
            Purpose = purpose,
            CodeHash = Hash(address, purpose, code),
            CreatedAt = now,
            ExpiresAt = now.Add(Lifetime),
        });
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        var (subject, intro) = purpose switch
        {
            LoginCodePurpose.ResetPassword => ("Reset your Travether password", "Use this code to set a new password:"),
            LoginCodePurpose.VerifyEmail => ("Confirm your email for Travether", "Use this code to confirm your email address:"),
            LoginCodePurpose.DeleteAccount => ("Confirm deleting your Travether account", "Use this code to confirm that you want to delete your account. This can't be undone:"),
            _ => ("Your Travether sign-in code", "Use this code to continue to Travether:"),
        };
        await email.SendAsync(new EmailMessage(
            address,
            $"{code} · {subject}",
            $"{intro}\n\n{code}\n\nIt expires in {Lifetime.TotalMinutes:0} minutes. If you didn't ask for it, you can ignore this email."), ct).ConfigureAwait(false);
        return true;
    }

    /// <summary>Checks a code and consumes it on success. Wrong guesses count against the code.</summary>
    public async Task<CodeCheck> VerifyAsync(string emailAddress, LoginCodePurpose purpose, string code, CancellationToken ct = default)
    {
        var address = NormalizeEmail(emailAddress);
        var now = clock.GetUtcNow();
        var row = await db.LoginCodes
            .Where(c => c.Email == address && c.Purpose == purpose && c.ConsumedAt == null)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (row is null)
        {
            return CodeCheck.Invalid;
        }

        if (row.ExpiresAt < now)
        {
            return CodeCheck.Expired;
        }

        if (row.Attempts >= MaxAttempts)
        {
            return CodeCheck.TooManyAttempts;
        }

        var expected = Encoding.ASCII.GetBytes(row.CodeHash);
        var actual = Encoding.ASCII.GetBytes(Hash(address, purpose, code.Trim()));
        if (!CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            row.Attempts++;
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return row.Attempts >= MaxAttempts ? CodeCheck.TooManyAttempts : CodeCheck.Invalid;
        }

        row.ConsumedAt = now;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return CodeCheck.Ok;
    }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private string Hash(string address, LoginCodePurpose purpose, string code)
    {
        var input = Encoding.UTF8.GetBytes($"{address}|{purpose}|{code}");
        return Convert.ToHexString(HMACSHA256.HashData(tokens.Key.Key, input));
    }
}
