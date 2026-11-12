using System.Net.Http.Headers;
using System.Text.Json;
using DCT_SD.Models.Dtos.SdAutoConversion;

namespace DCT_SD.Services;

public class SdAutoConversionClient : ISdAutoConversionClient
{
    private const string RequestUri = "/api/SDConversion/CreateSDConversion";
    private const string GetTitleSequenceRequestUri = "/api/SDConversion/GetTitleSequence";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ILogger<SdAutoConversionClient> _logger;

    public SdAutoConversionClient(HttpClient httpClient, ILogger<SdAutoConversionClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    // Unlike RdFetchApiClient (which throws BusinessValidationException so a controller can
    // surface it to the user), this never throws for an ordinary request failure - it always
    // returns a result so the caller (SystemMigrationService, a background batch) can mark that
    // one Entry Record Failed/RetryPending and keep processing the rest of the batch, per the
    // task's explicit "a failure in one Entry Record must not unnecessarily stop processing of
    // other eligible Entry Records."
    public async Task<SdAutoConversionResult> CreateSdConversionAsync(SdAutoConversionRequest request, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent
        {
            { new StringContent(request.RdCode), "RDCode" },
            { new StringContent(request.TitleSeq.ToString(System.Globalization.CultureInfo.InvariantCulture)), "TitleSeq" },
            { new StringContent(request.EntryNumber), "EntryNumber" },
        };

        var openedStreams = new List<Stream>();
        try
        {
            for (var i = 0; i < request.SupportingDocuments.Count; i++)
            {
                var doc = request.SupportingDocuments[i];
                content.Add(new StringContent(doc.DocumentId), $"SupportingDocuments[{i}].documentID");

                var stream = File.OpenRead(doc.ImagePath);
                openedStreams.Add(stream);
                var fileContent = new StreamContent(stream);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                content.Add(fileContent, $"SupportingDocuments[{i}].file", Path.GetFileName(doc.ImagePath));
            }

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.PostAsync(RequestUri, content, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "POST {BaseAddress}{RequestUri} failed: could not reach the SD Auto Conversion service.", _httpClient.BaseAddress, RequestUri);
                return new SdAutoConversionResult { IsSuccess = false, ErrorMessage = "Could not reach the SD Auto Conversion service." };
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "POST {BaseAddress}{RequestUri} timed out.", _httpClient.BaseAddress, RequestUri);
                return new SdAutoConversionResult { IsSuccess = false, ErrorMessage = "The SD Auto Conversion service did not respond in time." };
            }

            using (response)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "POST {BaseAddress}{RequestUri} returned HTTP {StatusCode} for RDCode {RdCode} / EntryNumber {EntryNumber}: {Body}",
                        _httpClient.BaseAddress, RequestUri, (int)response.StatusCode, request.RdCode, request.EntryNumber, body);
                    return new SdAutoConversionResult
                    {
                        IsSuccess = false,
                        HttpStatusCode = (int)response.StatusCode,
                        RawResponseJson = body,
                        ErrorMessage = $"The SD Auto Conversion service reported an error (HTTP {(int)response.StatusCode}).",
                    };
                }

                SdAutoConversionResponseEnvelope? envelope;
                try
                {
                    envelope = JsonSerializer.Deserialize<SdAutoConversionResponseEnvelope>(body, JsonOptions);
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "POST {BaseAddress}{RequestUri} returned a response that could not be parsed for RDCode {RdCode} / EntryNumber {EntryNumber}.", _httpClient.BaseAddress, RequestUri, request.RdCode, request.EntryNumber);
                    return new SdAutoConversionResult
                    {
                        IsSuccess = false,
                        HttpStatusCode = (int)response.StatusCode,
                        RawResponseJson = body,
                        ErrorMessage = "The SD Auto Conversion service returned a response that could not be understood.",
                    };
                }

                return new SdAutoConversionResult
                {
                    IsSuccess = true,
                    HttpStatusCode = (int)response.StatusCode,
                    RawResponseJson = body,
                    Response = envelope?.Response,
                };
            }
        }
        finally
        {
            foreach (var stream in openedStreams)
            {
                await stream.DisposeAsync();
            }
        }
    }

    // GET with a multipart/form-data body - unusual, but matches the documented contract exactly
    // (HttpClient/SocketsHttpHandler have no issue sending a body on GET; it's just built via
    // HttpRequestMessage rather than the GetAsync convenience method, which has none).
    public async Task<SdTitleSequenceResult> GetTitleSequenceAsync(SdTitleSequenceRequest request, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent
        {
            { new StringContent(request.RdCode), "RDCode" },
            { new StringContent(request.TitleType), "TitleType" },
            { new StringContent(request.TitleNo), "TitleNo" },
        };

        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, GetTitleSequenceRequestUri) { Content = content };

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(requestMessage, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "GET {BaseAddress}{RequestUri} failed: could not reach the SD Auto Conversion service.", _httpClient.BaseAddress, GetTitleSequenceRequestUri);
            return new SdTitleSequenceResult { IsSuccess = false, ErrorMessage = "Could not reach the SD Auto Conversion service." };
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "GET {BaseAddress}{RequestUri} timed out.", _httpClient.BaseAddress, GetTitleSequenceRequestUri);
            return new SdTitleSequenceResult { IsSuccess = false, ErrorMessage = "The SD Auto Conversion service did not respond in time." };
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "GET {BaseAddress}{RequestUri} returned HTTP {StatusCode} for RDCode {RdCode} / TitleNo {TitleNo}: {Body}",
                    _httpClient.BaseAddress, GetTitleSequenceRequestUri, (int)response.StatusCode, request.RdCode, request.TitleNo, body);
                return new SdTitleSequenceResult
                {
                    IsSuccess = false,
                    HttpStatusCode = (int)response.StatusCode,
                    RawResponseJson = body,
                    ErrorMessage = $"The SD Auto Conversion service reported an error (HTTP {(int)response.StatusCode}).",
                };
            }

            // Tolerant of two plausible shapes for the same documented sample - a bare array
            // ("[{...}]", matching what was actually pasted) or the same { "response": [...] }
            // envelope CreateSDConversion uses. Tried in that order; whichever parses into a
            // non-null list wins. Avoids a silent, always-empty result if the real server uses the
            // other shape than assumed.
            List<SdTitleSequenceItemDto>? results = null;
            try
            {
                results = JsonSerializer.Deserialize<List<SdTitleSequenceItemDto>>(body, JsonOptions);
            }
            catch (JsonException)
            {
                // fall through to the enveloped-shape attempt below
            }

            if (results is null)
            {
                try
                {
                    var envelope = JsonSerializer.Deserialize<SdTitleSequenceResponseEnvelope>(body, JsonOptions);
                    results = envelope?.Response;
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "GET {BaseAddress}{RequestUri} returned a response that could not be parsed for RDCode {RdCode} / TitleNo {TitleNo}.", _httpClient.BaseAddress, GetTitleSequenceRequestUri, request.RdCode, request.TitleNo);
                    return new SdTitleSequenceResult
                    {
                        IsSuccess = false,
                        HttpStatusCode = (int)response.StatusCode,
                        RawResponseJson = body,
                        ErrorMessage = "The SD Auto Conversion service returned a response that could not be understood.",
                    };
                }
            }

            return new SdTitleSequenceResult
            {
                IsSuccess = true,
                HttpStatusCode = (int)response.StatusCode,
                RawResponseJson = body,
                Results = results ?? [],
            };
        }
    }
}
