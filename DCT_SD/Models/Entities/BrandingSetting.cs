namespace DCT_SD.Models.Entities;

// Single-row configuration - the login page's background image. Storing just the relative
// wwwroot path (not the bytes) keeps the row tiny; the file itself lives under
// wwwroot/uploads/branding and is served directly by the static file middleware.
public class BrandingSetting : AuditableEntity
{
    public string? ImagePath { get; set; }
}
