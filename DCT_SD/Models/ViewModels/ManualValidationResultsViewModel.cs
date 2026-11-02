using DCT_SD.Models.Dtos.ManualValidation;

namespace DCT_SD.Models.ViewModels;

// Same purpose as FetchHistoryResultsViewModel/RootHistoryResultsViewModel: lets _Results.cshtml
// distinguish "no entry records require manual validation at all" from "this search matched
// nothing" without an extra query - HasAppliedFilters is simply whether the request carried any
// RdCode/EntryNumbersCsv/DateFrom/DateTo criteria (the only filters the Manual Validation page
// itself exposes) - an unfiltered search that still returns zero rows can only mean there are no
// records awaiting manual validation.
public class ManualValidationResultsViewModel
{
    public PagedResult<ManualValidationListItemDto> Result { get; set; } = new();
    public bool HasAppliedFilters { get; set; }
}
