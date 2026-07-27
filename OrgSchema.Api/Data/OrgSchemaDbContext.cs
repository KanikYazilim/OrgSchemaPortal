using Microsoft.EntityFrameworkCore;
using OrgSchema.Api.Models;

namespace OrgSchema.Api.Data;

public class OrgSchemaDbContext : DbContext
{
    public OrgSchemaDbContext(DbContextOptions<OrgSchemaDbContext> options) : base(options)
    {
    }

    public DbSet<OrgOverrideRule> Org_OverrideRules { get; set; }
    public DbSet<OrgMergeSuggestion> Org_MergeSuggestions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<OrgOverrideRule>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TargetId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.TargetType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.NewName).HasMaxLength(200);
            entity.Property(e => e.MergeTargetId).HasMaxLength(100);
        });

        modelBuilder.Entity<OrgMergeSuggestion>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SourceText).IsRequired().HasMaxLength(200);
            entity.Property(e => e.SimilarText).IsRequired().HasMaxLength(200);
        });
    }
}
