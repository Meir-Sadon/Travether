using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Travether.Api.Api;
using Travether.Api.Auth;
using Travether.Api.Data;
using Travether.Api.Profiles;

namespace Travether.Api.Controllers;

/// <summary>Optional profile fields; only the fields sent are changed.</summary>
public sealed record UpdateProfileRequest(
    [MaxLength(40)] string? DisplayName,
    [MaxLength(120)] string? FullName,
    [RegularExpression("^[A-Z]{2}$")] string? CountryCode,
    [MaxLength(500)] string? Bio,
    [MaxLength(ProfileRules.MaxLanguages)] IReadOnlyList<string>? Languages,
    [MaxLength(ProfileRules.MaxInterests)] IReadOnlyList<string>? Interests);

/// <summary>The signed-in user's own profile.</summary>
[ApiController]
[Authorize]
[Route("api/me")]
public sealed class MeController(TravetherDbContext db, TimeProvider clock) : ControllerBase
{
    [HttpGet]
    public async Task<MeDto> Get(CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == User.RequireUserId(), ct).ConfigureAwait(false);
        return MeDto.From(user, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime));
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
        return Ok(MeDto.From(user, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime)));
    }
}
