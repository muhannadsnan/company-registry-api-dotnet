using Microsoft.EntityFrameworkCore;
using Practice.Api.Data;
using Practice.Api.Domain;

namespace Practice.Api.Import;

public record ImportSummary(int Read, int Inserted, int Updated, int Unchanged, int Rejected);

public class CompanyImporter(AppDbContext db, BrregClient brreg, ILogger<CompanyImporter> logger)
{
    private const int PageSize = 500;

    public async Task<ImportSummary> RunAsync(CancellationToken ct)
    {
        int read = 0, inserted = 0, updated = 0, unchanged = 0, rejected = 0;

        for (var page = 0; ; page++)
        {
            // Brreg's search API refuses to page past 10k rows.
            if ((page + 1) * PageSize > 10_000)
                break;

            var result = await brreg.GetPageAsync(page, PageSize, ct);
            var units = result.Embedded?.Enheter ?? [];
            read += units.Count;

            // 1. Validate: only clean rows go further
            var valid = new List<BrregEnhet>();
            foreach (var unit in units)
            {
                if (Orgnr.IsValid(unit.Organisasjonsnummer)
                    && !string.IsNullOrWhiteSpace(unit.Navn)
                    && !string.IsNullOrWhiteSpace(unit.Organisasjonsform?.Kode))
                {
                    valid.Add(unit);
                }
                else
                {
                    rejected++;
                    logger.LogWarning("Rejected {Orgnr} ({Name})", unit.Organisasjonsnummer, unit.Navn);
                }
            }

            // 2. Load the companies of this page that already exist, in ONE query
            var numbers = valid.Select(u => u.Organisasjonsnummer!).ToList();
            var existing = await db.Companies
                .Where(c => numbers.Contains(c.Orgnr))
                .ToDictionaryAsync(c => c.Orgnr, ct);

            // 3. Upsert: update the existing, insert the new
            foreach (var unit in valid)
            {
                if (existing.TryGetValue(unit.Organisasjonsnummer!, out var company))
                {
                    company.Name = unit.Navn!;
                    company.OrgForm = unit.Organisasjonsform!.Kode!;
                    company.RegistrationDate = unit.RegistreringsdatoEnhetsregisteret;

                    if (db.Entry(company).State == EntityState.Modified)
                        updated++;
                    else
                        unchanged++;
                }
                else // not already existing: inserting
                {
                    db.Companies.Add(new Company
                    {
                        Orgnr = unit.Organisasjonsnummer!,
                        Name = unit.Navn!,
                        OrgForm = unit.Organisasjonsform!.Kode!,
                        RegistrationDate = unit.RegistreringsdatoEnhetsregisteret
                    });
                    inserted++;
                }
            }

            // 4. Save this page in one transaction, then forget the tracked objects
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();

            logger.LogInformation("Imported page {Page} of {TotalPages}", page + 1, result.Page.TotalPages);

            if (page + 1 >= result.Page.TotalPages)
                break;
        }

        return new ImportSummary(read, inserted, updated, unchanged, rejected);
    }
}