namespace DCT_SD.Models.Dtos.Users;

public class CreateUserRequestDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int RoleId { get; set; }
    public List<int> AssignedMenuIds { get; set; } = new();
}
