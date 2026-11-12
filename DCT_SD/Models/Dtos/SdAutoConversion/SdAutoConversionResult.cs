namespace DCT_SD.Models.Dtos.SdAutoConversion;

// Outcome of one CreateSDConversion call. IsSuccess means "got a parseable HTTP success
// response" - it says nothing about whether every supporting document actually migrated; the
// caller (SystemMigrationService) evaluates Response.Images itself per-document before deciding
// whether the whole Entry Record is Completed, per the task's explicit instruction not to treat
// HTTP 200 alone as success.
public class SdAutoConversionResult
{
    public bool IsSuccess { get; set; }
    public int? HttpStatusCode { get; set; }

    // The raw response body, preserved verbatim for ApiResponseJson - "preserve the complete
    // original API response for troubleshooting and auditing" (task section 4).
    public string? RawResponseJson { get; set; }
    public SdAutoConversionResponseDto? Response { get; set; }
    public string? ErrorMessage { get; set; }
}
