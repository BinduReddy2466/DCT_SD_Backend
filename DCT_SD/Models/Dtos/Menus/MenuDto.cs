namespace DCT_SD.Models.Dtos.Menus;

public class MenuDto
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool IsBaseMenu { get; set; }
}
