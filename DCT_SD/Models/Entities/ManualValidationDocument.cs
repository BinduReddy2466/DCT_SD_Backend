namespace DCT_SD.Models.Entities;

public class ManualValidationDocument
{
    public int Id { get; set; }
    public int ManualValidationRequestId { get; set; }
    public int? DocumentTypeId { get; set; }
    public string DocumentName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;

    public ManualValidationRequest ManualValidationRequest { get; set; } = null!;
    public DocumentType? DocumentType { get; set; }
}
