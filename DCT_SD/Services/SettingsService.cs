using DCT_SD.Configuration;
using DCT_SD.Helpers;
using DCT_SD.Helpers.Exceptions;
using DCT_SD.Models.Dtos.Settings;
using DCT_SD.Models.Entities;
using DCT_SD.Models.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace DCT_SD.Services;

public class SettingsService : ISettingsService
{
    private static readonly string[] AllowedImageContentTypes = { "image/jpeg", "image/png", "image/webp", "image/gif" };
    private const long MaxImageBytes = 5 * 1024 * 1024;
    private const string BrandingUploadsRelativePath = "uploads/branding";

    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _webHostEnvironment;

    public SettingsService(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
    {
        _context = context;
        _webHostEnvironment = webHostEnvironment;
    }

    public async Task<SessionSettingsDto> GetSessionSettingsAsync(CancellationToken cancellationToken = default)
    {
        var setting = await _context.SessionSettings.AsNoTracking().FirstAsync(cancellationToken);
        return new SessionSettingsDto { TimeoutMinutes = setting.TimeoutMinutes, Action = setting.Action.ToString() };
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

        var setting = await _context.SessionSettings.FirstAsync(cancellationToken);
        setting.TimeoutMinutes = request.TimeoutMinutes;
        setting.Action = action;
        await _context.SaveChangesAsync(cancellationToken);

        return new SessionSettingsDto { TimeoutMinutes = setting.TimeoutMinutes, Action = setting.Action.ToString() };
    }

    public async Task<BrandingDto> GetBrandingAsync(CancellationToken cancellationToken = default)
    {
        var setting = await _context.BrandingSettings.AsNoTracking().FirstAsync(cancellationToken);
        return new BrandingDto { ImageUrl = setting.ImagePath };
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

        var setting = await _context.BrandingSettings.FirstAsync(cancellationToken);

        var uploadsDirectory = Path.Combine(_webHostEnvironment.WebRootPath, BrandingUploadsRelativePath);
        Directory.CreateDirectory(uploadsDirectory);

        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var physicalPath = Path.Combine(uploadsDirectory, fileName);

        await using (var stream = File.Create(physicalPath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        DeletePhysicalFileIfExists(setting.ImagePath);

        setting.ImagePath = $"/{BrandingUploadsRelativePath}/{fileName}";
        await _context.SaveChangesAsync(cancellationToken);

        return new BrandingDto { ImageUrl = setting.ImagePath };
    }

    public async Task<BrandingDto> RemoveBrandingImageAsync(CancellationToken cancellationToken = default)
    {
        var setting = await _context.BrandingSettings.FirstAsync(cancellationToken);
        DeletePhysicalFileIfExists(setting.ImagePath);
        setting.ImagePath = null;
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
        var templates = await _context.EmailTemplates.AsNoTracking().OrderBy(t => t.Id).ToListAsync(cancellationToken);
        return templates.Select(MapToDto).ToArray();
    }

    public async Task<EmailTemplateDto> UpdateEmailTemplateAsync(string key, UpdateEmailTemplateRequestDto request, CancellationToken cancellationToken = default)
    {
        var template = await _context.EmailTemplates.FirstOrDefaultAsync(t => t.Key == key, cancellationToken)
            ?? throw new NotFoundException("Email template", key);

        template.Recipients = request.Recipients.Trim();
        template.Subject = request.Subject.Trim();
        template.Body = request.Body;

        await _context.SaveChangesAsync(cancellationToken);
        return MapToDto(template);
    }

    public async Task<EmailTemplateDto> RestoreEmailTemplateDefaultAsync(string key, CancellationToken cancellationToken = default)
    {
        var template = await _context.EmailTemplates.FirstOrDefaultAsync(t => t.Key == key, cancellationToken)
            ?? throw new NotFoundException("Email template", key);

        var defaults = DefaultEmailTemplates.Find(key)
            ?? throw new NotFoundException("Default email template", key);

        template.Recipients = defaults.Recipients;
        template.Subject = defaults.Subject;
        template.Body = defaults.Body;

        await _context.SaveChangesAsync(cancellationToken);
        return MapToDto(template);
    }

    private static EmailTemplateDto MapToDto(EmailTemplate t) => new()
    {
        Key = t.Key,
        Label = t.Label,
        Recipients = t.Recipients,
        Subject = t.Subject,
        Body = t.Body,
    };
}
