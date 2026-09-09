using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DCT_SD.Helpers.Exceptions;
using DCT_SD.Models;
using DCT_SD.Models.Dtos.RdConfig;
using DCT_SD.Models.ViewModels;
using DCT_SD.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace DCT_SD.Controllers;

[Authorize(Policy = $"Menu:{MenuKeys.RdConfig}")]
public class RdConfigController : Controller
{
    private readonly IRdConfigService _rdConfigService;
    private readonly IRdFetchApiClient _rdFetchApiClient;
    private readonly IFailedExtractionService _failedExtractionService;
    private readonly ILogger<RdConfigController> _logger;

    public RdConfigController(
        IRdConfigService rdConfigService,
        IRdFetchApiClient rdFetchApiClient,
        IFailedExtractionService failedExtractionService,
        ILogger<RdConfigController> logger)
    {
        _rdConfigService = rdConfigService;
        _rdFetchApiClient = rdFetchApiClient;
        _failedExtractionService = failedExtractionService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = await BuildIndexViewModelAsync(cancellationToken);
        return View(model);
    }

    private async Task<RdConfigIndexViewModel> BuildIndexViewModelAsync(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Fetching Management";
        ViewData["ActiveMenu"] = MenuKeys.RdConfig;

        var rootPath = await _rdConfigService.GetCurrentRootPathAsync(cancellationToken);
        var rootHistory = await _rdConfigService.SearchRootPathHistoryAsync(new RootPathHistorySearchRequestDto { PageNumber = 1, PageSize = 25 }, cancellationToken);
        var fetchHistory = await _rdConfigService.SearchFetchHistoryAsync(new FetchHistorySearchRequestDto { PageNumber = 1, PageSize = 25 }, cancellationToken);

        return new RdConfigIndexViewModel
        {
            CurrentPath = rootPath.CurrentPath,
            LatestUpdate = rootHistory.Items.FirstOrDefault(),
            FetchHistory = fetchHistory,
            RootHistory = rootHistory,
            RootPathForm = new RootPathFormViewModel { RootPath = rootPath.CurrentPath ?? string.Empty },
        };
    }

    [HttpGet]
    public async Task<IActionResult> BrowseFolders(string? path, CancellationToken cancellationToken)
    {
        var result = await _rdConfigService.BrowseDirectoriesAsync(path, cancellationToken);
        return PartialView("_BrowseFolder", result);
    }

    [HttpGet]
    public async Task<IActionResult> FetchHistoryResults([FromQuery] FetchHistorySearchRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _rdConfigService.SearchFetchHistoryAsync(request, cancellationToken);
        return PartialView("_FetchHistoryResults", result);
    }

    [HttpGet]
    public async Task<IActionResult> RootHistoryResults([FromQuery] RootPathHistorySearchRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _rdConfigService.SearchRootPathHistoryAsync(request, cancellationToken);
        var hasAppliedFilters = request.DateFrom.HasValue || request.DateTo.HasValue || !string.IsNullOrWhiteSpace(request.ModifiedBy);
        return PartialView("_RootHistoryResults", new RootHistoryResultsViewModel { Result = result, HasAppliedFilters = hasAppliedFilters });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateRootPath([Bind(Prefix = "RootPathForm")] RootPathFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var invalidModel = await BuildIndexViewModelAsync(cancellationToken);
            invalidModel.RootPathForm = model;
            return View("Index", invalidModel);
        }

        var path = model.RootPath.Trim();
        var remarks = model.Remarks.Trim();

        // Checked locally, before ever calling the external service: resubmitting the path
        // that's already configured is a no-op. Without this, the old flow called the external
        // API unconditionally, then silently swallowed the local mirror's own "no change"
        // rejection and told the user it succeeded anyway - a false positive, and an unnecessary
        // external call/history entry on the external side for every re-submission.
        var current = await _rdConfigService.GetCurrentRootPathAsync(cancellationToken);
        if (string.Equals(current.CurrentPath, path, StringComparison.OrdinalIgnoreCase))
        {
            TempData["ToastMessage"] = "The selected Root Source Path is the same as the current configuration. No changes have been made.";
            TempData["ToastVariant"] = "default";
            return RedirectToAction("Index");
        }

        try
        {
            // The external service is now authoritative for whether this update succeeds.
            var executedByUserId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId) ? parsedUserId : 0;
            var response = await _rdFetchApiClient.UpdateRootPathAsync(path, remarks, executedByUserId, cancellationToken);

            // Mirror the confirmed change into the existing local history table so the rest of
            // this page - the Root Source Path field, "Last Updated", and the History tab -
            // keeps rendering exactly as it always has, now reflecting the external result.
            try
            {
                await _rdConfigService.UpdateRootPathAsync(
                    new UpdateRootPathRequestDto { NewPath = response.Path ?? path, Remarks = remarks },
                    cancellationToken);
            }
            catch (BusinessValidationException)
            {
                // The external service's own resolved path ended up matching what's already
                // recorded locally (e.g. it normalized the path differently than expected) - the
                // external update itself still succeeded, so this isn't worth surfacing as an
                // error on top of that.
            }

            TempData["ToastMessage"] = "Root Source Path has been updated successfully.";
            TempData["ToastVariant"] = "success";
        }
        catch (BusinessValidationException ex)
        {
            TempData["ToastMessage"] = ex.Message;
            TempData["ToastVariant"] = "default";
        }

