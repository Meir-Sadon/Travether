using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Travether.Api.Api;
using Travether.Api.Auth;
using Travether.Api.Data;
using Travether.Api.Images;
using Travether.Api.Profiles;
using Travether.Api.Safety;

namespace Travether.Api.Controllers;

/// <summary>Optional profile fields; only the fields sent are changed.</summary>
public sealed record UpdateProfileRequest(
    [MaxLength(40)] string? DisplayName,
    [MaxLength(120)] string? FullName,
    [RegularExpression("^[A-Z]{2}$")] string? CountryCode,
    [MaxLength(500)] string? Bio,
    [RegularExpression(@"^(\+[1-9]\d{6,14})?$")] string? Phone,
    [MaxLength(ProfileRules.MaxLanguages)] IReadOnlyList<string>? Languages,
    [MaxLength(ProfileRules.MaxInterests)] IReadOnlyList<string>? Interests);

/// <summary>The signed-in user's own profile.</summary>
[ApiController]
[Authorize]
[Route("api/me")]
public sealed class MeController(TravetherDbContext db, IImageStore images, BanGuard bans, TimeProvider clock) : ControllerBase
{
    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    [HttpGet]
    public async Task<MeDto> Get(CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == User.RequireUserId(), ct).ConfigureAwait(false);
        return MeDto.From(user, Today);
    }

    [HttpPatch]
    public async Task<IActionResult> Update(UpdateProfileRequest req, CancellationToken ct)
    {
        var user = await db.Users.FirstAsync(u => u.Id == User.RequireUserId(), ct).ConfigureAwait(false);

        if (req.DisplayName is not null)
        {
            if (string.IsNullOrWhiteSpace(req.DisplayName))
            {
                return ApiError.BadRequest("DisplayNameRequired");
            }

            user.DisplayName = req.DisplayName.Trim();
        }

        if (req.FullName is not null)
        {
            if (string.IsNullOrWhiteSpace(req.FullName))
            {
                return ApiError.BadRequest("FullNameRequired");
            }

            user.FullName = req.FullName.Trim();
        }

        if (req.CountryCode is not null)
        {
            user.CountryCode = req.CountryCode;
        }

        if (req.Bio is not null)
        {
            user.Bio = string.IsNullOrWhiteSpace(req.Bio) ? null : req.Bio.Trim();
        }

        if (req.Phone is not null)
        {
            if (req.Phone.Length > 0 && await bans.IsPhoneBannedAsync(req.Phone, ct).ConfigureAwait(false))
            {
                return ApiError.Forbidden("PhoneNotAllowed");
            }

            // Private: only ever shown to others when the user shares it in a chat (PLAN.md decision 3).
            user.Phone = req.Phone.Length == 0 ? null : req.Phone;
        }

        if (req.Languages is not null)
        {
            if (!ProfileRules.TryNormalizeLanguages(req.Languages, out var languages))
            {
                return ApiError.BadRequest("InvalidLanguage");
            }

            user.Languages = languages;
        }

        if (req.Interests is not null)
        {
            if (!ProfileRules.TryNormalizeInterests(req.Interests, out var interests))
            {
                return ApiError.BadRequest("InvalidInterest");
            }

            user.Interests = interests;
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return Ok(MeDto.From(user, Today));
    }

    /// <summary>Uploads a profile photo (JPEG, PNG or WebP, up to 5 MB) and replaces the old one.</summary>
    [HttpPost("photo")]
    [RequestSizeLimit(ImageRules.MaxBytes + 64 * 1024)]
    public async Task<IActionResult> UploadPhoto(IFormFile? file, CancellationToken ct)
    {
        var (data, kind, error) = await ImageRules.ReadAsync(file, ct).ConfigureAwait(false);
        if (data is null)
        {
            return ApiError.BadRequest(error!);
        }

        string url;
        await using (data)
        {
            url = await images.SaveAsync(data, kind, "avatars", ct).ConfigureAwait(false);
        }

        var user = await db.Users.FirstAsync(u => u.Id == User.RequireUserId(), ct).ConfigureAwait(false);
        var old = user.PhotoUrl;
        user.PhotoUrl = url;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        if (old is not null)
        {
            await images.DeleteAsync(old, ct).ConfigureAwait(false);
        }

        return Ok(MeDto.From(user, Today));
    }

    [HttpDelete("photo")]
    public async Task<IActionResult> DeletePhoto(CancellationToken ct)
    {
        var user = await db.Users.FirstAsync(u => u.Id == User.RequireUserId(), ct).ConfigureAwait(false);
        if (user.PhotoUrl is { } old)
        {
            user.PhotoUrl = null;
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            await images.DeleteAsync(old, ct).ConfigureAwait(false);
        }

        return Ok(MeDto.From(user, Today));
    }
}
