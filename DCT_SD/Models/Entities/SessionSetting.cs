using DCT_SD.Models.Enums;

namespace DCT_SD.Models.Entities;

// Single-row configuration (mirrors DEFAULT_SESSION_SETTINGS in the frontend). RowVersion
// (inherited from AuditableEntity) guards against two admins overwriting each other's change.
public class SessionSetting : AuditableEntity
{
    public int TimeoutMinutes { get; set; }
    public SessionTimeoutAction Action { get; set; }
}
