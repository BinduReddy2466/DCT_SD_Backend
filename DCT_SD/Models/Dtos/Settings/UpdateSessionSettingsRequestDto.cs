namespace DCT_SD.Models.Dtos.Settings;

public class UpdateSessionSettingsRequestDto
{
    public int TimeoutMinutes { get; set; }
    public string Action { get; set; } = string.Empty;
}
