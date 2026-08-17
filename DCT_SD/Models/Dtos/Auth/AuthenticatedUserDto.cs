namespace DCT_SD.Models.Dtos.Auth;

public class AuthenticatedUserDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public IReadOnlyList<string> AllowedMenus { get; set; } = Array.Empty<string>();
}
