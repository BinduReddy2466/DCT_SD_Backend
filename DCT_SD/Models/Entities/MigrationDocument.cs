using DCT_SD.Models.Enums;

namespace DCT_SD.Models.Entities;

public class MigrationDocument
{
    public int Id { get; set; }
    public int MigrationRecordId { get; set; }
    public int? CodeLookupId { get; set; }
    public string DocumentName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public MigrationDocumentStatus Status { get; set; }
    public string? ExistingFileName { get; set; }
    public int? PerformedByUserId { get; set; }
    public string? PerformedByUsername { get; set; }
    public DateTime? ActionDate { get; set; }

    // Populated directly from the SD Auto Conversion API's per-image response
    // (destinationPath/sizeBytes/sequence) - see Services/SystemMigrationService.cs.
    public string? DestinationPath { get; set; }
    public long? SizeBytes { get; set; }
    public int? Sequence { get; set; }

    // The DCT-SD image path submitted to the API (SupportingDocuments[n].file) - the "Source
    // Image File Path", distinct from DestinationPath above (the "Target Image File Path").
    public string? SourceImagePath { get; set; }

    // Set only when Status = Failed (the API never recognized this document) - why it failed.
    public string? ReasonForFailing { get; set; }

    // Per-document Target System Validation - distinct from MigrationRecord.TargetSystemValidation
    // (the Entry Record-level value, also reused as-is for each sibling Title Record, since every
    // Title Record is already its own MigrationRecords row). Added 2026-11-12 so a document-level
    // value can be held independently of its parent record's value, rather than every document in
    // the Supporting Documents table merely echoing the parent's single value. Deliberately left
    // unset by every code path today - the external SD Auto Conversion API's documented response
    // has no field for this at either level; nothing populates it until that's confirmed with the
    // API team.
    public string? TargetSystemValidation { get; set; }

    public MigrationRecord MigrationRecord { get; set; } = null!;
    public CodeLookup? CodeLookup { get; set; }
}
