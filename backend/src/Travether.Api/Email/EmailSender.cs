namespace Travether.Api.Email;

public sealed record EmailMessage(string To, string Subject, string TextBody);

/// <summary>Transactional email. Production plugs in Resend or Postmark; locally the message is logged.</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}

/// <summary>Simulated sender for development: writes the message to the log instead of sending it.</summary>
public sealed partial class LogEmailSender(ILogger<LogEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        LogEmail(logger, message.To, message.Subject, message.TextBody);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Simulated email to {To}: {Subject}\n{Body}")]
    private static partial void LogEmail(ILogger logger, string to, string subject, string body);
}
