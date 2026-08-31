using ClosedXML.Excel;
using DCT_SD.Helpers;
using DCT_SD.Models;
using DCT_SD.Models.Dtos.EmptyFolders;
using DCT_SD.Models.Dtos.FailedExtraction;
using DCT_SD.Models.Dtos.ManualValidation;
using DCT_SD.Models.Dtos.Migrations;
using DCT_SD.Models.Dtos.RdConfig;
using DCT_SD.Models.Dtos.Reports;

namespace DCT_SD.Services;

// Each report type reuses its corresponding module's own search DTO, service method and query
// logic - nothing here re-implements filtering. The one wrinkle: those SearchAsync methods
// clamp PageSize to a max of 100 (correct for their live paginated screens), so "all matching
// records" for a report is built by paging through with that same 100-row page size rather than
// requesting one huge page.
public class ReportService : IReportService
{
    private const int PageBatchSize = 100;

    private readonly IRdConfigService _rdConfigService;
    private readonly IMigrationService _migrationService;
    private readonly IManualValidationService _manualValidationService;
    private readonly IEmptyFolderService _emptyFolderService;
    private readonly IRegistryOfficeService _registryOfficeService;
    private readonly IFailedExtractionService _failedExtractionService;

    public ReportService(
        IRdConfigService rdConfigService,
        IMigrationService migrationService,
        IManualValidationService manualValidationService,
        IEmptyFolderService emptyFolderService,
        IRegistryOfficeService registryOfficeService,
        IFailedExtractionService failedExtractionService)
    {
        _rdConfigService = rdConfigService;
        _migrationService = migrationService;
        _manualValidationService = manualValidationService;
        _emptyFolderService = emptyFolderService;
        _registryOfficeService = registryOfficeService;
        _failedExtractionService = failedExtractionService;
    }

    public async Task<IReadOnlyList<ReportFilterFieldDto>> GetFilterFieldsAsync(string reportType, CancellationToken cancellationToken = default)
    {
        // Migration Monitoring and Manual Validation's Registry of Deeds filter is a live
        // dropdown (see their own Index views), unlike the static option lists below.
        ReportFilterFieldDto? registryOfficeField = null;
        if (reportType is ReportTypes.MigrationMonitoring or ReportTypes.ManualValidation)
        {
            var offices = await _registryOfficeService.GetAllActiveAsync(cancellationToken);
            registryOfficeField = new ReportFilterFieldDto
            {
                Key = "RdCode",
                Label = "Registry of Deeds",
                Type = "select",
                Options = offices.Select(o => new ReportFilterOptionDto { Value = o.Code, Label = o.Name }).ToArray(),
            };
        }

        IReadOnlyList<ReportFilterFieldDto> fields = reportType switch
        {
            ReportTypes.RootSourcePathHistory =>
            [
                new() { Key = "DateFrom", Label = "Modified Date From", Type = "date" },
                new() { Key = "DateTo", Label = "Modified Date To", Type = "date" },
                new() { Key = "ModifiedBy", Label = "Modified By", Type = "text", Placeholder = "User ID" },
            ],
            ReportTypes.FetchHistory =>
            [
                new() { Key = "DateFrom", Label = "Fetch Date From", Type = "date" },
                new() { Key = "DateTo", Label = "Fetch Date To", Type = "date" },
                new() { Key = "ExecutedBy", Label = "Executed By", Type = "text", Placeholder = "User ID" },
            ],
            ReportTypes.MigrationMonitoring =>
            [
                registryOfficeField!,
                new() { Key = "RequestNumber", Label = "Request ID", Type = "text" },
                new() { Key = "EntryNumbersCsv", Label = "Entry Number", Type = "text" },
                new() { Key = "Title", Label = "Title Number", Type = "text" },
                new()
                {
                    Key = "MigrationStatus",
                    Label = "Migration Status",
                    Type = "select",
                    Options = new[] { "MigratedToExisting", "MigratedAsNew" }
                        .Select(s => new ReportFilterOptionDto { Value = s, Label = StatusDisplay.MigrationStatusToDisplay(s) })
                        .ToArray(),
                },
                new() { Key = "DateFrom", Label = "Migration Date From", Type = "date" },
                new() { Key = "DateTo", Label = "Migration Date To", Type = "date" },
            ],
            ReportTypes.ManualValidation =>
            [
                registryOfficeField!,
                new() { Key = "RequestNumber", Label = "Request ID", Type = "text" },
                new() { Key = "EntryNumbersCsv", Label = "Entry Number", Type = "text" },
                new() { Key = "Title", Label = "Title Number", Type = "text" },
                new()
                {
                    Key = "Status",
                    Label = "Status",
                    Type = "select",
                    Options = new[] { "IncompleteExtraction", "TargetRdNotIdentified" }
                        .Select(s => new ReportFilterOptionDto { Value = s, Label = StatusDisplay.ManualValidationStatusToDisplay(s) })
                        .ToArray(),
                },
                new() { Key = "DateFrom", Label = "Extraction Date From", Type = "date" },
                new() { Key = "DateTo", Label = "Extraction Date To", Type = "date" },
            ],
            ReportTypes.EmptyEntryFolders =>
            [
                new() { Key = "RdCode", Label = "Registry of Deeds Code", Type = "text" },
                new() { Key = "FolderName", Label = "Folder Name", Type = "text" },
                new() { Key = "DateFrom", Label = "Date From", Type = "date" },
                new() { Key = "DateTo", Label = "Date To", Type = "date" },
            ],
            ReportTypes.FailedExtraction =>
            [
                new() { Key = "Rd", Label = "RD", Type = "text" },
                new() { Key = "FolderName", Label = "Entry Folder Name", Type = "text" },
                new() { Key = "DateFrom", Label = "Extraction Date From", Type = "date" },
                new() { Key = "DateTo", Label = "Extraction Date To", Type = "date" },
            ],
            _ => [],
        };

        return fields;
    }

