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
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<FetchRun> FetchRuns => Set<FetchRun>();
    public DbSet<OcrExtractionRecord> OcrExtractionRecords => Set<OcrExtractionRecord>();
    public DbSet<ManualValidationRequest> ManualValidationRequests => Set<ManualValidationRequest>();
    public DbSet<MigrationRecord> MigrationRecords => Set<MigrationRecord>();
    public DbSet<MigrationDocument> MigrationDocuments => Set<MigrationDocument>();
    public DbSet<EmptyFolderRecord> EmptyFolderRecords => Set<EmptyFolderRecord>();
    public DbSet<CodeLookup> CodeLookups => Set<CodeLookup>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<RecordHistory> RecordHistory => Set<RecordHistory>();
    public DbSet<AuditLog> AuditLog => Set<AuditLog>();

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
