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

    // A pending "Others" -> real Document Type correction, submitted only as part of Save (per
    // the acceptance criteria: the change stays pending client-side and is never persisted -
    // file rename, DocumentsJson, imagePath - until Save succeeds). DocumentChangeIndex is the
    // same synthesized 1-based position as ManualValidationDocumentDto.Id.
    public int? DocumentChangeIndex { get; set; }
    public string? DocumentChangeCode { get; set; }
    public string? DocumentChangeName { get; set; }
}
