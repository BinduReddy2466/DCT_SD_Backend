namespace DCT_SD.Models.Dtos.Users;

public class UserSearchRequestDto
{
    public string? SearchTerm { get; set; }
    public int? RoleId { get; set; }
    public string? Status { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