    public Task<ReportPreviewDto> GetPreviewAsync(string reportType, IDictionary<string, string?> filters, int pageNumber, int pageSize, CancellationToken cancellationToken = default) =>
        reportType switch
        {
            ReportTypes.RootSourcePathHistory => PreviewRootSourcePathHistoryAsync(filters, pageNumber, pageSize, cancellationToken),
            ReportTypes.FetchHistory => PreviewFetchHistoryAsync(filters, pageNumber, pageSize, cancellationToken),
            ReportTypes.MigrationMonitoring => PreviewMigrationMonitoringAsync(filters, pageNumber, pageSize, cancellationToken),
            ReportTypes.ManualValidation => PreviewManualValidationAsync(filters, pageNumber, pageSize, cancellationToken),
            ReportTypes.EmptyEntryFolders => PreviewEmptyEntryFoldersAsync(filters, pageNumber, pageSize, cancellationToken),
            ReportTypes.FailedExtraction => PreviewFailedExtractionAsync(filters, pageNumber, pageSize, cancellationToken),
            _ => Task.FromResult(new ReportPreviewDto()),
        };

    private static readonly string[] RootSourcePathHistoryHeaders = ["Modified Date & Time", "From Source Path", "To Source Path", "Modified By", "Remarks"];

    private async Task<ReportPreviewDto> PreviewRootSourcePathHistoryAsync(IDictionary<string, string?> filters, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var request = new RootPathHistorySearchRequestDto
        {
            DateFrom = ParseDate(filters, "DateFrom"),
            DateTo = ParseDate(filters, "DateTo"),
            ModifiedBy = GetString(filters, "ModifiedBy"),
            PageNumber = pageNumber,
            PageSize = pageSize,
        };

        var page = await _rdConfigService.SearchRootPathHistoryAsync(request, cancellationToken);
        return ToPreview(RootSourcePathHistoryHeaders, page, item => new[]
        {
            FormatDate(item.ModifiedAt),
            item.FromPath ?? string.Empty,
            item.ToPath,
            item.ModifiedBy,
            item.Remarks,
        });
    }

    private static readonly string[] FetchHistoryHeaders = ["Fetch Date & Time", "Completion Date & Time", "Run Time", "Progress", "Status", "Executed By", "Source Path"];

    private async Task<ReportPreviewDto> PreviewFetchHistoryAsync(IDictionary<string, string?> filters, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var request = new FetchHistorySearchRequestDto
        {
            DateFrom = ParseDate(filters, "DateFrom"),
            DateTo = ParseDate(filters, "DateTo"),
            ExecutedBy = GetString(filters, "ExecutedBy"),
            PageNumber = pageNumber,
            PageSize = pageSize,
        };

        var page = await _rdConfigService.SearchFetchHistoryAsync(request, cancellationToken);
        return ToPreview(FetchHistoryHeaders, page, item => new[]
        {
            FormatDate(item.StartedAt),
            FormatDate(item.CompletedAt),
            item.RunTime ?? string.Empty,
            item.TotalCount.HasValue ? $"{item.ProcessedCount}/{item.TotalCount}" : item.ProcessedCount.ToString(),
            item.Status,
            item.ExecutedBy,
            item.SourcePath,
        });
    }

