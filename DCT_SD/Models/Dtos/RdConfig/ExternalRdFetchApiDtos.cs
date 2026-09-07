using System.Text.Json.Serialization;

namespace DCT_SD.Models.Dtos.RdConfig;

// Wire shapes for the separate external RD/fetch service (POST /rd-config, POST /fetch/start) -
// snake_case JSON per that service's own API, distinct from this app's own PascalCase DTOs.

public class ExternalUpdateRootPathRequest
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("remarks")]
    public string Remarks { get; set; } = string.Empty;
}

public class ExternalUpdateRootPathResponse
{
    [JsonPropertyName("path")]
    public string? Path { get; set; }
}

public class ExternalStartFetchRequest
{
    [JsonPropertyName("dry_run")]
    public bool DryRun { get; set; }

    [JsonPropertyName("apply_file_moves")]
    public bool ApplyFileMoves { get; set; }
}

// GET /fetch/{fetch_run_id} response shape - the FetchRunSummary schema per the deployed
// service's own OpenAPI spec (http://172.16.1.68:8123/openapi.json). PropertyNameCaseInsensitive
// is set at the deserialization call site as a light safety net, but these names/shapes are
// confirmed from that spec, not guessed.
public class ExternalFetchRunDetailsResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("processed_count")]
    public int? ProcessedCount { get; set; }

    [JsonPropertyName("total_count")]
    public int? TotalCount { get; set; }

    [JsonPropertyName("run_time_seconds")]
    public double? RunTimeSeconds { get; set; }

    [JsonPropertyName("executed_by")]
    public string? ExecutedBy { get; set; }

    [JsonPropertyName("source_path")]
    public string? SourcePath { get; set; }

    [JsonPropertyName("last_processed_folder_path")]
    public string? LastProcessedFolderPath { get; set; }

    [JsonPropertyName("started_at")]
    public DateTime? StartedAt { get; set; }

    [JsonPropertyName("completed_at")]
    public DateTime? CompletedAt { get; set; }
}
