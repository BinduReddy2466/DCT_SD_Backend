namespace DCT_SD.Models.Dtos.ManualValidation;

public class ManualValidationDetailDto
{
    public int Id { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public string? RdCode { get; set; }
    public string? RdName { get; set; }
    public string? EntryNumbersCsv { get; set; }
    public string? Title { get; set; }
    public string? TitleType { get; set; }
    public string? Plan { get; set; }
    public string? Block { get; set; }
    public string? Lot { get; set; }
    public string? TitleSequence { get; set; }
    public string Status { get; set; } = string.Empty;
    public IReadOnlyList<string> MissingFields { get; set; } = Array.Empty<string>();

    // One entry per ManualValidationRequests row sharing this record's exact EntryNumbersCsv
    // (always at least 1 - the opened record itself, when it has no group siblings). Title/
    // TitleType/Plan/Block/Lot/TitleSequence above still describe the opened record alone (kept
    // for backward compatibility); this is the source of truth for rendering "Title Record N".
    public IReadOnlyList<ManualValidationTitleRecordDto> TitleRecords { get; set; } = Array.Empty<ManualValidationTitleRecordDto>();

    // Supporting Documents merged across every row in the group and de-duplicated by physical
    // image identity (see ManualValidationService.BuildCombinedSortedDocuments) - Id is this
    // document's 1-based position in that merged/sorted list, not a position within any single
    // row's own DocumentsJson.
    public IReadOnlyList<ManualValidationDocumentDto> Documents { get; set; } = Array.Empty<ManualValidationDocumentDto>();
}
