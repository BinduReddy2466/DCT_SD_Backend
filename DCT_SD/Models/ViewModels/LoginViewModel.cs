using System.ComponentModel.DataAnnotations;

namespace DCT_SD.Models.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "Please enter your User ID / Email.")]
    [Display(Name = "User ID / Email")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your password.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}
