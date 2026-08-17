using DCT_SD.Models.Entities;

namespace DCT_SD.Models.Dtos.Auth;

public class RefreshRotationResult
{
    public User User { get; set; } = null!;
    public IReadOnlyList<string> AllowedMenus { get; set; } = Array.Empty<string>();
    public string NewRawToken { get; set; } = string.Empty;
    public DateTime NewExpiresAtUtc { get; set; }
}
