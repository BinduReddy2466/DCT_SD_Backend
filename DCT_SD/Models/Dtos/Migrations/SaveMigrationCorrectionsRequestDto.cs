namespace DCT_SD.Models.Dtos.Migrations;

public class SaveMigrationCorrectionsRequestDto
{
    // Only applied when MigrationDetailDto.IsTargetRdNotIdentified is true for this record -
    // rejected server-side otherwise (see MigrationService.SaveCorrectionsAsync).
    public string? RdCode { get; set; }

    // Zero or more pending Others -> real Document Type corrections, submitted only as part of
    // Save - same deferred-until-Save convention as Manual Validation's DocumentChangesJson.
    // JSON-serialized client-side as an array of DocumentCorrectionItemDto.
    public string? DocumentCorrectionsJson { get; set; }
}
