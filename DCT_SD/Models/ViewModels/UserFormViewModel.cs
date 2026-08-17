using System.ComponentModel.DataAnnotations;
using DCT_SD.Models.Dtos.Menus;
using DCT_SD.Models.Dtos.Roles;

namespace DCT_SD.Models.ViewModels;

public class UserFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "First Name is required.")]
    [Display(Name = "First Name")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last Name is required.")]
    [Display(Name = "Last Name")]
    public string LastName { get; set; } = string.Empty;

    [Display(Name = "Username (Email)")]
    public string Username { get; set; } = string.Empty;

    public string? Password { get; set; }

    [Display(Name = "Role")]
    public int? RoleId { get; set; }

    public string Status { get; set; } = "Active";
    public List<int> AssignedMenuIds { get; set; } = new();

    public bool IsEditing { get; set; }
    public bool RoleDisabled { get; set; }
    public IReadOnlyList<RoleDto> RoleOptions { get; set; } = Array.Empty<RoleDto>();
    public IReadOnlyList<MenuDto> Menus { get; set; } = Array.Empty<MenuDto>();
}
