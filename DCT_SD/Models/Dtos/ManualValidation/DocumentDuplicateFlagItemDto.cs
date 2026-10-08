namespace DCT_SD.Models.Dtos.ManualValidation;

// One entry of SaveManualValidationRequestDto.DocumentDuplicateFlagsJson - a single Duplicate
// checkbox's current state for one supporting document, submitted only as part of Save. Targets
// its document by DocumentId + RenamedFileName (not position/index - the Supporting Documents
// list can be sorted/reordered, and unlike a Document Type correction this never renames a file,
// so there's no stable position to resolve against in the first place).
public class DocumentDuplicateFlagItemDto
{
    public string? DocumentId { get; set; }
    public string RenamedFileName { get; set; } = string.Empty;
    public bool IsDuplicate { get; set; }
}
