namespace DCT_SD.Models.Dtos.Migrations;

public class MigrationDocumentDto
{
    public int Id { get; set; }
    public string DocumentName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ExistingFileName { get; set; }
    public string? PerformedBy { get; set; }
    public DateTime? ActionDate { get; set; }
    public string? DestinationPath { get; set; }
    public long? SizeBytes { get; set; }

    // Supporting Document Image columns AC.
    public string? DocumentType { get; set; }
    public string? SourceImagePath { get; set; }
    public string? TargetSystemValidation { get; set; }
    public string? ReasonForFailing { get; set; }

    // True when this document's Document Type is still "Others" - drives whether the Document
    // Type cell shows a searchable dropdown or read-only text (spec section 2.2).
    public bool CanChangeDocumentType { get; set; }
}
