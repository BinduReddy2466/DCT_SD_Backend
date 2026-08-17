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
    public string? Title { get; set; }
    public TitleType? TitleType { get; set; }
    public string? Plan { get; set; }
    public string? Block { get; set; }
    public string? Lot { get; set; }
    public string? TitleSequence { get; set; }

    public ManualValidationStatus Status { get; set; }
    public string MissingFieldsCsv { get; set; } = string.Empty;
    public DateTime ExtractionDate { get; set; }

    public int? UpdatedByUserId { get; set; }
    public string? UpdatedByUsername { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public int? LockedByUserId { get; set; }
    public string? LockedByUsername { get; set; }
    public DateTime? LockedAt { get; set; }

    public DateTime? MigratedAt { get; set; }

    public OcrExtractionRecord? OcrExtractionRecord { get; set; }
    public ICollection<ManualValidationDocument> Documents { get; set; } = new List<ManualValidationDocument>();
    public ICollection<ManualValidationRemark> RemarksHistory { get; set; } = new List<ManualValidationRemark>();
}
