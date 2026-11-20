namespace DCT_SD.Models.ViewModels;

public class ChangePasswordViewModel
{
    // Read-only on the view - always the currently authenticated user's own username, never
    // typed or trusted from the posted form (AccountController overwrites it from
    // ICurrentUserService on every POST, same reasoning as ResetPasswordViewModel.Email).
    public string Username { get; set; } = string.Empty;

    // Validated manually in AccountController.ChangePassword (POST), not via DataAnnotations -
    // same reasoning as ResetPasswordViewModel: one combined "all fields required" message,
    // not independent per-field required messages.
    public string OldPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}
