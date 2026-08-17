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
            "Lares Profile Created",
            "Hi {{FirstName}} {{LastName}},\n\nYour Lares account has been created.\n\nUsername: {{Email}}\nTemporary Password: {{TemporaryPassword}}\n\nPlease sign in and change your password using the link below:\n{{ChangePasswordLink}}\n\nDate: {{CurrentDate}}"),
        new DefaultEmailTemplate(
            "user_locked",
            "User Locked",
            "{{Email}}",
            "Lares User Status Change",
            "Hi {{FirstName}} {{LastName}},\n\nYour Lares account ({{Email}}) has been locked as of {{CurrentDate}}.\n\nIf you believe this is a mistake, please contact your Administrator."),
        new DefaultEmailTemplate(
            "user_activated",
            "User Activated",
            "{{Email}}",
            "Lares User Status Change",
            "Hi {{FirstName}} {{LastName}},\n\nYour Lares account ({{Email}}) has been activated as of {{CurrentDate}}.\n\nYou may sign in using your existing credentials."),
        new DefaultEmailTemplate(
            "user_deactivated",
            "User Deactivated",
            "{{Email}}",
            "Lares User Status Change",
            "Hi {{FirstName}} {{LastName}},\n\nYour Lares account ({{Email}}) has been deactivated as of {{CurrentDate}}.\n\nPlease contact your Administrator for further assistance."),
    };

    public static DefaultEmailTemplate? Find(string key) => All.FirstOrDefault(t => t.Key == key);
}
