namespace DCT_SD.Models.ViewModels;

public class FieldInfoTooltipViewModel
{
    public string Title { get; set; } = string.Empty;
    public IReadOnlyList<string> Items { get; set; } = Array.Empty<string>();
}
