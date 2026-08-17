namespace DCT_SD.Models.Dtos.Settings;

public class UpdateEmailTemplateRequestDto
{
    public string Recipients { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}
