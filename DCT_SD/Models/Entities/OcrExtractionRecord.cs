using DCT_SD.Models.Enums;

namespace DCT_SD.Models.Entities;

public class OcrExtractionRecord
{
    public int Id { get; set; }
    public string RequestNumber { get; set; } = string.Empty;

    public int? FetchRunId { get; set; }
    public string? RdCode { get; set; }
    public string? RdName { get; set; }
    public string FolderPath { get; set; } = string.Empty;
    public string? EntryNumbersCsv { get; set; }
    // Maps to the EntryNumberRange column already present in the deployed schema (added via
    // DBChanges/dct_sd_main.sql, not an EF migration - same column/purpose as
    // ManualValidationRequest.EntryNumberRange).
    public string? EntryNumberRange { get; set; }
    public string? TitleNumber { get; set; }
    public TitleType? TitleType { get; set; }
    // Added 2026-11-12: this table never had these, unlike ManualValidationRequests' own
    // Plan/Block/Lot columns - confirmed with the client that the external OCR/fetch pipeline
    // does capture this data for fully-extracted records too, it just had nowhere to be stored.
    public string? Plan { get; set; }
    public string? Block { get; set; }
    public string? Lot { get; set; }
    public string? TitleSequence { get; set; }
    public int DocumentCount { get; set; }
    public OcrExtractionStatus ExtractionStatus { get; set; }
    public DateTime ExtractionDateTime { get; set; }
    public string? DocumentsJson { get; set; }

    public FetchRun? FetchRun { get; set; }
    public ICollection<ManualValidationRequest> ManualValidationRequests { get; set; } = new List<ManualValidationRequest>();
    public ICollection<MigrationRecord> MigrationRecords { get; set; } = new List<MigrationRecord>();
}
