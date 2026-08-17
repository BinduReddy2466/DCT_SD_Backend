namespace DCT_SD.Models.Entities;

// One row per notification event (user_created/user_locked/user_activated/user_deactivated).
// Content-management only - there is no outbound mail sender in this app; Recipients/Subject/
// Body are stored and previewable so an admin can curate the wording ahead of when a real
// mail sender gets wired up, matching the behavior of the legacy client-only settings screen
// but now durable across sessions/devices instead of living in one browser's localStorage.
public class EmailTemplate : AuditableEntity
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Recipients { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}
