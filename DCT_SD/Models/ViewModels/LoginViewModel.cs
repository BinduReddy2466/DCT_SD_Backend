using System.ComponentModel.DataAnnotations;

namespace DCT_SD.Models.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "All fields are required.")]
    [Display(Name = "User ID / Email")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "All fields are required.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}
