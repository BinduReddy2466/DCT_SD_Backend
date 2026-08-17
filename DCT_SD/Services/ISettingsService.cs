using DCT_SD.Models.Dtos.Settings;
using Microsoft.AspNetCore.Http;

namespace DCT_SD.Services;

public interface ISettingsService
{
    Task<SessionSettingsDto> GetSessionSettingsAsync(CancellationToken cancellationToken = default);
    Task<SessionSettingsDto> UpdateSessionSettingsAsync(UpdateSessionSettingsRequestDto request, CancellationToken cancellationToken = default);

    Task<BrandingDto> GetBrandingAsync(CancellationToken cancellationToken = default);
    Task<BrandingDto> UpdateBrandingImageAsync(IFormFile file, CancellationToken cancellationToken = default);
    Task<BrandingDto> RemoveBrandingImageAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmailTemplateDto>> GetEmailTemplatesAsync(CancellationToken cancellationToken = default);
    Task<EmailTemplateDto> UpdateEmailTemplateAsync(string key, UpdateEmailTemplateRequestDto request, CancellationToken cancellationToken = default);
    Task<EmailTemplateDto> RestoreEmailTemplateDefaultAsync(string key, CancellationToken cancellationToken = default);
}
