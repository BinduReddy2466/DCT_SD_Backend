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

    // True when this document either still needs manual Document Type correction (DocumentId is
    // "OTHERS") or was corrected from "OTHERS" at some point in the past (DocumentsJson's
    // documentTypeCode field is used as that permanent marker once corrected - see
    // ApplyDocumentTypeChange). Drives whether the Document Type dropdown is shown at all: a
    // document that was extracted as a real type directly (never "OTHERS") is never editable
    // through this UI, but a document that started as "OTHERS" stays correctable indefinitely,
    // even after being reclassified and saved, so a wrong pick can still be fixed later.
    public bool CanChangeDocumentType { get; set; }
}
