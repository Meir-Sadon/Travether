using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Travether.Api.Api;
using Travether.Api.Auth;
using Travether.Api.Authorization;
using Travether.Api.Data;
using Travether.Api.Domain;
using Travether.Api.Privacy;
using Travether.Api.Profiles;
using Travether.Api.Safety;

namespace Travether.Api.Controllers;

/// <summary>
/// Sign-up and sign-in (PLAN.md §4.1, decision 2): email + password, email one-time code, and
/// Google/Apple. Identity is verified first; accounts are created only after the 18+ profile step.
/// </summary>
[ApiController]
[Route("api/auth")]
[EnableRateLimiting(AuthSetup.AuthRateLimit)]
public sealed class AuthController(
    TravetherDbContext db,
    LoginCodeService codes,
    TokenService tokens,
    SessionCookie session,
    IExternalIdentityVerifier external,
    AuthOptions options,
    BanGuard bans,
    PrivacyService privacy,
    TimeProvider clock) : ControllerBase
{
    private static readonly PasswordHasher<User> Hasher = new();

    // Verified against when the email is unknown, so a miss takes as long as a wrong password.
    private static readonly string DummyHash = Hasher.HashPassword(null!, "not-a-real-password");

    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    [HttpGet("providers")]
    public ProvidersDto Providers() => new(
        external.IsEnabled(ExternalProvider.Google) ? options.GoogleClientId : null,
        external.IsEnabled(ExternalProvider.Apple) ? options.AppleClientId : null,
        external.IsEnabled(ExternalProvider.Apple) ? options.AppleRedirectUri : null);

    /// <summary>The current session. Visitors get <c>{ user: null }</c> rather than an error.</summary>
    [HttpGet("me")]
    public async Task<SessionDto> Me(CancellationToken ct)
    {
        var id = User.GetUserId();
        var user = id is null ? null : await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct).ConfigureAwait(false);
        if (user is null)
        {
            return new SessionDto(null);
        }

        var (needsConsent, analytics) = await privacy.SessionFlagsAsync(user.Id, ct).ConfigureAwait(false);
        return new SessionDto(MeDto.From(user, Today), needsConsent, analytics);
    }

    /// <summary>Emails a sign-in code. Always 202, whether or not an account exists (no account enumeration).</summary>
    [HttpPost("email/start")]
    public async Task<IActionResult> EmailStart(EmailStartRequest req, CancellationToken ct)
    {
        if (!await codes.SendAsync(req.Email, LoginCodePurpose.SignIn, ct).ConfigureAwait(false))
        {
            return ApiError.TooManyRequests("TooManyCodes");
        }

        return Accepted();
    }

    /// <summary>Signs in an existing account, or returns a sign-up token for a new one.</summary>
    [HttpPost("email/verify")]
    public async Task<IActionResult> EmailVerify(EmailVerifyRequest req, CancellationToken ct)
    {
        var check = await codes.VerifyAsync(req.Email, LoginCodePurpose.SignIn, req.Code, ct).ConfigureAwait(false);
        if (check != CodeCheck.Ok)
        {
            return CodeError(check);
        }

        var email = LoginCodeService.NormalizeEmail(req.Email);
        var user = await FindByEmailAsync(email, ct).ConfigureAwait(false);
        if (user is null)
        {
            return Ok(AuthResultDto.NeedsProfile(tokens.CreateSignup(new SignupClaims(email, null, null, null)), email, null));
        }

        if (!AccessRules.IsActive(user))
        {
            return ApiError.Forbidden("AccountSuspended");
        }

        ClaimAddress(user);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return await SignInAsync(user, ct).ConfigureAwait(false);
    }

    [HttpPost("external")]
    public async Task<IActionResult> External(ExternalSignInRequest req, CancellationToken ct)
    {
        if (!external.IsEnabled(req.Provider))
        {
            return ApiError.BadRequest("ProviderDisabled");
        }

        var identity = await external.VerifyAsync(req.Provider, req.IdToken, ct).ConfigureAwait(false);
        if (identity is null)
        {
            return ApiError.Unauthorized("InvalidIdToken");
        }

        var linked = await db.ExternalLogins.Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Provider == identity.Provider && x.Subject == identity.Subject, ct).ConfigureAwait(false);
        if (linked is not null)
        {
            return AccessRules.IsActive(linked.User) ? await SignInAsync(linked.User, ct).ConfigureAwait(false) : ApiError.Forbidden("AccountSuspended");
        }

        if (!identity.EmailVerified)
        {
            return ApiError.BadRequest("EmailNotVerified");
        }

        var email = LoginCodeService.NormalizeEmail(identity.Email);
        var user = await FindByEmailAsync(email, ct).ConfigureAwait(false);
        if (user is not null)
        {
            if (!AccessRules.IsActive(user))
            {
                return ApiError.Forbidden("AccountSuspended");
            }

            // The provider verified this address, so it may sign in to the account that owns it.
            db.ExternalLogins.Add(new ExternalLogin { Provider = identity.Provider, Subject = identity.Subject, UserId = user.Id });
            ClaimAddress(user);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return await SignInAsync(user, ct).ConfigureAwait(false);
        }

        var name = identity.Name ?? req.GivenName;
        var token = tokens.CreateSignup(new SignupClaims(email, identity.Provider, identity.Subject, name));
        return Ok(AuthResultDto.NeedsProfile(token, email, name));
    }

    /// <summary>Creates the account: with a sign-up token (verified email), or with email + password.</summary>
    [HttpPost("register")]
    [EnableRateLimiting(SafetySetup.SignupRateLimit)]
    public async Task<IActionResult> Register(RegisterRequest req, CancellationToken ct)
    {
        if (!req.AcceptTerms)
        {
            return ApiError.BadRequest("TermsRequired");
        }

        if (!AccessRules.IsAdult(req.DateOfBirth, Today))
        {
            return ApiError.BadRequest("Underage");
        }

        if (req.DateOfBirth < Today.AddYears(-120))
        {
            return ApiError.BadRequest("InvalidDateOfBirth");
        }

        SignupClaims? verified = null;
        string email;
        if (req.SignupToken is not null)
        {
            verified = await tokens.ReadSignupAsync(req.SignupToken).ConfigureAwait(false);
            if (verified is null)
            {
                return ApiError.BadRequest("SignupExpired");
            }

            email = verified.Email;
        }
        else if (req.Email is not null && req.Password is not null)
        {
            email = LoginCodeService.NormalizeEmail(req.Email);
        }
        else
        {
            return ApiError.BadRequest("CredentialsRequired");
        }

        if (await FindByEmailAsync(email, ct).ConfigureAwait(false) is not null)
        {
            return ApiError.Conflict("EmailTaken");
        }

        // Someone banned before, by address or by this browser, can't start over (PLAN.md §4.8).
        if (await bans.IsBannedAsync(email, BanGuard.DeviceId(HttpContext), ct).ConfigureAwait(false))
        {
            return ApiError.Forbidden("AccountSuspended");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            DisplayName = req.DisplayName.Trim(),
            FullName = req.FullName.Trim(),
            Email = email,
            DateOfBirth = req.DateOfBirth,
            CountryCode = req.CountryCode,
            VerificationBadges = verified is null ? VerificationBadges.None : VerificationBadges.ContactVerified,
            CreatedAt = clock.GetUtcNow(),
        };
        if (req.Password is not null)
        {
            user.PasswordHash = Hasher.HashPassword(user, req.Password);
        }

        db.Users.Add(user);
        if (verified?.Provider is { } provider)
        {
            db.ExternalLogins.Add(new ExternalLogin { Provider = provider, Subject = verified.Subject!, UserId = user.Id });
        }

        // Consent records (GDPR / Israeli PPL): what was accepted, which version, when.
        foreach (var kind in PrivacyRules.Legal)
        {
            db.Consents.Add(new Consent { Id = Guid.NewGuid(), UserId = user.Id, Kind = kind, Version = options.LegalVersion, GrantedAt = user.CreatedAt });
        }

        // Optional and unticked by default.
        if (req.AllowAnalytics)
        {
            db.Consents.Add(new Consent { Id = Guid.NewGuid(), UserId = user.Id, Kind = ConsentKind.Analytics, Version = options.LegalVersion, GrantedAt = user.CreatedAt });
        }

        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return ApiError.Conflict("EmailTaken");
        }

        if (verified is null)
        {
            await codes.SendAsync(email, LoginCodePurpose.VerifyEmail, ct).ConfigureAwait(false);
        }

        return await SignInAsync(user, ct).ConfigureAwait(false);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req, CancellationToken ct)
    {
        var user = await FindByEmailAsync(LoginCodeService.NormalizeEmail(req.Email), ct).ConfigureAwait(false);
        var result = Hasher.VerifyHashedPassword(user!, user?.PasswordHash ?? DummyHash, req.Password);
        if (user?.PasswordHash is null || result == PasswordVerificationResult.Failed)
        {
            return ApiError.Unauthorized("InvalidCredentials");
        }

        if (!AccessRules.IsActive(user))
        {
            return ApiError.Forbidden("AccountSuspended");
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = Hasher.HashPassword(user, req.Password);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        return await SignInAsync(user, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The address owner has just proven it (emailed code or Google/Apple). If nobody had proven it before, the
    /// account may have been registered by someone else with a password they chose: drop that password and end
    /// its sessions, so only the real owner keeps access. Then the account earns the contact-verified badge.
    /// </summary>
    private static void ClaimAddress(User user)
    {
        if (!user.VerificationBadges.HasFlag(VerificationBadges.ContactVerified))
        {
            user.PasswordHash = null;
            user.SessionVersion++;
        }

        user.VerificationBadges |= VerificationBadges.ContactVerified;
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        SessionCookie.SignOut(HttpContext);
        return NoContent();
    }

    /// <summary>Ends every session of this account, on all devices.</summary>
    [Authorize]
    [HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll(CancellationToken ct)
    {
        var id = User.RequireUserId();
        await db.Users.Where(u => u.Id == id).ExecuteUpdateAsync(u => u.SetProperty(x => x.SessionVersion, x => x.SessionVersion + 1), ct).ConfigureAwait(false);
        SessionCookie.SignOut(HttpContext);
        return NoContent();
    }

    /// <summary>Emails a reset code when the account exists. Always 202.</summary>
    [HttpPost("password/forgot")]
    public async Task<IActionResult> ForgotPassword(EmailStartRequest req, CancellationToken ct)
    {
        var user = await FindByEmailAsync(LoginCodeService.NormalizeEmail(req.Email), ct).ConfigureAwait(false);
        if (user is not null && AccessRules.IsActive(user))
        {
            await codes.SendAsync(user.Email, LoginCodePurpose.ResetPassword, ct).ConfigureAwait(false);
        }

        return Accepted();
    }

    /// <summary>Sets a new password, signs out every other session and signs this one in.</summary>
    [HttpPost("password/reset")]
    public async Task<IActionResult> ResetPassword(PasswordResetRequest req, CancellationToken ct)
    {
        var check = await codes.VerifyAsync(req.Email, LoginCodePurpose.ResetPassword, req.Code, ct).ConfigureAwait(false);
        if (check != CodeCheck.Ok)
        {
            return CodeError(check);
        }

        var user = await FindByEmailAsync(LoginCodeService.NormalizeEmail(req.Email), ct).ConfigureAwait(false);
        if (user is null || !AccessRules.IsActive(user))
        {
            return CodeError(CodeCheck.Invalid);
        }

        user.PasswordHash = Hasher.HashPassword(user, req.NewPassword);
        user.SessionVersion++;
        user.VerificationBadges |= VerificationBadges.ContactVerified;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return await SignInAsync(user, ct).ConfigureAwait(false);
    }

    /// <summary>Sends a code to confirm the signed-in user's email (for accounts made with a password).</summary>
    [Authorize]
    [HttpPost("email/confirm/start")]
    public async Task<IActionResult> ConfirmEmailStart(CancellationToken ct)
    {
        var user = await db.Users.FirstAsync(u => u.Id == User.RequireUserId(), ct).ConfigureAwait(false);
        if (user.VerificationBadges.HasFlag(VerificationBadges.ContactVerified))
        {
            return NoContent();
        }

        return await codes.SendAsync(user.Email, LoginCodePurpose.VerifyEmail, ct).ConfigureAwait(false)
            ? Accepted()
            : ApiError.TooManyRequests("TooManyCodes");
    }

    [Authorize]
    [HttpPost("email/confirm")]
    public async Task<IActionResult> ConfirmEmail(CodeRequest req, CancellationToken ct)
    {
        var user = await db.Users.FirstAsync(u => u.Id == User.RequireUserId(), ct).ConfigureAwait(false);
        var check = await codes.VerifyAsync(user.Email, LoginCodePurpose.VerifyEmail, req.Code, ct).ConfigureAwait(false);
        if (check != CodeCheck.Ok)
        {
            return CodeError(check);
        }

        user.VerificationBadges |= VerificationBadges.ContactVerified;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return Ok(MeDto.From(user, Today));
    }

    private Task<User?> FindByEmailAsync(string email, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(u => u.Email == email && u.DeletedAt == null, ct);

    private async Task<IActionResult> SignInAsync(User user, CancellationToken ct)
    {
        await bans.RecordDeviceAsync(user.Id, BanGuard.DeviceId(HttpContext), ct).ConfigureAwait(false);
        session.SignIn(HttpContext, user);
        return Ok(AuthResultDto.SignedIn(MeDto.From(user, Today)));
    }

    private static ObjectResult CodeError(CodeCheck check) => check switch
    {
        CodeCheck.Expired => ApiError.BadRequest("CodeExpired"),
        CodeCheck.TooManyAttempts => ApiError.TooManyRequests("TooManyAttempts"),
        _ => ApiError.BadRequest("InvalidCode"),
    };
}
