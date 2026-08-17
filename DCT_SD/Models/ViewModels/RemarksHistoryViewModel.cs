using DCT_SD.Models.Dtos.ManualValidation;

namespace DCT_SD.Models.ViewModels;

public class RemarksHistoryViewModel
{
    public int RecordId { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public PagedResult<ManualValidationRemarkDto> Remarks { get; set; } = new();
}
