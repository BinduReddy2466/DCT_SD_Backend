using DCT_SD.Models.Enums;

namespace DCT_SD.Models.Entities;

public class ManualValidationRemark
{
    public int Id { get; set; }
    public int ManualValidationRequestId { get; set; }
    public RemarkAction Action { get; set; }
    public string Remarks { get; set; } = string.Empty;
    public int ByUserId { get; set; }
    public string ByUsername { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public ManualValidationRequest ManualValidationRequest { get; set; } = null!;
}
