namespace DCT_SD.Models.Entities;

// Generic settings table (replaces the old BrandingSettings/EmailTemplates/SessionSettings
// tables). Category discriminates the row's purpose ("Branding", "Session", "EmailTemplate");
// DataJson holds the category-specific payload as JSON - interpreting it is a service concern.
public class AppSetting : AuditableEntity
{
    public string Category { get; set; } = string.Empty;
    public string? Key { get; set; }
    public string? Label { get; set; }
    public string DataJson { get; set; } = "{}";
}
