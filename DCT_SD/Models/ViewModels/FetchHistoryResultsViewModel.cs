using DCT_SD.Models.Dtos.RdConfig;

namespace DCT_SD.Models.ViewModels;

// Same purpose as RootHistoryResultsViewModel: lets _FetchHistoryResults.cshtml distinguish "the
// table has no fetch runs at all" from "this search matched nothing" without an extra query -
// HasAppliedFilters is simply whether the request carried any DateFrom/DateTo/ExecutedBy
// criteria - an unfiltered search that still returns zero rows can only mean the underlying
// table is empty.
public class FetchHistoryResultsViewModel
{
    public PagedResult<FetchRunItemDto> Result { get; set; } = new();
    public bool HasAppliedFilters { get; set; }
}
