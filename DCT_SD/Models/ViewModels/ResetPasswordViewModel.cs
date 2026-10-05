namespace DCT_SD.Models.ViewModels;

public class ResetPasswordViewModel
{
    public string Token { get; set; } = string.Empty;

    // Read-only on the view - populated from the validated token's email claim, never typed by
    // the user. Not a [Required]/DataAnnotations field for that reason: emptiness here would
    // mean the token itself is bad, which the GET action already screens for before this model
    // is ever shown, not a validation case the submitted form itself can trigger.
    public string Email { get; set; } = string.Empty;

    // Validated manually in AccountController.ResetPassword (POST), not via DataAnnotations -
    // the acceptance criteria call for exactly one combined "All fields are required" message
    // when either is empty, not independent per-field required messages.
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}
