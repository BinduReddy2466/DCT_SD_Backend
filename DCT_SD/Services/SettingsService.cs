using System.Text.Json;
using DCT_SD.Configuration;
using DCT_SD.Helpers;
using DCT_SD.Helpers.Exceptions;
using DCT_SD.Models;
using DCT_SD.Models.Dtos.Settings;
using DCT_SD.Models.Entities;
using DCT_SD.Models.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace DCT_SD.Services;

// AppSettings replaces the old SessionSettings/BrandingSettings/EmailTemplates tables: one row
// per Category (Session/Branding, single row each) or per Category+Key (EmailTemplate, one row
// per template), with the actual fields carried in DataJson. Shapes below are confirmed from
// live data, not invented.
public class SettingsService : ISettingsService
{
    private static readonly string[] AllowedImageContentTypes = { "image/jpeg", "image/png", "image/webp", "image/gif" };
    private const long MaxImageBytes = 5 * 1024 * 1024;
    private const string BrandingUploadsRelativePath = "uploads/branding";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _webHostEnvironment;

    public SettingsService(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
    {
        _context = context;
        _webHostEnvironment = webHostEnvironment;
    }

    public async Task<SessionSettingsDto> GetSessionSettingsAsync(CancellationToken cancellationToken = default)
    {
        var setting = await _context.AppSettings.AsNoTracking()
            .FirstAsync(s => s.Category == AppSettingCategories.Session, cancellationToken);
        var data = JsonSerializer.Deserialize<SessionSettingsJson>(setting.DataJson, JsonOptions)!;
        return new SessionSettingsDto { TimeoutMinutes = data.TimeoutMinutes, Action = ((SessionTimeoutAction)data.Action).ToString() };
    }

    public async Task<SessionSettingsDto> UpdateSessionSettingsAsync(UpdateSessionSettingsRequestDto request, CancellationToken cancellationToken = default)
    {
        if (request.TimeoutMinutes <= 0)
        {
            throw new BusinessValidationException("Please enter a valid custom timeout value.");
        }

        if (!Enum.TryParse<SessionTimeoutAction>(request.Action, true, out var action))
        {
            throw new BusinessValidationException("Timeout action must be one of: Lock, Logout.");
        }

        var setting = await _context.AppSettings.FirstAsync(s => s.Category == AppSettingCategories.Session, cancellationToken);
        setting.DataJson = JsonSerializer.Serialize(new SessionSettingsJson(request.TimeoutMinutes, (int)action), JsonOptions);
        await _context.SaveChangesAsync(cancellationToken);

        return new SessionSettingsDto { TimeoutMinutes = request.TimeoutMinutes, Action = action.ToString() };
    }

    public async Task<BrandingDto> GetBrandingAsync(CancellationToken cancellationToken = default)
    {
        var setting = await _context.AppSettings.AsNoTracking()
            .FirstAsync(s => s.Category == AppSettingCategories.Branding, cancellationToken);
        var data = JsonSerializer.Deserialize<BrandingJson>(setting.DataJson, JsonOptions)!;
        return new BrandingDto { ImageUrl = data.ImagePath };
    }

    public async Task<BrandingDto> UpdateBrandingImageAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file.Length == 0)
        {
            throw new BusinessValidationException("Please choose an image file to upload.");
        }

        if (file.Length > MaxImageBytes)
        {
            throw new BusinessValidationException("Image must be 5 MB or smaller.");
        }

        if (!AllowedImageContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new BusinessValidationException("Please upload a JPEG, PNG, WEBP, or GIF image.");
        }

        var setting = await _context.AppSettings.FirstAsync(s => s.Category == AppSettingCategories.Branding, cancellationToken);
        var data = JsonSerializer.Deserialize<BrandingJson>(setting.DataJson, JsonOptions)!;

        var uploadsDirectory = Path.Combine(_webHostEnvironment.WebRootPath, BrandingUploadsRelativePath);
        Directory.CreateDirectory(uploadsDirectory);

        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var physicalPath = Path.Combine(uploadsDirectory, fileName);

