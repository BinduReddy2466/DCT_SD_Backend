namespace DCT_SD.Models.Dtos.ManualValidation;

// One Title Record = exactly one underlying ManualValidationRequests row (RecordId), contributing
// its own Title/TitleType/Plan/Block/Lot/TitleSequence to a Details view that may combine several
// rows sharing the same EntryNumbersCsv. Every row in the group is its own Title Record here -
// never merged or overwritten into another - so a group of 2 rows always yields exactly 2 of these.
public class ManualValidationTitleRecordDto
{
    public int RecordId { get; set; }
    public string? Title { get; set; }
    public string? TitleType { get; set; }
    public string? Plan { get; set; }
    public string? Block { get; set; }
    public string? Lot { get; set; }
    public string? TitleSequence { get; set; }
    public IReadOnlyList<string> MissingFields { get; set; } = Array.Empty<string>();
}
