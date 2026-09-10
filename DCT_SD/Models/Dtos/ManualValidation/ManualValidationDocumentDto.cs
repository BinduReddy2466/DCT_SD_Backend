namespace DCT_SD.Models.Dtos.ManualValidation;

public class ManualValidationDocumentDto
{
    public int Id { get; set; }

    // The exact documentId value from DocumentsJson (e.g. "DOC160", or "OTHERS" for a document
    // still needing manual Document Type correction) - never generated/guessed.
    public string? DocumentId { get; set; }
    public string DocumentName { get; set; } = string.Empty;

    // Always the exact renamedFileName value from the DocumentsJson object - never generated,
    // reconstructed, or derived from anything else.
    public string RenamedFileName { get; set; } = string.Empty;
}