        await using (var stream = File.Create(physicalPath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        DeletePhysicalFileIfExists(data.ImagePath);

        var newImagePath = $"/{BrandingUploadsRelativePath}/{fileName}";
        setting.DataJson = JsonSerializer.Serialize(new BrandingJson(newImagePath), JsonOptions);
        await _context.SaveChangesAsync(cancellationToken);

        return new BrandingDto { ImageUrl = newImagePath };
    }

    public async Task<BrandingDto> RemoveBrandingImageAsync(CancellationToken cancellationToken = default)
    {
        var setting = await _context.AppSettings.FirstAsync(s => s.Category == AppSettingCategories.Branding, cancellationToken);
        var data = JsonSerializer.Deserialize<BrandingJson>(setting.DataJson, JsonOptions)!;
        DeletePhysicalFileIfExists(data.ImagePath);
        setting.DataJson = JsonSerializer.Serialize(new BrandingJson(null), JsonOptions);
        await _context.SaveChangesAsync(cancellationToken);

        return new BrandingDto { ImageUrl = null };
    }

    private void DeletePhysicalFileIfExists(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return;
        }

        var physicalPath = Path.Combine(_webHostEnvironment.WebRootPath, relativePath.TrimStart('/'));
        if (File.Exists(physicalPath))
        {
            File.Delete(physicalPath);
        }
    }

    public async Task<IReadOnlyList<EmailTemplateDto>> GetEmailTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var templates = await _context.AppSettings.AsNoTracking()
            .Where(s => s.Category == AppSettingCategories.EmailTemplate)
            .OrderBy(s => s.Id)
            .ToListAsync(cancellationToken);
        return templates.Select(MapToDto).ToArray();
    }

    public async Task<EmailTemplateDto> UpdateEmailTemplateAsync(string key, UpdateEmailTemplateRequestDto request, CancellationToken cancellationToken = default)
    {
        var setting = await _context.AppSettings
            .FirstOrDefaultAsync(s => s.Category == AppSettingCategories.EmailTemplate && s.Key == key, cancellationToken)
            ?? throw new NotFoundException("Email template", key);

        var data = JsonSerializer.Deserialize<EmailTemplateJson>(setting.DataJson, JsonOptions)!;
        setting.DataJson = JsonSerializer.Serialize(data with
        {
            Recipients = request.Recipients.Trim(),
            Subject = request.Subject.Trim(),
            Body = request.Body,
        }, JsonOptions);

        await _context.SaveChangesAsync(cancellationToken);
        return MapToDto(setting);
    }

    public async Task<EmailTemplateDto> RestoreEmailTemplateDefaultAsync(string key, CancellationToken cancellationToken = default)
    {
        var setting = await _context.AppSettings
            .FirstOrDefaultAsync(s => s.Category == AppSettingCategories.EmailTemplate && s.Key == key, cancellationToken)
            ?? throw new NotFoundException("Email template", key);

        var defaults = DefaultEmailTemplates.Find(key)
            ?? throw new NotFoundException("Default email template", key);

        setting.DataJson = JsonSerializer.Serialize(new EmailTemplateJson(defaults.Recipients, defaults.Subject, defaults.Body), JsonOptions);

        await _context.SaveChangesAsync(cancellationToken);
        return MapToDto(setting);
    }

    private static EmailTemplateDto MapToDto(AppSetting s)
    {
        var data = JsonSerializer.Deserialize<EmailTemplateJson>(s.DataJson, JsonOptions)!;
        return new EmailTemplateDto
        {
            Key = s.Key ?? string.Empty,
            Label = s.Label ?? string.Empty,
            Recipients = data.Recipients,
            Subject = data.Subject,
            Body = data.Body,
        };
    }

    private record SessionSettingsJson(int TimeoutMinutes, int Action);
    private record BrandingJson(string? ImagePath);
    private record EmailTemplateJson(string Recipients, string Subject, string Body);
}
