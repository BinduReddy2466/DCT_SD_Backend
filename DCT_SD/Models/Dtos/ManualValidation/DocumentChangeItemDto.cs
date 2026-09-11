namespace DCT_SD.Models.Dtos.ManualValidation;

// One entry of SaveManualValidationRequestDto.DocumentChangesJson - a single pending
// "Others" -> real Document Type correction for one supporting document.
public class DocumentChangeItemDto
{
    public int Index { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
