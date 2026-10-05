namespace DCT_SD.Services;

public interface IEmailSenderService
{
    Task SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken = default);
}
