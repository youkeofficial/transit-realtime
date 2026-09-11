using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TransitRealtime.Web.Models;

namespace TransitRealtime.Web.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<IdentityUser, IdentityRole, string>(options)
{
    public DbSet<TransitService> TransitServices => Set<TransitService>();
    public DbSet<ApiKeyEntity> ApiKeys => Set<ApiKeyEntity>();
    public DbSet<AuditLogEntry> AuditLogs => Set<AuditLogEntry>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<TransitService>()
            .HasIndex(s => s.Name);

        builder.Entity<ApiKeyEntity>()
            .HasOne(k => k.Service)
            .WithMany(s => s.ApiKeys)
            .HasForeignKey(k => k.ServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ApiKeyEntity>()
            .HasIndex(k => k.KeyPrefix);

        builder.Entity<AuditLogEntry>()
            .HasIndex(l => l.ServiceId);
    }
}
