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

/// <summary>Stands in for the push services: records each request and answers 201, or a chosen status per endpoint.</summary>
public sealed class CapturingPushHandler : HttpMessageHandler
{
    public ConcurrentQueue<(Uri Endpoint, byte[] Body, string? Authorization)> Sent { get; } = new();

    public ConcurrentDictionary<string, System.Net.HttpStatusCode> Answers { get; } = new();

    public IEnumerable<(Uri Endpoint, byte[] Body, string? Authorization)> To(string endpoint) => Sent.Where(s => s.Endpoint.ToString() == endpoint);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
        Sent.Enqueue((request.RequestUri!, body, request.Headers.TryGetValues("Authorization", out var auth) ? auth.Single() : null));
        return new HttpResponseMessage(Answers.TryGetValue(request.RequestUri!.ToString(), out var status) ? status : System.Net.HttpStatusCode.Created);
    }
}
