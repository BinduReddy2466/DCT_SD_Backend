using DCT_SD.Models.Enums;

namespace DCT_SD.Models.Entities;

public class ManualValidationRequest
{
    public int Id { get; set; }
    public string RequestNumber { get; set; } = string.Empty;

    public int? OcrExtractionRecordId { get; set; }
    public string? RdCode { get; set; }
    public string? RdName { get; set; }
    public string? EntryNumbersCsv { get; set; }
    // Maps to the EntryNumberRange column already present in the deployed schema (added via
    // DBChanges/dct_sd_main.sql, not an EF migration - see ManualValidationService.GetGroupRecordsAsync
    // for why it exists: it is the grouping/record-matching key, distinct in purpose from
    // EntryNumbersCsv, which stays a separate, independently-edited field.
    public string? EntryNumberRange { get; set; }
    public string? Title { get; set; }
    public TitleType? TitleType { get; set; }
    public string? Plan { get; set; }
    public string? Block { get; set; }
    public string? Lot { get; set; }
    public string? TitleSequence { get; set; }

    public ManualValidationStatus Status { get; set; }
    public string MissingFieldsCsv { get; set; } = string.Empty;
    public string? DocumentsJson { get; set; }
    public DateTime ExtractionDate { get; set; }

    public int? UpdatedByUserId { get; set; }
    public string? UpdatedByUsername { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public int? LockedByUserId { get; set; }
    public string? LockedByUsername { get; set; }
    public DateTime? LockedAt { get; set; }

    public DateTime? MigratedAt { get; set; }

    public OcrExtractionRecord? OcrExtractionRecord { get; set; }
}
