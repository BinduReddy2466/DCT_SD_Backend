namespace DCT_SD.Models.Dtos.ManualValidation;

// One row of the "Repeating Title Number" disambiguation window.
public class TitleSequenceCandidateDto
{
    public string? RdCode { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TitleType { get; set; } = string.Empty;

    // Combined Plan/Block/Lot display text as returned by the external GetTitleSequence API
    // (e.g. "PLAN NO: PCS-00-007380 BLOCK NO: LOT NO:") - not parsed into separate fields, since
    // the API doesn't return them separately and the format isn't formally specified.
    public string? Pbl { get; set; }

    public string Sequence { get; set; } = string.Empty;
}
