using Microsoft.EntityFrameworkCore;

namespace CompanyRegistry.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<ImportRun> ImportRuns => Set<ImportRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Company>(company =>
        {
            company.HasKey(c => c.OrganisationNumber);
            company.Property(c => c.OrganisationNumber).HasMaxLength(9).IsFixedLength();
            company.Property(c => c.Name).HasMaxLength(500);
            company.Property(c => c.OrganisationFormCode).HasMaxLength(10);
            company.Property(c => c.OrganisationFormDescription).HasMaxLength(200);
            company.Property(c => c.StreetAddress).HasMaxLength(500);
            company.Property(c => c.PostalCode).HasMaxLength(10);
            company.Property(c => c.PostalPlace).HasMaxLength(100);
            company.Property(c => c.MunicipalityNumber).HasMaxLength(4);
            company.Property(c => c.Municipality).HasMaxLength(100);

            company.HasIndex(c => c.OrganisationFormCode);
            company.HasIndex(c => c.MunicipalityNumber);
            company.HasIndex(c => c.PostalCode);
        });

        modelBuilder.Entity<ImportRun>(run =>
        {
            run.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            run.Property(r => r.Trigger).HasMaxLength(20);
            run.Property(r => r.RequestedBy).HasMaxLength(100);
            run.Property(r => r.CorrelationId).HasMaxLength(64);
            run.Ignore(r => r.IsActive);
            run.HasIndex(r => r.QueuedAt);
        });
    }
}
