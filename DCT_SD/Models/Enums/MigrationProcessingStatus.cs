namespace DCT_SD.Models.Enums;

// Tracks the System Migration batch's own claim/attempt lifecycle for a MigrationRecords row,
// independent of MigrationStatus (which describes a completed migration's outcome - existing vs
// new title/entry record - and only gets a real value once ProcessingStatus reaches Completed).
public enum MigrationProcessingStatus
{
    Pending = 1,
    Processing = 2,
    Completed = 3,
    Failed = 4,
    RetryPending = 5
}
