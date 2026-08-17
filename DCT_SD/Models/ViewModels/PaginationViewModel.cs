namespace DCT_SD.Models.ViewModels;

public class PaginationViewModel
{
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public string ActionName { get; set; } = "Results";
}
