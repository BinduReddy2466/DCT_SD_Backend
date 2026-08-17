namespace DCT_SD.Models.Entities;

public class DocumentType : AuditableEntity
{
    public string DocumentCode { get; set; } = string.Empty;
    public string DocumentName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<ManualValidationDocument> ManualValidationDocuments { get; set; } = new List<ManualValidationDocument>();
    public ICollection<MigrationDocument> MigrationDocuments { get; set; } = new List<MigrationDocument>();
}
