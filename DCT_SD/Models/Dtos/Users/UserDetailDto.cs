namespace DCT_SD.Models.Dtos.Users;

public class UserDetailDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public int RoleId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public IReadOnlyList<int> AssignedMenuIds { get; set; } = Array.Empty<int>();
    public DateTime DateCreated { get; set; }
}
