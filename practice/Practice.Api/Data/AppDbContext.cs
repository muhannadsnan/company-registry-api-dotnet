using Microsoft.EntityFrameworkCore;

namespace Practice.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Company> Companies => Set<Company>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Company>( company => 
        {
            company.HasKey(c => c.Orgnr);
            company.Property(c => c.Orgnr).HasMaxLength(9).IsFixedLength();
            company.Property(c => c.Name).HasMaxLength(500);
            company.Property(c => c.OrgForm).HasMaxLength(10);
        });
    }
}