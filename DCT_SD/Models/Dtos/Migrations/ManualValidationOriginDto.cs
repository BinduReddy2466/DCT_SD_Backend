namespace DCT_SD.Models.Dtos.Migrations;

// Read-only snapshot of the Manual Validation record this Entry Record originated from (spec
// section 11) - present only when MigrationRecord.OcrExtractionRecordId matches an active
// ManualValidationRequests row. Never written back to.
public class ManualValidationOriginDto
{
    public string? Title { get; set; }
    public string? TitleType { get; set; }
    public string? Plan { get; set; }
    public string? Block { get; set; }
    public string? Lot { get; set; }
    public string? TitleSequence { get; set; }
    public DateTime ExtractionDate { get; set; }
}
