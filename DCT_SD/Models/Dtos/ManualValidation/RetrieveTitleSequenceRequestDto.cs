namespace DCT_SD.Models.Dtos.ManualValidation;

public class RetrieveTitleSequenceRequestDto
{
    public string Title { get; set; } = string.Empty;
    public string TitleType { get; set; } = string.Empty;
    public string Plan { get; set; } = string.Empty;
    public string Block { get; set; } = string.Empty;
    public string Lot { get; set; } = string.Empty;
}
