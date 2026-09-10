namespace DCT_SD.Models.Dtos.ManualValidation;

// One row of the "Repeating Title Number" disambiguation window.
public class TitleSequenceCandidateDto
{
    public string? RdCode { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TitleType { get; set; } = string.Empty;
    public string? Plan { get; set; }
    public string? Block { get; set; }
    public string? Lot { get; set; }
    public string Sequence { get; set; } = string.Empty;
}
