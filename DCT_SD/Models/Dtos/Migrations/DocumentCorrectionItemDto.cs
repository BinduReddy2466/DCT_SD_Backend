namespace DCT_SD.Models.Dtos.Migrations;

// One pending Others -> real Document Type correction for one supporting document image, as
// submitted by Migration Details' Save. DocumentId matches MigrationDocumentDto.Id - a real
// MigrationDocuments row id when positive, or the synthetic -(position+1) id GetByIdAsync assigns
// a still-"Pending" (not yet migrated) document when negative - see
// MigrationService.ApplyRealDocumentCorrections / ApplyPendingDocumentCorrections.
public class DocumentCorrectionItemDto
{
    public int DocumentId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
