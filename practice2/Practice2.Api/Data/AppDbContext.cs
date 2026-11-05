using Microsoft.EntityFrameworkCore;

namespace Practice2.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<ImportRun> ImportRuns => Set<ImportRun>();
    public DbSet<Role> Roles => Set<Role>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Company>(company =>
        {
            company.HasKey(c => c.Orgnr);
            company.Property(c => c.Orgnr).HasMaxLength(9).IsFixedLength();
            company.Property(c => c.Name).HasMaxLength(500);
            company.Property(c => c.OrgForm).HasMaxLength(10);
        });

        modelBuilder.Entity<ImportRun>(impRun =>
        {
            impRun.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            impRun.Property(r => r.Error).HasMaxLength(2000);
        });

        modelBuilder.Entity<Role>(role =>
        {
            role.Property(r => r.Orgnr).HasMaxLength(9).IsFixedLength();
            role.Property(r => r.RoleType).HasMaxLength(10);
            role.Property(r => r.PersonName).HasMaxLength(300);
            role.Property(r => r.UnitOrgnr).HasMaxLength(9).IsFixedLength();
            role.Property(r => r.UnitName).HasMaxLength(500);

            role.HasOne(r => r.Company)
                .WithMany(c => c.Roles)
                .HasForeignKey(r => r.Orgnr)
                .OnDelete(DeleteBehavior.Cascade);

            // What identifies a role: the company, the type and the holder. NULLs count as equal,
            // so two rows with the same person and no unit are duplicates too.
            role.HasIndex(r => new { r.Orgnr, r.RoleType, r.PersonName, r.BirthYear, r.UnitOrgnr })
                .IsUnique()
                .AreNullsDistinct(false)
                .HasDatabaseName("ux_roles_identity");

            role.HasIndex(r => r.RoleType);
            role.HasIndex(r => r.BirthYear);

            role.ToTable(t => t.HasCheckConstraint(
                "ck_roles_one_holder", "(person_name IS NULL) <> (unit_orgnr IS NULL)"));
        });
    }
}