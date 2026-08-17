namespace DCT_SD.Models.Entities;

public class OcrExtractionEntry
{
    public int Id { get; set; }
    public int OcrExtractionRecordId { get; set; }
    public string EntryNumber { get; set; } = string.Empty;

    public OcrExtractionRecord OcrExtractionRecord { get; set; } = null!;
}
