using DCT_SD.Models.Enums;

namespace DCT_SD.Models.Entities;

public class TitleSequenceLookup
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public TitleType TitleType { get; set; }
    public string Plan { get; set; } = string.Empty;
    public string Block { get; set; } = string.Empty;
    public string Lot { get; set; } = string.Empty;
    public string Sequence { get; set; } = string.Empty;
}
