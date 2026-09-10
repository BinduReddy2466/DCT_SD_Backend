namespace DCT_SD.Models.Dtos.ManualValidation;

public class TitleSequenceDto
{
    public string Sequence { get; set; } = string.Empty;

    // True when more than one Title Sequence record still matches after applying every
    // applicable criterion (RD Code + Title Number + Title Type, then Plan/Block/Lot) - the
    // "Repeating Title Number" case. Sequence is empty and Candidates holds every remaining
    // match for the user to pick from.
    public bool IsAmbiguous { get; set; }
    public IReadOnlyList<TitleSequenceCandidateDto> Candidates { get; set; } = Array.Empty<TitleSequenceCandidateDto>();
}
