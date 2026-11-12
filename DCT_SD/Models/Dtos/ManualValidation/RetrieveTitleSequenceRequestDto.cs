namespace DCT_SD.Models.Dtos.ManualValidation;

public class RetrieveTitleSequenceRequestDto
{
    // Identifies which Title Record row (ManualValidationRequest.Id) a single-match result is
    // persisted to - never guessed from position, since a record can have several Title Record
    // rows open on the same page at once.
    public int RecordId { get; set; }

    public string RdCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string TitleType { get; set; } = string.Empty;
}
