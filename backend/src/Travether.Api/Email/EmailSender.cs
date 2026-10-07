namespace Travether.Api.Email;

public sealed record EmailMessage(string To, string Subject, string TextBody);

/// <summary>Transactional email. Production plugs in Resend or Postmark; locally the message is logged.</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}

/// <summary>
/// Simulated sender for development: writes the message to the log instead of sending it. Outside
/// Development and Testing the body is never logged, because it holds one-time sign-in codes.
/// </summary>
public sealed partial class LogEmailSender(ILogger<LogEmailSender> logger, IHostEnvironment env) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        if (env.IsDevelopment() || env.IsEnvironment("Testing"))
        {
            LogEmail(logger, message.To, message.Subject, message.TextBody);
        }
        else
        {
            LogNotSent(logger);
        }

        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Simulated email to {To}: {Subject}\n{Body}")]
    private static partial void LogEmail(ILogger logger, string to, string subject, string body);

    [LoggerMessage(Level = LogLevel.Error, Message = "Email was not sent: no email provider is configured (Email:ResendApiKey)")]
    private static partial void LogNotSent(ILogger logger);
}
