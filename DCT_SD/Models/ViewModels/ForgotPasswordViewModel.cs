using System.ComponentModel.DataAnnotations;

namespace DCT_SD.Models.ViewModels;

public class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "Please enter your registered email address.")]
    [Display(Name = "Email Address")]
    public string Email { get; set; } = string.Empty;
}
