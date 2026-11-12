using System.Text.Json.Serialization;

namespace DCT_SD.Models.Dtos.SdAutoConversion;

// Outcome of one GetTitleSequence call - same shape/rationale as SdAutoConversionResult.
public class SdTitleSequenceResult
{
    public bool IsSuccess { get; set; }
    public int? HttpStatusCode { get; set; }
    public string? RawResponseJson { get; set; }
    public List<SdTitleSequenceItemDto> Results { get; set; } = [];
    public string? ErrorMessage { get; set; }
}

// One Title Sequence record as returned by the external API. More than one in the same response
// means a genuinely repeating Title Number - the caller (ManualValidationService) surfaces every
// one of them for the user to pick from, same "Repeating Title Number" UX as before.
public class SdTitleSequenceItemDto
{
    public long TitleSeq { get; set; }
    public string? RdCode { get; set; }

    // Named "titleID" in the documented response, but it actually carries the Title Type
    // (e.g. "TCT") - confirmed from the sample response, not a separate identifier.
    [JsonPropertyName("titleID")]
    public string? TitleType { get; set; }

    public string? TitleNo { get; set; }
    public DateTimeOffset? TitleRegistrationDate { get; set; }

    // Plan/Block/Lot as one combined display string (e.g. "PLAN NO: PCS-00-007380 BLOCK NO: LOT
    // NO:") - the API does not return these as separate fields, so this is shown as-is rather
    // than parsed, which would be fragile given the format isn't formally specified.
    public string? Pbl { get; set; }
}

// Fallback envelope shape - mirrors SdAutoConversionResponseEnvelope's { "response": ... }
// convention, in case the real server wraps the array that way rather than returning it bare as
// the pasted documentation example (itself not quite valid JSON) showed.
public class SdTitleSequenceResponseEnvelope
{
    public List<SdTitleSequenceItemDto>? Response { get; set; }
}
