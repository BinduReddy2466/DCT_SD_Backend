using DCT_SD.Models.Dtos.RdConfig;

namespace DCT_SD.Models.ViewModels;

// Lets _RootHistoryResults.cshtml distinguish "the table has no history rows at all" from
// "this search matched nothing" without an extra query: HasAppliedFilters is simply whether
// the request carried any DateFrom/DateTo/ModifiedBy criteria - an unfiltered search that
// still returns zero rows can only mean the underlying table is empty.
public class RootHistoryResultsViewModel
{
    public PagedResult<RootPathHistoryItemDto> Result { get; set; } = new();
    public bool HasAppliedFilters { get; set; }
}
