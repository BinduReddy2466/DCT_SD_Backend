namespace DCT_SD.Models.Dtos.ManualValidation;

public class SaveManualValidationRequestDto
{
    public string? RdCode { get; set; }
    public string? EntryNumbersCsv { get; set; }
    public string? Title { get; set; }
    public string? TitleType { get; set; }
    public string? Plan { get; set; }
    public string? Block { get; set; }
    public string? Lot { get; set; }
    public string? TitleSequence { get; set; }

    // Zero or more pending "Others" -> real Document Type corrections, submitted only as part of
    // Save (per the acceptance criteria: changes stay pending client-side and are never
    // persisted - file rename, DocumentsJson, imagePath - until Save succeeds). JSON-serialized
    // client-side as an array of {index, code, name} - a plain array survives a form POST far
    // more simply than trying to model-bind a list of complex objects from FormData. index is
    // the same synthesized 1-based position as ManualValidationDocumentDto.Id.
    public string? DocumentChangesJson { get; set; }
}
