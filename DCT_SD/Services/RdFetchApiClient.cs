using System.Net.Http.Json;
using System.Text.Json;
using DCT_SD.Helpers.Exceptions;
using DCT_SD.Models.Dtos.RdConfig;

namespace DCT_SD.Services;

public class RdFetchApiClient : IRdFetchApiClient
{
    private readonly HttpClient _httpClient;

    public RdFetchApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ExternalUpdateRootPathResponse> UpdateRootPathAsync(string path, string remarks, CancellationToken cancellationToken = default)
    {
        var request = new ExternalUpdateRootPathRequest { Path = path, Remarks = remarks };
        return await PostAsync<ExternalUpdateRootPathRequest, ExternalUpdateRootPathResponse>("/rd-config", request, cancellationToken);
    }

    private static readonly JsonSerializerOptions DetailsJsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<HttpResponseMessage> StartFetchStreamAsync(CancellationToken cancellationToken = default)
    {
        // Always false/false per the integration requirement - file moves and dry-run mode are
        // not enabled from this button.
        var request = new ExternalStartFetchRequest { DryRun = false, ApplyFileMoves = false };

        HttpResponseMessage response;
        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/fetch/start")
            {
                Content = JsonContent.Create(request),
            };
            // ResponseHeadersRead: return as soon as headers arrive instead of buffering the
            // whole (potentially very long-running) SSE body first - the caller streams it live.
            response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (HttpRequestException)
        {
            throw new BusinessValidationException(
                "Could not reach the fetch service. Make sure you're connected to Paradigm WiFi or VPN and try again.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new BusinessValidationException(
                "The fetch service did not respond in time. Make sure you're connected to Paradigm WiFi or VPN and try again.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var message = await BuildErrorMessageAsync(response, cancellationToken);
            response.Dispose();
            throw new BusinessValidationException(message);
        }

        return response;
    }

    public async Task<FetchRunDetailDto?> GetFetchRunDetailsAsync(int fetchRunId, CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync($"/fetch/{fetchRunId}", cancellationToken);
        }
        catch (HttpRequestException)
        {
            throw new BusinessValidationException(
                "Could not reach the fetch service. Make sure you're connected to Paradigm WiFi or VPN and try again.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new BusinessValidationException(
                "The fetch service did not respond in time. Make sure you're connected to Paradigm WiFi or VPN and try again.");
        }

        using (response)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new BusinessValidationException(await BuildErrorMessageAsync(response, cancellationToken));
            }

            ExternalFetchRunDetailsResponse? parsed;
            try
            {
                parsed = await response.Content.ReadFromJsonAsync<ExternalFetchRunDetailsResponse>(DetailsJsonOptions, cancellationToken);
            }
            catch (JsonException)
            {
                throw new BusinessValidationException("The fetch service returned a response that could not be understood.");
            }

            if (parsed is null)
            {
                throw new BusinessValidationException("The fetch service returned an unexpected empty response.");
            }

            return new FetchRunDetailDto
            {
                Id = parsed.Id,
                Status = parsed.Status ?? string.Empty,
                ProcessedCount = parsed.ProcessedCount ?? 0,
                TotalCount = parsed.TotalCount,
                RunTime = FormatRunTime(parsed.RunTimeSeconds),
                ExecutedBy = parsed.ExecutedBy ?? string.Empty,
                SourcePath = parsed.SourcePath ?? string.Empty,
                LastProcessedFolderPath = parsed.LastProcessedFolderPath,
                StartedAt = parsed.StartedAt ?? default,
                CompletedAt = parsed.CompletedAt,
            };
        }
    }

    // Mirrors RdConfigService's own FormatRunTime bucketing (h/m/s), just fed a duration
    // directly - the deployed service reports run_time_seconds as a number, not a
    // pre-formatted string.
    private static string? FormatRunTime(double? seconds)
    {
        if (seconds is null)
        {
            return null;
        }

        var span = TimeSpan.FromSeconds(seconds.Value);
        return span.TotalHours >= 1
            ? $"{(int)span.TotalHours}h {span.Minutes}m {span.Seconds}s"
            : span.TotalMinutes >= 1
                ? $"{span.Minutes}m {span.Seconds}s"
                : $"{span.Seconds}s";
    }

    private async Task<TResponse> PostAsync<TRequest, TResponse>(string requestUri, TRequest body, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync(requestUri, body, cancellationToken);
        }
        catch (HttpRequestException)
        {
            throw new BusinessValidationException(
                "Could not reach the fetch service. Make sure you're connected to Paradigm WiFi or VPN and try again.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new BusinessValidationException(
                "The fetch service did not respond in time. Make sure you're connected to Paradigm WiFi or VPN and try again.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new BusinessValidationException(await BuildErrorMessageAsync(response, cancellationToken));
        }

        try
        {
            var result = await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: cancellationToken);
            return result ?? throw new BusinessValidationException("The fetch service returned an unexpected empty response.");
        }
        catch (JsonException)
        {
            throw new BusinessValidationException("The fetch service returned a response that could not be understood.");
        }
    }

    // The external service's error shape isn't documented beyond "returns 400" - this covers
    // the common conventions (a plain "detail" string, or a list of {msg} validation errors)
    // and falls back to the raw body or the status code if neither shape matches.
    private static async Task<string> BuildErrorMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(body))
        {
            return $"The fetch service reported an error (HTTP {(int)response.StatusCode}).";
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("detail", out var detail))
            {
                if (detail.ValueKind == JsonValueKind.String)
                {
                    return detail.GetString()!;
                }

                if (detail.ValueKind == JsonValueKind.Array)
                {
                    var messages = detail.EnumerateArray()
                        .Select(e => e.TryGetProperty("msg", out var msg) ? msg.GetString() : null)
                        .Where(m => !string.IsNullOrWhiteSpace(m));
                    var joined = string.Join(" ", messages);
                    if (!string.IsNullOrWhiteSpace(joined))
                    {
                        return joined;
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Not JSON - fall through to returning the raw body below.
        }

        return body.Length <= 300 ? body : $"The fetch service reported an error (HTTP {(int)response.StatusCode}).";
    }
}
