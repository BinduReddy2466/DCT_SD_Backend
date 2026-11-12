using DCT_SD.Models.Dtos.SdAutoConversion;

namespace DCT_SD.Services;

// Talks to the external PHILARIS-RD "SD Auto Conversion" service over HTTP (multipart/form-data,
// X-API-Key header) - the second external HTTP dependency in this app, alongside IRdFetchApiClient.
// Base URL + API key are config only (SDAutoConversion:BaseUrl / SDAutoConversion:ApiKey), never
// hardcoded - see Extensions/ServiceCollectionExtensions.cs.
public interface ISdAutoConversionClient
{
    Task<SdAutoConversionResult> CreateSdConversionAsync(SdAutoConversionRequest request, CancellationToken cancellationToken = default);

    // GET /api/SDConversion/GetTitleSequence - same external system, a different endpoint. Used by
    // Manual Validation's "Retrieve Title Sequence" button.
    Task<SdTitleSequenceResult> GetTitleSequenceAsync(SdTitleSequenceRequest request, CancellationToken cancellationToken = default);
}
