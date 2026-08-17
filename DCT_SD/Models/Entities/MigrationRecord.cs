using DCT_SD.Models.Enums;

namespace DCT_SD.Models.Entities;

public class MigrationRecord
{
    public int Id { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public int? OcrExtractionRecordId { get; set; }
    public DateTime MigrationDate { get; set; }

    public string RdCode { get; set; } = string.Empty;
    public string RdName { get; set; } = string.Empty;
    public string? EntryNumbersCsv { get; set; }
    public string? Title { get; set; }
    public TitleType? TitleType { get; set; }
    public string? Plan { get; set; }
    public string? Block { get; set; }
    public string? Lot { get; set; }
    public string? TitleSequence { get; set; }

    public MigrationStatus MigrationStatus { get; set; }
    public SupportingDocumentStatus SdStatus { get; set; }
    public string MigratedToRdName { get; set; } = string.Empty;

    public OcrExtractionRecord? OcrExtractionRecord { get; set; }
    public ICollection<MigrationDocument> Documents { get; set; } = new List<MigrationDocument>();
}
