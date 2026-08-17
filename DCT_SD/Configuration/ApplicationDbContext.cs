using System.Reflection;
using System.Security.Claims;
using DCT_SD.Models.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace DCT_SD.Configuration;

public class ApplicationDbContext : DbContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IHttpContextAccessor httpContextAccessor)
        : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Menu> Menus => Set<Menu>();
    public DbSet<UserMenuPermission> UserMenuPermissions => Set<UserMenuPermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<RegistryOffice> RegistryOffices => Set<RegistryOffice>();
    public DbSet<RootPathHistory> RootPathHistories => Set<RootPathHistory>();
    public DbSet<FetchRun> FetchRuns => Set<FetchRun>();
    public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
    public DbSet<OcrExtractionRecord> OcrExtractionRecords => Set<OcrExtractionRecord>();
    public DbSet<OcrExtractionEntry> OcrExtractionEntries => Set<OcrExtractionEntry>();
    public DbSet<ManualValidationRequest> ManualValidationRequests => Set<ManualValidationRequest>();
    public DbSet<ManualValidationDocument> ManualValidationDocuments => Set<ManualValidationDocument>();
    public DbSet<ManualValidationRemark> ManualValidationRemarks => Set<ManualValidationRemark>();
    public DbSet<TitleSequenceLookup> TitleSequenceLookups => Set<TitleSequenceLookup>();
    public DbSet<MigrationRecord> MigrationRecords => Set<MigrationRecord>();
    public DbSet<MigrationDocument> MigrationDocuments => Set<MigrationDocument>();
    public DbSet<EmptyFolderRecord> EmptyFolderRecords => Set<EmptyFolderRecord>();
    public DbSet<SessionSetting> SessionSettings => Set<SessionSetting>();
    public DbSet<BrandingSetting> BrandingSettings => Set<BrandingSetting>();
    public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditInformation();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyAuditInformation()
    {
        var now = DateTime.UtcNow;
        var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userId = int.TryParse(userIdClaim, out var id) ? id : (int?)null;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.CreatedBy = userId;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
                entry.Entity.UpdatedBy = userId;
            }
        }
    }
}
