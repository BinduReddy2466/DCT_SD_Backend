namespace DCT_SD.Models.Dtos.SdAutoConversion;

// GET /api/SDConversion/GetTitleSequence - multipart/form-data body (unusual for a GET, but
// matches the documented contract exactly).
public class SdTitleSequenceRequest
{
    public string RdCode { get; set; } = string.Empty;
    public string TitleType { get; set; } = string.Empty;
    public string TitleNo { get; set; } = string.Empty;
}
