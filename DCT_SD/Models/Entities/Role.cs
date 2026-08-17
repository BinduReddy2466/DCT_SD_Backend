namespace DCT_SD.Models.Entities;

public class Role : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystemDefined { get; set; }

    public ICollection<User> Users { get; set; } = new List<User>();
}
