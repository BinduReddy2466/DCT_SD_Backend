using System.ComponentModel.DataAnnotations;

namespace DCT_SD.Models.ViewModels;

public class RoleFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Role Name is required.")]
    [StringLength(50)]
    [Display(Name = "Role Name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Description { get; set; }

    public bool IsSystemDefined { get; set; }
}