    private static readonly string[] MigrationMonitoringHeaders = ["Request ID", "Migration Date", "RD", "Entry No.", "Title No.", "Title Type", "Migration Status", "SD Status", "Migrated To"];

    private async Task<ReportPreviewDto> PreviewMigrationMonitoringAsync(IDictionary<string, string?> filters, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var request = new MigrationSearchRequestDto
        {
            RdCode = GetString(filters, "RdCode"),
            RequestNumber = GetString(filters, "RequestNumber"),
            EntryNumbersCsv = GetString(filters, "EntryNumbersCsv"),
            Title = GetString(filters, "Title"),
            MigrationStatus = GetString(filters, "MigrationStatus"),
            DateFrom = ParseDate(filters, "DateFrom"),
            DateTo = ParseDate(filters, "DateTo"),
            PageNumber = pageNumber,
            PageSize = pageSize,
        };

        var page = await _migrationService.SearchAsync(request, cancellationToken);
        return ToPreview(MigrationMonitoringHeaders, page, item => new[]
        {
            item.RequestNumber,
            FormatDate(item.MigrationDate),
            item.RdName,
            item.EntryNumbersCsv ?? string.Empty,
            item.Title ?? string.Empty,
            item.TitleType ?? string.Empty,
            StatusDisplay.MigrationStatusToDisplay(item.MigrationStatus),
            StatusDisplay.SdStatusToDisplay(item.SdStatus),
            item.MigratedTo,
        });
    }

    private static readonly string[] ManualValidationHeaders = ["Request ID", "RD Code", "RD Name", "Entry No.", "Title No.", "Title Type", "Status", "Missing Fields", "Extraction Date", "Updated By", "Updated Date"];

    private async Task<ReportPreviewDto> PreviewManualValidationAsync(IDictionary<string, string?> filters, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var request = new ManualValidationSearchRequestDto
        {
            RdCode = GetString(filters, "RdCode"),
            RequestNumber = GetString(filters, "RequestNumber"),
            EntryNumbersCsv = GetString(filters, "EntryNumbersCsv"),
            Title = GetString(filters, "Title"),
            Status = GetString(filters, "Status"),
            DateFrom = ParseDate(filters, "DateFrom"),
            DateTo = ParseDate(filters, "DateTo"),
            PageNumber = pageNumber,
            PageSize = pageSize,
        };

        var page = await _manualValidationService.SearchAsync(request, cancellationToken);
        return ToPreview(ManualValidationHeaders, page, item => new[]
        {
            item.RequestNumber,
            item.RdCode ?? string.Empty,
            item.RdName ?? string.Empty,
            item.EntryNumbersCsv ?? string.Empty,
            item.Title ?? string.Empty,
            item.TitleType ?? string.Empty,
            StatusDisplay.ManualValidationStatusToDisplay(item.Status),
            StatusDisplay.DescribeMissingFields(item.MissingFields),
            FormatDate(item.ExtractionDate),
            item.UpdatedBy ?? string.Empty,
            FormatDate(item.UpdatedDate),
        });
    }

    private static readonly string[] EmptyEntryFoldersHeaders = ["Fetch Date/Time", "RD Code", "RD Name", "Folder Name", "Folder Path", "Status"];

    private async Task<ReportPreviewDto> PreviewEmptyEntryFoldersAsync(IDictionary<string, string?> filters, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var request = new EmptyFolderSearchRequestDto
        {
            RdCode = GetString(filters, "RdCode"),
            FolderName = GetString(filters, "FolderName"),
            DateFrom = ParseDate(filters, "DateFrom"),
            DateTo = ParseDate(filters, "DateTo"),
            PageNumber = pageNumber,
            PageSize = pageSize,
        };

        var page = await _emptyFolderService.SearchAsync(request, cancellationToken);
        return ToPreview(EmptyEntryFoldersHeaders, page, item => new[]
        {
            FormatDate(item.FetchDateTime),
            item.RdCode ?? string.Empty,
            item.RdName ?? string.Empty,
            item.FolderName,
            item.FolderPath,
            item.Status,
        });
    }

    private static readonly string[] FailedExtractionHeaders = ["Extraction Date and Time", "RD", "Entry Folder Name", "Folder Path", "Failure Reason"];

