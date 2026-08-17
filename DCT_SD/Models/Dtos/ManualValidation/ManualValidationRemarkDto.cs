namespace DCT_SD.Models.Dtos.ManualValidation;

public class ManualValidationRemarkDto
{
    public int Id { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string By { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
}
