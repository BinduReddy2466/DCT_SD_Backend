namespace DCT_SD.Models.Dtos.ManualValidation;

// One Title Record's worth of edits submitted from a Save - RecordId identifies exactly which
// underlying ManualValidationRequests row this targets (never inferred by position/order alone),
// and only a RecordId that is actually a member of the saving record's Entry Number group is
// honored server-side.
public class SaveTitleRecordItemDto
{
    public int RecordId { get; set; }
    public string? Title { get; set; }
    public string? TitleType { get; set; }
    public string? Plan { get; set; }
    public string? Block { get; set; }
    public string? Lot { get; set; }
    public string? TitleSequence { get; set; }
}
