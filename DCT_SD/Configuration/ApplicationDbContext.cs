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
    public DbSet<MigrationRecordLogEntry> MigrationRecordLogEntries => Set<MigrationRecordLogEntry>();
    public DbSet<EmptyFolderRecord> EmptyFolderRecords => Set<EmptyFolderRecord>();
    public DbSet<FailedExtractionRecord> FailedExtractionRecords => Set<FailedExtractionRecord>();
    public DbSet<CodeLookup> CodeLookups => Set<CodeLookup>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<RecordHistory> RecordHistory => Set<RecordHistory>();
    public DbSet<AuditLog> AuditLog => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditInformation();
        await SetSessionContextAsync(cancellationToken);
        return await base.SaveChangesAsync(cancellationToken);
    }

    // Lets the Section 23 _Hist/_Log triggers know who/where a change came from via
    // SESSION_CONTEXT, since trigger code has no other way to see the calling user.
    // Must run on the same open connection SaveChanges then uses, so the connection
    // is opened explicitly here (EF Core won't close a connection it didn't open).
    private async Task SetSessionContextAsync(CancellationToken cancellationToken)
    {
        if (!ChangeTracker.HasChanges())
        {
            return;
        }

        // SESSION_CONTEXT/sp_set_session_context is SQL Server-only syntax - the production
        // provider is always SqlServer, so this never skips anything in production. It exists so
        // unit tests can run this same SaveChangesAsync override against EF Core's InMemory
        // provider (which has no connection to open and no SQL dialect to execute this against)
        // without needing a separate test-only DbContext subclass.
        if (!Database.IsSqlServer())
        {
            return;
        }

        var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userId = int.TryParse(userIdClaim, out var id) ? id : (int?)null;
        var ipAddr = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

        await Database.OpenConnectionAsync(cancellationToken);
        await Database.ExecuteSqlInterpolatedAsync(
            $"EXEC sp_set_session_context N'AppUserId', {userId}; EXEC sp_set_session_context N'AppClientIp', {ipAddr};",
            cancellationToken);
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
