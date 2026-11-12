namespace DCT_SD.Models.Enums;

public enum MigrationDocumentStatus
{
    Migrated = 1,
    DuplicateSd = 2,

    // No longer a reachable action (replaced by ConfirmedDuplicate/InsertedAsNew below per the
    // Manual Corrections/Duplicate Resolution spec) - kept only so pre-existing rows already in
    // this status keep displaying correctly.
    Overwritten = 3,

    // Displays as "Inserted" (not "Inserted as New") - see StatusDisplay.MigrationDocStatusMap.
    InsertedAsNew = 4,

    // The SD Auto Conversion API never returned this document (missing from its Images response,
    // or returned with a status other than the documented "Inserted"/"Duplicate SD") - see
    // ReasonForFailing for why. Distinct from Pending (below), which means no attempt has been
    // made at all yet.
    Failed = 5,

    // A Duplicate SD image the user explicitly chose to leave alone (PHILARIS-RD's existing image
    // is kept, this DCT-SD image is never migrated) - see MigrationService.ConfirmDuplicateAsync.
    ConfirmedDuplicate = 6,

    // Still classified "Others" at submission time - excluded from the SD Auto Conversion request
    // entirely (never attempted), not merely a failure. Becomes correctable again (and no longer
    // NotApplicable) via the existing Document Type correction flow, same as any other
    // CodeLookupId-null document.
    NotApplicable = 7,

    // Created the moment this record enters Migration Monitoring, before the external API is ever
    // called - see the pre-creation logic in ManualValidationService.MarkReadyForMigrationAsync /
    // MigrationService.SyncEligibleOcrExtractionRecordsAsync. Replaces the old
    // display-only "Pending" that MigrationService.GetByIdAsync used to synthesize on the fly from
    // Document_Json for a record with no real MigrationDocuments rows yet - that fallback no
    // longer exists, since a real Pending row now always exists from creation.
    Pending = 8,
}
