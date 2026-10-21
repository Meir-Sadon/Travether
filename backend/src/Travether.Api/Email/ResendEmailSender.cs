using System.Net.Http.Headers;

namespace Travether.Api.Email;

public sealed class EmailOptions
{
    /// <summary>Resend API key. When empty, emails are only logged (<see cref="LogEmailSender"/>).</summary>
    public string? ResendApiKey { get; set; }

    /// <summary>Sender, e.g. <c>Travether &lt;hello@travether.app&gt;</c>.</summary>
    public string From { get; set; } = "Travether <onboarding@resend.dev>";
}

/// <summary>Sends through the Resend HTTP API (https://resend.com/docs/api-reference/emails/send-email).</summary>
public sealed class ResendEmailSender(HttpClient http, EmailOptions options) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails")
        {
            Content = JsonContent.Create(new { from = options.From, to = new[] { message.To }, subject = message.Subject, text = message.TextBody }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ResendApiKey);
        using var response = await http.SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }
}

public static class EmailSetup
{
    public static IServiceCollection AddTravetherEmail(this IServiceCollection services, IConfiguration config)
    {
        var options = config.GetSection("Email").Get<EmailOptions>() ?? new EmailOptions();
        services.AddSingleton(options);
        if (string.IsNullOrWhiteSpace(options.ResendApiKey))
        {
            services.AddSingleton<IEmailSender, LogEmailSender>();
        }
        else
        {
            services.AddHttpClient<IEmailSender, ResendEmailSender>();
        }

        return services;
    }
}
