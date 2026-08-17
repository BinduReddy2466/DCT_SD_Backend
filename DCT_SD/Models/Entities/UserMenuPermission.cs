namespace DCT_SD.Models.Entities;

public class UserMenuPermission
{
    public int UserId { get; set; }
    public int MenuId { get; set; }
    public DateTime GrantedAt { get; set; }

    public User User { get; set; } = null!;
    public Menu Menu { get; set; } = null!;
}
