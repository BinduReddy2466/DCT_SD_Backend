using DCT_SD.Models.Enums;

namespace DCT_SD.Helpers;

// Builds the composite key format CodeLookups.Code uses for LookupType = "TitleSequence",
// confirmed from live data: "{Title}|{TitleType as int}|{Plan}|{Block}|{Lot}".
// The matching Sequence value lives in CodeLookups.Name.
public static class TitleSequenceKey
{
    public static string Build(string title, TitleType titleType, string plan, string block, string lot) =>
        $"{title}|{(int)titleType}|{plan}|{block}|{lot}";
}
