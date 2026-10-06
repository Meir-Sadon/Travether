using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Travether.Api.Api;
using Travether.Api.Auth;
using Travether.Api.Chat;
using Travether.Api.Data;
using Travether.Api.Domain;
using Travether.Api.Plans;

namespace Travether.Api.Controllers;

/// <summary>Card and plan chats. Contact details are never revealed automatically; people share their own number by choice.</summary>
[ApiController]
[Authorize]
[Route("api/chats")]
public sealed class ChatController(TravetherDbContext db, ChatService chats, PlanViews plans, TimeProvider clock) : ControllerBase
{
    private Guid Me => User.RequireUserId();

    [HttpGet]
    public Task<IReadOnlyList<ChatSummaryDto>> Mine(CancellationToken ct) => chats.ListAsync(Me, ct);

    /// <summary>The chat and its latest messages; pass <c>before</c> to page back.</summary>
    [HttpGet("{kind}/{refId:guid}")]
    public async Task<IActionResult> Get(string kind, Guid refId, DateTimeOffset? before, CancellationToken ct)
    {
        if (await OpenAsync(kind, refId, ct).ConfigureAwait(false) is not { } convo)
        {
            return ApiError.NotFound();
        }

        var (messages, hasMore) = await chats.PageAsync(Me, convo.Id, before, ct).ConfigureAwait(false);
        string title;
        MeetingPointDto? meetingPoint = null;
        if (convo.Type == ConversationType.Card)
        {
            title = await db.VacationCards.Where(c => c.Id == refId).Select(c => c.Name).FirstAsync(ct).ConfigureAwait(false);
        }
        else
        {
            var plan = (await plans.LoadAsync(refId, Me, null, ct).ConfigureAwait(false))!;
            title = plan.Title;
            meetingPoint = plan.MeetingPoint;
        }

        return Ok(new ChatDto(ChatKeys.For(convo.Type, refId), convo.Type, refId, title, await chats.MemberCountAsync(convo, ct).ConfigureAwait(false), meetingPoint, messages, hasMore));
    }

    [HttpPost("{kind}/{refId:guid}/messages")]
    public async Task<IActionResult> Send(string kind, Guid refId, SendMessageInput input, CancellationToken ct)
    {
        var body = input.Body?.Trim() ?? "";
        if (body.Length == 0)
        {
            return ApiError.BadRequest("MessageEmpty");
        }

        return await OpenAsync(kind, refId, ct).ConfigureAwait(false) is { } convo
            ? Ok(await chats.PostAsync(convo, Me, body, MessageKind.Text, ct).ConfigureAwait(false))
            : ApiError.NotFound();
    }

    /// <summary>Shares the sender's own phone number (as a call or WhatsApp link) in this chat.</summary>
    [HttpPost("{kind}/{refId:guid}/contact")]
    public async Task<IActionResult> ShareContact(string kind, Guid refId, ShareContactInput input, CancellationToken ct)
    {
        if (await OpenAsync(kind, refId, ct).ConfigureAwait(false) is not { } convo)
        {
            return ApiError.NotFound();
        }

        var phone = await db.Users.Where(u => u.Id == Me).Select(u => u.Phone).FirstAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrEmpty(phone))
        {
            return ApiError.BadRequest("PhoneRequired");
        }

        var kindOfMessage = input.Kind == ContactKind.Whatsapp ? MessageKind.ContactWhatsapp : MessageKind.ContactPhone;
        return Ok(await chats.PostAsync(convo, Me, phone, kindOfMessage, ct).ConfigureAwait(false));
    }

    [HttpPost("{kind}/{refId:guid}/read")]
    public async Task<IActionResult> MarkRead(string kind, Guid refId, CancellationToken ct)
    {
        if (await OpenAsync(kind, refId, ct).ConfigureAwait(false) is not { } convo)
        {
            return ApiError.NotFound();
        }

        await chats.MarkReadAsync(convo.Id, Me, clock.GetUtcNow(), ct).ConfigureAwait(false);
        return NoContent();
    }

    private async Task<Conversation?> OpenAsync(string kind, Guid refId, CancellationToken ct) =>
        ChatKeys.Parse(kind) is { } type ? await chats.OpenAsync(Me, type, refId, ct).ConfigureAwait(false) : null;
}
