using DCT_SD.Models.Enums;

namespace DCT_SD.Models.Entities;

public class MigrationDocument
{
    public int Id { get; set; }
    public int MigrationRecordId { get; set; }
    public int? DocumentTypeId { get; set; }
    public string DocumentName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public MigrationDocumentStatus Status { get; set; } = MigrationDocumentStatus.Migrated;
    public string? ExistingFileName { get; set; }
    public int? PerformedByUserId { get; set; }
    public string? PerformedByUsername { get; set; }
    public DateTime? ActionDate { get; set; }

    public MigrationRecord MigrationRecord { get; set; } = null!;
    public DocumentType? DocumentType { get; set; }
}
