using System.ComponentModel.DataAnnotations;

namespace DCT_SD.Models.ViewModels;

public class RootPathFormViewModel
{
    [Required(ErrorMessage = "Root Source Path is required.")]
    [Display(Name = "Root Source Path")]
    public string RootPath { get; set; } = string.Empty;

    [Required(ErrorMessage = "Remarks is required.")]
    [StringLength(500)]
    public string Remarks { get; set; } = string.Empty;
}
