using System.Net;
using System.Net.Mail;

namespace DCT_SD.Services;

// Uses .NET's own built-in SmtpClient - no extra NuGet dependency - against the same Zimbra mail
// server already used by other applications in this organization (host/port/SSL/from-address are
// plain config, same as every other non-secret setting in appsettings.json; only the password is
// a secret, read from user secrets / the Smtp__Password environment variable, never committed).
public class EmailSenderService : IEmailSenderService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailSenderService> _logger;

    public EmailSenderService(IConfiguration configuration, ILogger<EmailSenderService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken = default)
    {
        var host = _configuration["Smtp:Host"]
            ?? throw new InvalidOperationException("Smtp:Host is not configured.");
        var port = int.TryParse(_configuration["Smtp:Port"], out var parsedPort) ? parsedPort : 25;
        var enableSsl = bool.TryParse(_configuration["Smtp:EnableSsl"], out var parsedSsl) && parsedSsl;
        var fromAddress = _configuration["Smtp:FromAddress"]
            ?? throw new InvalidOperationException("Smtp:FromAddress is not configured.");
        var fromName = _configuration["Smtp:FromName"] ?? fromAddress;
        var username = _configuration["Smtp:Username"] ?? fromAddress;
        var password = _configuration["Smtp:Password"]
            ?? throw new InvalidOperationException("Smtp:Password is missing. Set it via the Smtp__Password environment variable or user secrets.");

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl,
            Credentials = new NetworkCredential(username, password),
        };

        using var message = new MailMessage
        {
            From = new MailAddress(fromAddress, fromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false,
        };
        message.To.Add(toAddress);

        try
        {
            await client.SendMailAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            // The caller (e.g. Add User) must never fail account creation just because the
            // notification email couldn't be delivered - log it for diagnosis and let the
            // caller decide whether/how to surface that to the admin.
            _logger.LogError(ex, "Failed to send email to {ToAddress} with subject '{Subject}'.", toAddress, subject);
            throw;
        }
    }
}
