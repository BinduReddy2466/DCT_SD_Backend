using Microsoft.AspNetCore.Razor.TagHelpers;

namespace DCT_SD.Helpers;

// Ports the React StatusBadge/STATUS_VARIANTS lookup verbatim - <status-badge status="@x">
// renders <span class="badge badge-{variant}">{x}</span> with the same status->color map
// shared across Fetch History, Migration Monitoring, Manual Validation, Empty Folders and
// User Management.
[HtmlTargetElement("status-badge")]
public class StatusBadgeTagHelper : TagHelper
{
    private static readonly Dictionary<string, string> Variants = new()
    {
        ["Ongoing Fetching"] = "info",
        ["Completed"] = "ok",
        ["Failed"] = "err",
        ["Migrated to Existing Title/Entry Record"] = "ok",
        ["Migrated as New Record"] = "info",
        ["All Supporting Documents Migrated"] = "ok",
        ["Partially Duplicate SD"] = "warn",
        ["All Supporting Documents are Duplicate SD"] = "err",
        ["Incomplete Extraction"] = "warn",
        ["Target RD Not Identified"] = "err",
        ["Fully Extracted"] = "ok",
        ["Partially Extracted"] = "warn",
        ["Empty Entry Folder"] = "neutral",
        ["Migrated"] = "ok",
        ["Duplicate SD"] = "err",
        ["Overwritten"] = "info",
        ["Inserted as New"] = "info",
        ["Saved"] = "info",
        ["Closed"] = "neutral",
        ["Active"] = "ok",
        ["Locked"] = "warn",
        ["Deactivated"] = "err",
    };

    public string Status { get; set; } = string.Empty;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var variant = Variants.GetValueOrDefault(Status, "neutral");
        output.TagName = "span";
        output.Attributes.SetAttribute("class", $"badge badge-{variant}");
        output.Content.SetContent(Status);
    }
}
