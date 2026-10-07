using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Travether.Api.Api;
using Travether.Api.Auth;
using Travether.Api.Data;
using Travether.Api.Domain;
using Travether.Api.Privacy;

namespace Travether.Api.Controllers;

/// <summary>Consents, data export and account deletion in self-service (PLAN.md §4.9).</summary>
[ApiController]
[Authorize]
[Route("api/me")]
public sealed class PrivacyController(
    TravetherDbContext db,
    PrivacyService privacy,
    LoginCodeService codes,
    IOptions<JsonOptions> jsonOptions,
    TimeProvider clock) : ControllerBase
{
    private static readonly PasswordHasher<User> Hasher = new();

    private Guid Me => User.RequireUserId();

    [HttpGet("privacy")]
    public Task<PrivacyDto> Get(CancellationToken ct) => privacy.GetAsync(Me, ct);

    /// <summary>Accepts the current terms, privacy policy and community guidelines.</summary>
    [HttpPost("consents/legal")]
    public async Task<PrivacyDto> AcceptLegal(CancellationToken ct)
    {
        await privacy.AcceptLegalAsync(Me, ct).ConfigureAwait(false);
        return await privacy.GetAsync(Me, ct).ConfigureAwait(false);
    }

    /// <summary>Grants or withdraws an optional consent (analytics, marketing email).</summary>
    [HttpPut("consents")]
    public async Task<IActionResult> SetOptional(OptionalConsentInput input, CancellationToken ct)
    {
        if (!PrivacyRules.Optional.Contains(input.Kind))
        {
            return ApiError.BadRequest("ConsentNotOptional");
        }

        await privacy.SetOptionalAsync(Me, input.Kind, input.Granted, ct).ConfigureAwait(false);
        return Ok(await privacy.GetAsync(Me, ct).ConfigureAwait(false));
    }

    /// <summary>Downloads everything stored about the signed-in user as JSON.</summary>
    [HttpGet("export")]
    [EnableRateLimiting(PrivacySetup.ExportRateLimit)]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var json = new JsonSerializerOptions(jsonOptions.Value.JsonSerializerOptions) { WriteIndented = true };
        var bytes = await privacy.ExportAsync(Me, json, ct).ConfigureAwait(false);
        var date = clock.GetUtcNow().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        Response.Headers.CacheControl = "no-store";
        return File(bytes, "application/json", $"travether-data-{date}.json");
    }

    /// <summary>Emails a code that confirms deleting the account (for accounts without a password, or a forgotten one).</summary>
    [HttpPost("delete/code")]
    public async Task<IActionResult> SendDeleteCode(CancellationToken ct)
    {
        var address = await db.Users.Where(u => u.Id == Me).Select(u => u.Email).FirstAsync(ct).ConfigureAwait(false);
        return await codes.SendAsync(address, LoginCodePurpose.DeleteAccount, ct).ConfigureAwait(false)
            ? Accepted()
            : ApiError.TooManyRequests("TooManyCodes");
    }

    /// <summary>Deletes the account for good, confirmed by the password or an emailed code, and signs out.</summary>
    [HttpPost("delete")]
    [EnableRateLimiting(AuthSetup.AuthRateLimit)]
    public async Task<IActionResult> Delete(DeleteAccountInput input, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == Me, ct).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(input.Code))
        {
            var check = await codes.VerifyAsync(user.Email, LoginCodePurpose.DeleteAccount, input.Code, ct).ConfigureAwait(false);
            if (check != CodeCheck.Ok)
            {
                return check switch
                {
                    CodeCheck.Expired => ApiError.BadRequest("CodeExpired"),
                    CodeCheck.TooManyAttempts => ApiError.TooManyRequests("TooManyAttempts"),
                    _ => ApiError.BadRequest("InvalidCode"),
                };
            }
        }
        else if (!string.IsNullOrEmpty(input.Password))
        {
            if (user.PasswordHash is null || Hasher.VerifyHashedPassword(user, user.PasswordHash, input.Password) == PasswordVerificationResult.Failed)
            {
                return ApiError.BadRequest("WrongPassword");
            }
        }
        else
        {
            return ApiError.BadRequest("ConfirmationRequired");
        }

        await privacy.DeleteAccountAsync(user.Id, ct).ConfigureAwait(false);
        SessionCookie.SignOut(HttpContext);
        return NoContent();
    }
}
