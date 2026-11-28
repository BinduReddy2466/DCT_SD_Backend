using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DCT_SD.Models.Dtos.ImageDecryption;

namespace DCT_SD.Services;

public class ImageDecryptionClient : IImageDecryptionClient
{
    private const string RequestUri = "/api/image/GetFileBinaryImage";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ILogger<ImageDecryptionClient> _logger;

    public ImageDecryptionClient(HttpClient httpClient, ILogger<ImageDecryptionClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    // Response shape: { "Filebyte": "<base64 image bytes>" } - per the client's own reference
    // implementation (a dynamic JSON deserialize assigned straight into a byte[]-typed variable
    // with no explicit conversion, which only works implicitly for a Base64 string).
    private class GetFileBinaryImageResponse
    {
        public string? Filebyte { get; set; }
    }

    public async Task<ImageDecryptionResult> GetDecryptedImageAsync(string filePath, CancellationToken cancellationToken = default)
    {
        // Mirrors the client's own reference implementation's path cleanup exactly: collapse every
        // run of backslashes (whether from a Windows UNC path or a manually-joined
        // folder+filename) down to "//". DCT_SD already stores one combined path per document
        // (SourceImagePath), unlike that reference's separate foldername/document_path, so there's
        // no Path.Combine step here - just the same two-step regex applied directly.
        var cleanedPath = Regex.Replace(Regex.Replace(filePath, @"\\{2,}", "//"), @"\\+", "//").Trim();

        var payload = new { Filepath = cleanedPath };
        using var content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsync(RequestUri, content, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "POST {BaseAddress}{RequestUri} failed: could not reach the Image Decryption service.", _httpClient.BaseAddress, RequestUri);
            return new ImageDecryptionResult { IsSuccess = false, ErrorMessage = "Could not reach the Image Decryption service." };
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "POST {BaseAddress}{RequestUri} timed out.", _httpClient.BaseAddress, RequestUri);
            return new ImageDecryptionResult { IsSuccess = false, ErrorMessage = "The Image Decryption service did not respond in time." };
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "POST {BaseAddress}{RequestUri} returned HTTP {StatusCode} for Filepath {Filepath}: {Body}",
                    _httpClient.BaseAddress, RequestUri, (int)response.StatusCode, cleanedPath, body);
                return new ImageDecryptionResult { IsSuccess = false, ErrorMessage = $"The Image Decryption service reported an error (HTTP {(int)response.StatusCode})." };
            }

            GetFileBinaryImageResponse? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<GetFileBinaryImageResponse>(body, JsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "POST {BaseAddress}{RequestUri} returned a response that could not be parsed for Filepath {Filepath}.", _httpClient.BaseAddress, RequestUri, cleanedPath);
                return new ImageDecryptionResult { IsSuccess = false, ErrorMessage = "The Image Decryption service returned a response that could not be understood." };
            }

            if (string.IsNullOrWhiteSpace(parsed?.Filebyte))
            {
                return new ImageDecryptionResult { IsSuccess = false, ErrorMessage = "The Image Decryption service returned an empty response." };
            }

            try
            {
                return new ImageDecryptionResult { IsSuccess = true, Bytes = Convert.FromBase64String(parsed.Filebyte) };
            }
            catch (FormatException ex)
            {
                _logger.LogError(ex, "POST {BaseAddress}{RequestUri} returned a Filebyte value that was not valid Base64 for Filepath {Filepath}.", _httpClient.BaseAddress, RequestUri, cleanedPath);
                return new ImageDecryptionResult { IsSuccess = false, ErrorMessage = "The Image Decryption service returned data that could not be decoded." };
            }
        }
    }
}
