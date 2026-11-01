using Microsoft.EntityFrameworkCore;

namespace Practice.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<ImportRun> ImportRuns => Set<ImportRun>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Company>( company => 
        {
            company.HasKey(c => c.Orgnr);
            company.Property(c => c.Orgnr).HasMaxLength(9).IsFixedLength();
            company.Property(c => c.Name).HasMaxLength(500);
            company.Property(c => c.OrgForm).HasMaxLength(10);
        });

        modelBuilder.Entity<ImportRun>( run =>
        {
            run.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<User>(user =>
        {
            user.HasIndex(u => u.Username).IsUnique();
            user.Property(u => u.Username).HasMaxLength(50);
            user.Property(u => u.Scopes).HasMaxLength(200);
        });
    }
}