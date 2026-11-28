namespace DCT_SD.Models.Dtos.ImageDecryption;

// Outcome of one GetFileBinaryImage call. Mirrors SdAutoConversionResult's shape (IsSuccess + an
// ErrorMessage instead of throwing for an ordinary request failure - network error, non-2xx
// status, or a response that didn't parse) so a missing/unreachable image is just another reason
// to fall through to the same "not found" response the caller already used for a literally
// missing file, never a thrown exception a controller action would need to catch separately.
public class ImageDecryptionResult
{
    public bool IsSuccess { get; set; }
    public byte[]? Bytes { get; set; }
    public string? ErrorMessage { get; set; }
}
