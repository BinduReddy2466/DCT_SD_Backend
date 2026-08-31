namespace DCT_SD.Models.Enums;

public enum OcrExtractionStatus
{
    // No live row currently uses 0 (existing data is only 1/2) - safe to claim it for Failed
    // without touching the OcrExtractionRecords schema, since the column is a plain int.
    Failed = 0,
    FullyExtracted = 1,
    PartiallyExtracted = 2
}
