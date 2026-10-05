namespace DCT_SD.Helpers;

// Ports DEFAULT_EMAIL_TEMPLATES from the React frontend verbatim. Used both to seed the
// EmailTemplates table on first run and to answer "Restore Default" without a second copy
// of the same wording drifting out of sync.
public record DefaultEmailTemplate(string Key, string Label, string Recipients, string Subject, string Body);

public static class DefaultEmailTemplates
{
    public static readonly IReadOnlyList<DefaultEmailTemplate> All = new[]
    {
        new DefaultEmailTemplate(
            "user_created",
            "User Created",
            "{{Email}}",
            "Lares Profile created",
            "Dear {{FirstName}} {{LastName}},\n\nA profile has been created for you in the Lares Portal.\n\nPlease follow the instructions below to complete the registration process:\n\n1. Log in to the portal using the link: {{ResetPasswordLink}}.\n2. Use the credentials provided below to access your account:\n   User ID: {{Email}}\n   Password: {{TemporaryPassword}}\n\nOnce logged in, you will be prompted to update your password for security purposes.\n\nIf you have any questions or need assistance, please do not hesitate to contact us.\n\nWarm regards,\nLares"),
        new DefaultEmailTemplate(
            "password_reset",
            "Password Reset Request",
            "{{Email}}",
            "Password Reset Request",
            "Dear {{FirstName}} {{LastName}},\n\nTo reset your password, please click the link below:\n{{ResetPasswordLink}}\n\nIf you did not request a password reset, please ignore this email. Your account will remain secure.\n\nImportant:\n\nThis link will expire in {{ExpiryMinutes}} minutes for security reasons.\n\nIf you need further assistance, feel free to contact our support team.\n\nWarm regards,\nLares"),
        new DefaultEmailTemplate(
            "user_locked",
            "User Locked",
            "{{Email}}",
            "Lares User Status Change",
            "Dear {{FirstName}} {{LastName}},\n\nYour account status has been locked by the Administrator.\n\nUser ID: {{Email}}\n\nWarm regards,\nLares"),
        new DefaultEmailTemplate(
            "user_activated",
            "User Activated",
            "{{Email}}",
            "Lares User Status Change",
            "Dear {{FirstName}} {{LastName}},\n\nYour account status has been activated by the Administrator.\n\nUser ID: {{Email}}\n\nYou may sign in using your existing credentials.\n\nWarm regards,\nLares"),
        new DefaultEmailTemplate(
            "user_deactivated",
            "User Deactivated",
            "{{Email}}",
            "Lares User Status Change",
            "Dear {{FirstName}} {{LastName}},\n\nYour account status has been deactivated by the Administrator.\n\nUser ID: {{Email}}\n\nWarm regards,\nLares"),
    };

    public static DefaultEmailTemplate? Find(string key) => All.FirstOrDefault(t => t.Key == key);
}