    private async Task<ReportPreviewDto> PreviewFailedExtractionAsync(IDictionary<string, string?> filters, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var request = new FailedExtractionSearchRequestDto
        {
            Rd = GetString(filters, "Rd"),
            FolderName = GetString(filters, "FolderName"),
            DateFrom = ParseDate(filters, "DateFrom"),
            DateTo = ParseDate(filters, "DateTo"),
            PageNumber = pageNumber,
            PageSize = pageSize,
        };

        var page = await _failedExtractionService.SearchAsync(request, cancellationToken);
        return ToPreview(FailedExtractionHeaders, page, item => new[]
        {
            FormatDate(item.ExtractionDateTime),
            item.RdName ?? string.Empty,
            item.FolderName,
            item.FolderPath,
            item.FailureReason,
        });
    }

    private static ReportPreviewDto ToPreview<TItem>(IReadOnlyList<string> headers, PagedResult<TItem> page, Func<TItem, string[]> toRow) => new()
    {
        Headers = headers,
        Rows = page.Items.Select(item => (IReadOnlyList<string>)toRow(item)).ToArray(),
        TotalCount = page.TotalCount,
        PageNumber = page.PageNumber,
        PageSize = page.PageSize,
    };

    private static string FormatDate(DateTime value) => value.ToString("MM/dd/yyyy h:mm tt");
    private static string FormatDate(DateTime? value) => value.HasValue ? FormatDate(value.Value) : string.Empty;

