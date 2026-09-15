namespace DCT_SD.Models.Dtos.ManualValidation;

public class SaveManualValidationRequestDto
{
    public string? RdCode { get; set; }
    public string? EntryNumbersCsv { get; set; }

    // Zero or more Title Records being saved, each targeting one row of the Entry Number group
    // by RecordId (see SaveTitleRecordItemDto). Bound from indexed form fields
    // ("TitleRecords[0].RecordId", "TitleRecords[0].Title", ...) - one per "Title Record N" row
    // rendered in Details.cshtml.
    public List<SaveTitleRecordItemDto> TitleRecords { get; set; } = new();

    // Zero or more pending "Others" -> real Document Type corrections, submitted only as part of
    // Save (per the acceptance criteria: changes stay pending client-side and are never
    // persisted - file rename, DocumentsJson, imagePath - until Save succeeds). JSON-serialized
    // client-side as an array of {index, code, name} - a plain array survives a form POST far
    // more simply than trying to model-bind a list of complex objects from FormData. index is
    // the same synthesized 1-based position as ManualValidationDocumentDto.Id (a position in the
    // group's merged/de-duplicated Supporting Documents list, not any single row's own list).
    public string? DocumentChangesJson { get; set; }
}
