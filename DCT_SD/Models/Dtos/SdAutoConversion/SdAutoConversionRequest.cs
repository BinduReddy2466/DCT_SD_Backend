namespace DCT_SD.Models.Dtos.SdAutoConversion;

// Everything needed to build one POST /api/SDConversion/CreateSDConversion multipart request -
// see Services/SdAutoConversionClient.cs. RdCode/TitleSeq come straight off one MigrationRecords
// row. EntryNumber is that same row's own EntryNumbersCsv value - as of 2026-11-12 ("Migration
// Tracking should be per entry number", client-confirmed), that column holds exactly ONE entry
// number per row (MigrationRecord creation splits a source's comma-separated entry number list
// into one row per entry number - see Helpers/EntryNumberCsvParser.cs), so this is one request per
// entry number, not one combined request for the whole list as it was before that change. The
// supporting documents are the same set reused across every one of a source's split rows.
public class SdAutoConversionRequest
{
    public required string RdCode { get; set; }
    public required decimal TitleSeq { get; set; }
    public required string EntryNumber { get; set; }
    public required IReadOnlyList<SdAutoConversionDocumentRequest> SupportingDocuments { get; set; }
}

public class SdAutoConversionDocumentRequest
{
    public required string DocumentId { get; set; }

    // Absolute path on disk - read directly via System.IO.File, same as
    // ManualValidationController.DocumentImage does for the sibling ManualValidationRequest
    // table's DocumentsJson.
    public required string ImagePath { get; set; }
}
