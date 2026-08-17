namespace DCT_SD.Models.Entities;

public class Menu : AuditableEntity
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsBaseMenu { get; set; }

    public ICollection<UserMenuPermission> UserMenuPermissions { get; set; } = new List<UserMenuPermission>();
}