    public async Task<ReportGenerationResult> GenerateAsync(string reportType, IDictionary<string, string?> filters, CancellationToken cancellationToken = default)
    {
        var label = ReportTypes.Labels.GetValueOrDefault(reportType, reportType);

        using var workbook = new XLWorkbook();
        var hasRecords = reportType switch
        {
            ReportTypes.RootSourcePathHistory => await WriteRootSourcePathHistoryAsync(workbook, filters, cancellationToken),
            ReportTypes.FetchHistory => await WriteFetchHistoryAsync(workbook, filters, cancellationToken),
            ReportTypes.MigrationMonitoring => await WriteMigrationMonitoringAsync(workbook, filters, cancellationToken),
            ReportTypes.ManualValidation => await WriteManualValidationAsync(workbook, filters, cancellationToken),
            ReportTypes.EmptyEntryFolders => await WriteEmptyEntryFoldersAsync(workbook, filters, cancellationToken),
            ReportTypes.FailedExtraction => await WriteFailedExtractionAsync(workbook, filters, cancellationToken),
            _ => false,
        };

        if (!hasRecords)
        {
            return new ReportGenerationResult { HasRecords = false };
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return new ReportGenerationResult
        {
            HasRecords = true,
            FileBytes = stream.ToArray(),
            FileName = $"{label}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
        };
    }

    private async Task<bool> WriteRootSourcePathHistoryAsync(XLWorkbook workbook, IDictionary<string, string?> filters, CancellationToken cancellationToken)
    {
        var request = new RootPathHistorySearchRequestDto
        {
            DateFrom = ParseDate(filters, "DateFrom"),
            DateTo = ParseDate(filters, "DateTo"),
            ModifiedBy = GetString(filters, "ModifiedBy"),
        };

        var items = await FetchAllAsync(request, _rdConfigService.SearchRootPathHistoryAsync, cancellationToken);
        if (items.Count == 0) return false;

        var sheet = workbook.Worksheets.Add("Report");
        WriteHeader(sheet, "Modified Date & Time", "From Source Path", "To Source Path", "Modified By", "Remarks");
        var row = 2;
        foreach (var item in items)
        {
            sheet.Cell(row, 1).Value = item.ModifiedAt;
            sheet.Cell(row, 2).Value = item.FromPath ?? string.Empty;
            sheet.Cell(row, 3).Value = item.ToPath;
            sheet.Cell(row, 4).Value = item.ModifiedBy;
            sheet.Cell(row, 5).Value = item.Remarks;
            row++;
        }

        sheet.Columns().AdjustToContents();
        return true;
    }

    private async Task<bool> WriteFetchHistoryAsync(XLWorkbook workbook, IDictionary<string, string?> filters, CancellationToken cancellationToken)
    {
        var request = new FetchHistorySearchRequestDto
        {
            DateFrom = ParseDate(filters, "DateFrom"),
            DateTo = ParseDate(filters, "DateTo"),
            ExecutedBy = GetString(filters, "ExecutedBy"),
        };

        var items = await FetchAllAsync(request, _rdConfigService.SearchFetchHistoryAsync, cancellationToken);
        if (items.Count == 0) return false;

        var sheet = workbook.Worksheets.Add("Report");
        WriteHeader(sheet, "Fetch Date & Time", "Completion Date & Time", "Run Time", "Progress", "Status", "Executed By", "Source Path");
        var row = 2;
        foreach (var item in items)
        {
            sheet.Cell(row, 1).Value = item.StartedAt;
            if (item.CompletedAt.HasValue) sheet.Cell(row, 2).Value = item.CompletedAt.Value;
            sheet.Cell(row, 3).Value = item.RunTime ?? string.Empty;
            sheet.Cell(row, 4).Value = item.TotalCount.HasValue ? $"{item.ProcessedCount}/{item.TotalCount}" : item.ProcessedCount.ToString();
            sheet.Cell(row, 5).Value = item.Status;
            sheet.Cell(row, 6).Value = item.ExecutedBy;
            sheet.Cell(row, 7).Value = item.SourcePath;
            row++;
        }

        sheet.Columns().AdjustToContents();
        return true;
    }

    private async Task<bool> WriteMigrationMonitoringAsync(XLWorkbook workbook, IDictionary<string, string?> filters, CancellationToken cancellationToken)
    {
        var request = new MigrationSearchRequestDto
        {
            RdCode = GetString(filters, "RdCode"),
            RequestNumber = GetString(filters, "RequestNumber"),
            EntryNumbersCsv = GetString(filters, "EntryNumbersCsv"),
            Title = GetString(filters, "Title"),
            MigrationStatus = GetString(filters, "MigrationStatus"),
            DateFrom = ParseDate(filters, "DateFrom"),
            DateTo = ParseDate(filters, "DateTo"),
        };

        var items = await FetchAllAsync(request, _migrationService.SearchAsync, cancellationToken);
        if (items.Count == 0) return false;

        var sheet = workbook.Worksheets.Add("Report");
        WriteHeader(sheet, "Request ID", "Migration Date", "RD", "Entry No.", "Title No.", "Title Type", "Migration Status", "SD Status", "Migrated To");
        var row = 2;
        foreach (var item in items)
        {
            sheet.Cell(row, 1).Value = item.RequestNumber;
            sheet.Cell(row, 2).Value = item.MigrationDate;
            sheet.Cell(row, 3).Value = item.RdName;
            sheet.Cell(row, 4).Value = item.EntryNumbersCsv ?? string.Empty;
            sheet.Cell(row, 5).Value = item.Title ?? string.Empty;
            sheet.Cell(row, 6).Value = item.TitleType ?? string.Empty;
            sheet.Cell(row, 7).Value = StatusDisplay.MigrationStatusToDisplay(item.MigrationStatus);
            sheet.Cell(row, 8).Value = StatusDisplay.SdStatusToDisplay(item.SdStatus);
            sheet.Cell(row, 9).Value = item.MigratedTo;
            row++;
        }

        sheet.Columns().AdjustToContents();
        return true;
    }

    private async Task<bool> WriteManualValidationAsync(XLWorkbook workbook, IDictionary<string, string?> filters, CancellationToken cancellationToken)
    {
        var request = new ManualValidationSearchRequestDto
        {
            RdCode = GetString(filters, "RdCode"),
            RequestNumber = GetString(filters, "RequestNumber"),
            EntryNumbersCsv = GetString(filters, "EntryNumbersCsv"),
            Title = GetString(filters, "Title"),
            Status = GetString(filters, "Status"),
            DateFrom = ParseDate(filters, "DateFrom"),
            DateTo = ParseDate(filters, "DateTo"),
        };

        var items = await FetchAllAsync(request, _manualValidationService.SearchAsync, cancellationToken);
        if (items.Count == 0) return false;

        var sheet = workbook.Worksheets.Add("Report");
        WriteHeader(sheet, "Request ID", "RD Code", "RD Name", "Entry No.", "Title No.", "Title Type", "Status", "Missing Fields", "Extraction Date", "Updated By", "Updated Date");
        var row = 2;
        foreach (var item in items)
        {
            sheet.Cell(row, 1).Value = item.RequestNumber;
            sheet.Cell(row, 2).Value = item.RdCode ?? string.Empty;
            sheet.Cell(row, 3).Value = item.RdName ?? string.Empty;
            sheet.Cell(row, 4).Value = item.EntryNumbersCsv ?? string.Empty;
            sheet.Cell(row, 5).Value = item.Title ?? string.Empty;
            sheet.Cell(row, 6).Value = item.TitleType ?? string.Empty;
            sheet.Cell(row, 7).Value = StatusDisplay.ManualValidationStatusToDisplay(item.Status);
            sheet.Cell(row, 8).Value = StatusDisplay.DescribeMissingFields(item.MissingFields);
            sheet.Cell(row, 9).Value = item.ExtractionDate;
            sheet.Cell(row, 10).Value = item.UpdatedBy ?? string.Empty;
            if (item.UpdatedDate.HasValue) sheet.Cell(row, 11).Value = item.UpdatedDate.Value;
            row++;
        }

        sheet.Columns().AdjustToContents();
        return true;
    }

    private async Task<bool> WriteEmptyEntryFoldersAsync(XLWorkbook workbook, IDictionary<string, string?> filters, CancellationToken cancellationToken)
    {
        var request = new EmptyFolderSearchRequestDto
        {
            RdCode = GetString(filters, "RdCode"),
            FolderName = GetString(filters, "FolderName"),
            DateFrom = ParseDate(filters, "DateFrom"),
            DateTo = ParseDate(filters, "DateTo"),
        };

        var items = await FetchAllAsync(request, _emptyFolderService.SearchAsync, cancellationToken);
        if (items.Count == 0) return false;

        var sheet = workbook.Worksheets.Add("Report");
        WriteHeader(sheet, "Fetch Date/Time", "RD Code", "RD Name", "Folder Name", "Folder Path", "Status");
        var row = 2;
        foreach (var item in items)
        {
            sheet.Cell(row, 1).Value = item.FetchDateTime;
            sheet.Cell(row, 2).Value = item.RdCode ?? string.Empty;
            sheet.Cell(row, 3).Value = item.RdName ?? string.Empty;
            sheet.Cell(row, 4).Value = item.FolderName;
            sheet.Cell(row, 5).Value = item.FolderPath;
            sheet.Cell(row, 6).Value = item.Status;
            row++;
        }

        sheet.Columns().AdjustToContents();
        return true;
    }

    private async Task<bool> WriteFailedExtractionAsync(XLWorkbook workbook, IDictionary<string, string?> filters, CancellationToken cancellationToken)
    {
        var request = new FailedExtractionSearchRequestDto
        {
            Rd = GetString(filters, "Rd"),
            FolderName = GetString(filters, "FolderName"),
            DateFrom = ParseDate(filters, "DateFrom"),
            DateTo = ParseDate(filters, "DateTo"),
        };

        var items = await FetchAllAsync(request, _failedExtractionService.SearchAsync, cancellationToken);
        if (items.Count == 0) return false;

        var sheet = workbook.Worksheets.Add("Report");
        WriteHeader(sheet, "Extraction Date and Time", "RD", "Entry Folder Name", "Folder Path", "Failure Reason");
        var row = 2;
        foreach (var item in items)
        {
            sheet.Cell(row, 1).Value = item.ExtractionDateTime;
            sheet.Cell(row, 2).Value = item.RdName ?? string.Empty;
            sheet.Cell(row, 3).Value = item.FolderName;
            sheet.Cell(row, 4).Value = item.FolderPath;
            sheet.Cell(row, 5).Value = item.FailureReason;
            row++;
        }

        sheet.Columns().AdjustToContents();
        return true;
    }

    private static void WriteHeader(IXLWorksheet sheet, params string[] headers)
    {
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
        }
    }

    // Pages through a module's own SearchAsync with its native page-size cap (see PageBatchSize)
    // until every matching row has been collected, instead of touching that cap just for reports.
    private static async Task<List<TItem>> FetchAllAsync<TRequest, TItem>(
        TRequest request,
        Func<TRequest, CancellationToken, Task<PagedResult<TItem>>> search,
        CancellationToken cancellationToken)
        where TRequest : IPageableRequest
    {
        var all = new List<TItem>();
        request.PageNumber = 1;
        request.PageSize = PageBatchSize;

        while (true)
        {
            var page = await search(request, cancellationToken);
            all.AddRange(page.Items);
            if (all.Count >= page.TotalCount || page.Items.Count == 0)
            {
                break;
            }

            request.PageNumber++;
        }

        return all;
    }

    private static DateTime? ParseDate(IDictionary<string, string?> filters, string key) =>
        filters.TryGetValue(key, out var value) && DateTime.TryParse(value, out var parsed) ? parsed : null;

    private static string? GetString(IDictionary<string, string?> filters, string key) =>
        filters.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
}
