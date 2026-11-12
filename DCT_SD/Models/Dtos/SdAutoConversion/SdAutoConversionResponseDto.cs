namespace DCT_SD.Models.Dtos.SdAutoConversion;

// Mirrors the API guide's CreateSDConversion success-response shape exactly (SD_Conversion_API_
// Guide_v1.docx (1).pdf, section 5.1): { "response": { rdCode, totalImagesCopied, processingDate,
// Images: [...] } }. Deserialized case-insensitively (JsonSerializerDefaults.Web), so the PDF's
// own inconsistent "Images" (capitalized, unlike every other camelCase field) still binds fine.
public class SdAutoConversionResponseEnvelope
{
    public SdAutoConversionResponseDto? Response { get; set; }
}

public class SdAutoConversionResponseDto
{
    public string? RdCode { get; set; }
    public int TotalImagesCopied { get; set; }
    public DateTimeOffset? ProcessingDate { get; set; }
    public List<SdAutoConversionImageDto> Images { get; set; } = [];
}

public class SdAutoConversionImageDto
{
    public int Sequence { get; set; }
    public string? DocumentId { get; set; }
    public string? DestinationPath { get; set; }
    public string? FileName { get; set; }
    public long SizeBytes { get; set; }

    // Documented values: "Inserted", "Duplicate SD". No documented "Failed" value at the
    // per-image level - see Services/SystemMigrationService.cs for how an image missing from
    // this list entirely (rather than present with an unexpected status) is handled.
    public string? Status { get; set; }
}
