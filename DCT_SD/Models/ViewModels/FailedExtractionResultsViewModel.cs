using DCT_SD.Models.Dtos.FailedExtraction;

namespace DCT_SD.Models.ViewModels;

// Distinguishes "nothing has ever failed" (show "No failed extraction records found.") from
// "this search matched nothing" (show "No records found.") - both render as an empty table.
public class FailedExtractionResultsViewModel
{
    public PagedResult<FailedExtractionListItemDto> Result { get; set; } = new();
    public bool HasAnyRecords { get; set; }
}
