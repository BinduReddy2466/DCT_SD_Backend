namespace DCT_SD.Models.Dtos.Users;

public class UpdateUserRequestDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int RoleId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Password { get; set; }
    public List<int> AssignedMenuIds { get; set; } = new();
}
