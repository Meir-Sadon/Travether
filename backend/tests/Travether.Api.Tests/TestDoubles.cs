using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Travether.Api.Auth;
using Travether.Api.Domain;
using Travether.Api.Email;

namespace Travether.Api.Tests;

public sealed partial class CapturingEmailSender : IEmailSender
{
    public ConcurrentQueue<EmailMessage> Sent { get; } = new();

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        Sent.Enqueue(message);
        return Task.CompletedTask;
    }

    /// <summary>The six-digit code from the latest email to this address.</summary>
    public string LatestCode(string to) =>
        SixDigits().Match(Sent.Last(m => string.Equals(m.To, to, StringComparison.OrdinalIgnoreCase)).TextBody).Value;

    [GeneratedRegex(@"\b\d{6}\b")]
    private static partial Regex SixDigits();
}

/// <summary>Accepts tokens registered with <see cref="Issue"/>; everything else is invalid.</summary>
public sealed class FakeExternalVerifier : IExternalIdentityVerifier
{
    private readonly ConcurrentDictionary<string, ExternalIdentity> tokens = new();

    public bool IsEnabled(ExternalProvider provider) => true;

    public string Issue(ExternalIdentity identity)
    {
        var token = Guid.NewGuid().ToString("N");
        tokens[token] = identity;
        return token;
    }

    public Task<ExternalIdentity?> VerifyAsync(ExternalProvider provider, string idToken, CancellationToken ct = default) =>
        Task.FromResult(tokens.TryGetValue(idToken, out var id) && id.Provider == provider ? id : null);
}
