using DCT_SD.Models.Dtos.ImageDecryption;

namespace DCT_SD.Services;

// Talks to the client's existing Image Decryption API (GetFileBinaryImage) - the third external
// HTTP dependency in this app, alongside IRdFetchApiClient and ISdAutoConversionClient. Base URL
// is config only (ImageDecryption:BaseUrl), never hardcoded - see
// Extensions/ServiceCollectionExtensions.cs. The single common entry point every supporting-
// document image viewer (Migration Monitoring, Manual Validation) goes through, so decryption
// logic lives here exactly once rather than being duplicated per controller.
public interface IImageDecryptionClient
{
    Task<ImageDecryptionResult> GetDecryptedImageAsync(string filePath, CancellationToken cancellationToken = default);
}