        return RedirectToAction("Index");
    }

    // Streams POST /fetch/start's Server-Sent Events straight through to the browser as they
    // arrive (no buffering, no waiting for the run to finish) so the page can render live
    // progress. This is a plain fetch() POST from JS, not a native form submit - the antiforgery
    // token travels as a form field, same as every other fetch()-driven action in this app.
    // rootPath is whatever the RD Configuration UI's Root Source Path field showed at the moment
    // Start Fetching was clicked (never hardcoded here) - passed straight through as
    // /fetch/start's "path" field.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartFetchStream(string? rootPath, CancellationToken cancellationToken)
    {
        rootPath = rootPath?.Trim();
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return BadRequest(new { message = "The root source path must be configured before starting a fetch." });
        }

        FetchRunItemDto localRun;
        try
        {
            // Reuses the existing guard (root path configured, no other run already Ongoing)
            // and creates the local FetchRuns mirror row up front, exactly as the old
            // synchronous StartFetch action did.
            localRun = await _rdConfigService.StartFetchAsync(cancellationToken);
        }
        catch (BusinessValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        HttpResponseMessage externalResponse;
        try
        {
            var executedByUserId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId) ? parsedUserId : 0;
            externalResponse = await _rdFetchApiClient.StartFetchStreamAsync(rootPath, executedByUserId, cancellationToken);
        }
        catch (BusinessValidationException ex)
        {
            // Nothing has been written to the response yet, so this can still be a normal JSON
            // error - but the local "Ongoing" row from above must not be left stuck that way
            // (SearchFetchHistoryAsync's own hasOngoing guard would then permanently block every
            // future attempt to start a fetch).
            await _rdConfigService.FailFetchRunAsync(localRun.Id, ex.Message, CancellationToken.None);
            return BadRequest(new { message = ex.Message });
        }
        catch (OperationCanceledException)
        {
            // The caller (browser) disconnected before the external service even responded - not
            // a BusinessValidationException, so it wouldn't be caught above, but the local row
            // still must not be left stuck Ongoing for the same reason.
            await _rdConfigService.FailFetchRunAsync(localRun.Id, cancellationToken: CancellationToken.None);
            throw;
        }

        using (externalResponse)
        {
            Response.ContentType = "text/event-stream";
            Response.Headers.CacheControl = "no-cache";
            Response.Headers["X-Accel-Buffering"] = "no"; // no-op locally, prevents buffering behind an nginx-style reverse proxy

            // The IIS in-process hosting model (the default when this app is published behind
            // IIS, per Setup-DCT_SD-IIS.ps1) buffers the response body by default - each
            // Response.Body.WriteAsync below would sit in that buffer instead of reaching the
            // browser until either the buffer fills or the response ends, which is exactly the
            // "nothing shows until the whole fetch finishes" symptom. This is the documented
            // opt-out for streaming responses; harmless (no-op) under Kestrel/out-of-process
            // hosting, where nothing buffers this way to begin with.
            HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

            RunCompleteInfo? runComplete = null;
            string? runFailureReason = null;
            var streamedOk = true;
            try
            {
                await using var upstream = await externalResponse.Content.ReadAsStreamAsync(cancellationToken);
                var buffer = new byte[8192];
                var frameBuilder = new StringBuilder();

                int bytesRead;
                while ((bytesRead = await upstream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await Response.Body.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);

                    frameBuilder.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                    var (foundRunComplete, failures, foundFailureReason) = ProcessCompleteRecords(frameBuilder, localRun.Id, _logger);
                    runComplete ??= foundRunComplete;
                    runFailureReason ??= foundFailureReason;

                    foreach (var failure in failures)
                    {
                        // localRun.Id is always 0 now (no local row is ever persisted - see
                        // RdConfigService.StartFetchAsync), so there's no real FetchRuns id to
                        // link this failure to; OcrExtractionRecord.FetchRunId is nullable for
                        // exactly this reason.
                        await RecordFolderFailureAsync(null, failure, cancellationToken);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Headers (text/event-stream) are already sent, so this can no longer become a
                // JSON error response - the client sees the connection simply end and falls back
                // to its own "stream closed" handling. Still log the real cause: this is exactly
                // the case where a fetch run appears to just "stop" with no explanation anywhere
                // else.
                _logger.LogError(ex, "SSE stream for fetch run {LocalFetchRunId} ended unexpectedly while relaying /fetch/start.", localRun.Id);
                streamedOk = false;
            }
            catch (OperationCanceledException)
            {
                // The caller (browser) disconnected before the run finished (tab closed,
                // navigated away, network drop) - expected, not an error worth logging.
                streamedOk = false;
            }

            // The run has finished (successfully, or the connection dropped) as far as this
            // request is concerned. Reconcile the local mirror row with the authoritative final
            // state so the existing Fetch History table reflects it without the page reloading.
            if (runComplete is { FetchRunId: { } fetchRunId })
            {
                try
                {
                    var details = await _rdFetchApiClient.GetFetchRunDetailsAsync(fetchRunId, CancellationToken.None);
                    if (details is not null)
                    {
                        await _rdConfigService.CompleteFetchRunAsync(localRun.Id, details, runFailureReason, CancellationToken.None);
                    }
                    else
                    {
                        // GET /fetch/{id} came back 404 for an id run_complete itself just gave
                        // us - fall back to the run_complete payload's own fields rather than
                        // treating an already-known-successful run as failed.
                        await _rdConfigService.CompleteFetchRunAsync(localRun.Id, runComplete.ToDetailDto(fetchRunId), runFailureReason, CancellationToken.None);
                    }
                }
                catch (BusinessValidationException)
                {
                    await _rdConfigService.CompleteFetchRunAsync(localRun.Id, runComplete.ToDetailDto(fetchRunId), runFailureReason, CancellationToken.None);
                }
            }
            else if (runComplete is not null)
            {
                // run_complete arrived but without a fetch_run_id (e.g. "nothing new to
                // process" - no run was actually created on the external side) - there's no id
                // to query GET /fetch/{id} with, so reconcile directly from this payload.
                await _rdConfigService.CompleteFetchRunAsync(localRun.Id, runComplete.ToDetailDto(null), runFailureReason, CancellationToken.None);
            }
            else if (!streamedOk)
            {
                await _rdConfigService.FailFetchRunAsync(localRun.Id, runFailureReason, CancellationToken.None);
            }
        }

        return new EmptyResult();
    }

    private sealed record FolderFailure(string? FolderPath, string? RdCode, string? RdName, string? FailureReason);

    // Captured from a run_complete SSE record. FetchRunId is null when the external service
    // never assigned one for this run (e.g. it found nothing new to process) - the other fields
    // are still meaningful in that case and are what CompleteFetchRunAsync falls back to.
    private sealed record RunCompleteInfo(int? FetchRunId, string? Status, int? ProcessedCount, int? TotalCount)
    {
        public FetchRunDetailDto ToDetailDto(int? fetchRunId) => new()
        {
            Id = fetchRunId ?? 0,
            Status = Status ?? "Completed",
            ProcessedCount = ProcessedCount ?? 0,
            TotalCount = TotalCount,
        };
    }

    // Splits whatever new text has arrived into complete SSE records (separated by a blank
    // line, per the SSE spec) and parses each one's "event:"/"data:" lines independently,
    // exactly like the browser-side parser - so a field that happens to be named e.g. "id"
    // inside an unrelated event is never mistaken for run_complete's fetch_run_id. Only the
    // trailing, possibly-incomplete record is kept in the buffer for the next read.
    private static (RunCompleteInfo? RunComplete, List<FolderFailure> Failures, string? RunFailureReason) ProcessCompleteRecords(StringBuilder frameBuilder, int localFetchRunId, ILogger logger)
    {
        var text = frameBuilder.ToString();
        var records = Regex.Split(text, "\r?\n\r?\n");

        frameBuilder.Clear();
        frameBuilder.Append(records[^1]);
        var completeRecords = records[..^1];

        RunCompleteInfo? runComplete = null;
        string? runFailureReason = null;
        var failures = new List<FolderFailure>();

        foreach (var record in completeRecords)
        {
            if (string.IsNullOrWhiteSpace(record))
            {
                continue;
            }

            string? eventName = null;
            var dataLines = new List<string>();
            foreach (var rawLine in record.Split('\n'))
            {
                var line = rawLine.TrimEnd('\r');
                if (line.StartsWith("event:", StringComparison.Ordinal))
                {
                    eventName = line["event:".Length..].Trim();
                }
                else if (line.StartsWith("data:", StringComparison.Ordinal))
                {
                    dataLines.Add(line["data:".Length..].Trim());
                }
            }

            var json = string.Join('\n', dataLines);
            if (json.Length == 0)
            {
                continue;
            }

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(json);
            }
            catch (JsonException ex)
            {
                // Usually just a partial JSON object caught mid-frame (not worth retrying), but
                // could also be a genuinely malformed event from the external service - log it so
                // an unexpected/malformed SSE event is traceable instead of silently dropped.
                logger.LogWarning(ex, "Skipped an unparsable SSE record for fetch run {LocalFetchRunId}: {Record}",
                    localFetchRunId, json.Length > 300 ? json[..300] + "…" : json);
                continue;
            }

            using (doc)
            {
                var root = doc.RootElement;
                eventName ??= TryGetString(root, "event", "type", "event_type", "kind");

                if (eventName == "run_complete" && runComplete is null)
                {
                    runComplete = new RunCompleteInfo(
                        TryGetInt(root, "fetch_run_id", "run_id", "id"),
                        TryGetString(root, "status"),
                        TryGetInt(root, "processed_count", "processedCount"),
                        TryGetInt(root, "total_count", "totalCount"));
                }

                if (eventName == "connectivity_check" && runFailureReason is null && TryGetBool(root, "ok") == false)
                {
                    var message = TryGetString(root, "message");
                    if (message is null)
                    {
                        // The real service reports connectivity as booleans with no message when
                        // something's down ({ database, llm, root_path, ok }) - build one from
                        // whichever checks came back false, mirroring the browser-side fallback.
                        var problems = new[] { "database", "llm", "root_path" }
                            .Where(key => TryGetBool(root, key) == false)
                            .ToArray();
                        message = problems.Length > 0
                            ? $"Connectivity issue: {string.Join(", ", problems)} not reachable."
                            : "Connectivity issue detected.";
                    }
                    runFailureReason = message;
                }

                if (eventName == "system_error" && runFailureReason is null)
                {
                    runFailureReason = TryGetString(root, "message", "reason", "detail");
                }

                if (eventName == "folder_result")
                {
                    var status = TryGetString(root, "status") ?? string.Empty;
                    if (status.Contains("fail", StringComparison.OrdinalIgnoreCase))
                    {
                        failures.Add(new FolderFailure(
                            TryGetString(root, "folder_path", "folderPath"),
                            TryGetString(root, "rd_code", "rdCode"),
                            TryGetString(root, "rd_name", "rdName"),
                            // "reason" is the confirmed field name (per the real service's own
                            // captured event stream); the others are fallbacks in case a future
                            // version of the API renames it.
                            TryGetString(root, "reason", "failure_reason", "failureReason", "error", "message", "detail")));
                    }
                }
            }
        }

        return (runComplete, failures, runFailureReason);
    }

    private static bool? TryGetBool(JsonElement root, string key) =>
        root.TryGetProperty(key, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;

    private static string? TryGetString(JsonElement root, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!root.TryGetProperty(key, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }

            if (value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
            {
                return value.ToString();
            }
        }

        return null;
    }

    // JsonElement.TryGetInt32() throws InvalidOperationException (not just "returns false") when
    // the value's ValueKind isn't Number - e.g. the real fetch service sends "fetch_run_id": null
    // whenever a run finds nothing new to process, and that must resolve to "no id", not crash
    // the whole relay loop mid-stream.
    private static int? TryGetInt(JsonElement root, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (root.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var id))
            {
                return id;
            }
        }

        return null;
    }

    // Records a failed folder into the existing Failed Extraction table (OcrExtractionRecords +
    // RecordHistory) as it's observed live in the stream. Best-effort: a problem persisting this
    // bookkeeping must never interrupt relaying the run's progress to the browser.
    private async Task RecordFolderFailureAsync(int? localFetchRunId, FolderFailure failure, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(failure.FolderPath))
        {
            return;
        }

        // RequestNumber only exists here as an internal, unique bookkeeping key (Failed
        // Extraction never displays it) - the folder never got far enough to be assigned a real
        // one by the extraction pipeline.
        var requestNumber = $"FAILED-{DateTime.UtcNow:yyMMddHHmmssfff}-{Random.Shared.Next(1000, 9999)}";
        var reason = string.IsNullOrWhiteSpace(failure.FailureReason)
            ? "The fetch service reported this folder as failed."
            : failure.FailureReason!;

        try
        {
            await _failedExtractionService.RecordFailureAsync(
                requestNumber,
                failure.RdCode,
                failure.RdName,
                failure.FolderPath!,
                reason,
                DateTime.UtcNow,
                localFetchRunId,
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to record a folder failure into Failed Extraction for fetch run {LocalFetchRunId}, folder {FolderPath}.", localFetchRunId, failure.FolderPath);
        }
    }

    // For the Fetch History table's "View" action (opened in the shared #ajaxModal, hence the
    // modal-chrome view here): id is the *local* FetchRuns row id (the one shown/paginated in
    // the table). Looks up whatever external fetch_run_id was stashed on it and re-queries the
    // external service live; falls back to the local mirror's own data if no external id was
    // ever recorded (e.g. a row from before this integration existed) or the external call
    // turns up nothing.
    [HttpGet]
    public async Task<IActionResult> FetchRunDetails(int id, CancellationToken cancellationToken)
    {
        var local = await _rdConfigService.GetFetchRunAsync(id, cancellationToken);
        if (local is null)
        {
            return PartialView("_FetchRunDetailsModal", (FetchRunDetailDto?)null);
        }

        FetchRunDetailDto? details = null;
        if (local.Value.ExternalFetchRunId is { } externalId)
        {
            try
            {
                details = await _rdFetchApiClient.GetFetchRunDetailsAsync(externalId, cancellationToken);
            }
            catch (BusinessValidationException)
            {
                // Fall back to the local mirror's own data below rather than failing the view.
            }
        }

        details ??= new FetchRunDetailDto
        {
            Id = local.Value.Item.Id,
            Status = local.Value.Item.Status,
            ProcessedCount = local.Value.Item.ProcessedCount,
            TotalCount = local.Value.Item.TotalCount,
            RunTime = local.Value.Item.RunTime,
            ExecutedBy = local.Value.Item.ExecutedBy,
            SourcePath = local.Value.Item.SourcePath,
            StartedAt = local.Value.Item.StartedAt,
            CompletedAt = local.Value.Item.CompletedAt,
        };

        // Neither GET /fetch/{id} nor run_complete itself ever carries a reason - it only ever
        // existed in the live SSE stream at the moment the run failed, which is why it's
        // recorded locally (RdConfigService.RecordFailureReasonAsync) instead.
        details.FailureReason ??= local.Value.FailureReason;

        return PartialView("_FetchRunDetailsModal", details);
    }
}
