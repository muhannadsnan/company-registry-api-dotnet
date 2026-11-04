using Microsoft.EntityFrameworkCore;
using Practice2.Api.Data;
using Practice2.Api.Domain;

namespace Practice2.Api.Import;

public record ImportSummary(int Read, int Inserted, int Updated, int Unchanged, int Rejected);

public class CompanyImporter(AppDbContext db, BrregClient brreg, ILogger<CompanyImporter> logger)
{
    private const int PageSize = 500;

    public async Task<ImportSummary> RunAsync(CancellationToken ct)
    {
        int read = 0, inserted = 0, updated = 0, unchanged = 0, rejected = 0;

        for (var page = 0; ; page++)
        {
            // Brreg's search API refuses to page past 10,000 rows.
            if ((page + 1) * PageSize > 10_000)
                break;

            var result = await brreg.GetPageAsync(page, PageSize, ct);
            var units = result.Embedded?.Enheter ?? [];
            read += units.Count;

            // 1. Validate: only clean rows go further.
            var validBrregList = new List<BrregEnhet>();
            foreach (var unit in units)
            {
                if (Orgnr.IsValid(unit.Organisasjonsnummer)
                    && !string.IsNullOrWhiteSpace(unit.Navn)
                    && !string.IsNullOrWhiteSpace(unit.Organisasjonsform?.Kode))
                {
                    validBrregList.Add(unit);
                }
                else
                {
                    rejected++;
                    logger.LogWarning("Rejected {Orgnr} ({Name})", unit.Organisasjonsnummer, unit.Navn);
                }
            }

            // 2. Load this page's companies that already exist, in one query.
            var numbers = validBrregList.Select(u => u.Organisasjonsnummer!).ToList();
            var existing = await db.Companies
                .Where(c => numbers.Contains(c.Orgnr))
                .ToDictionaryAsync(c => c.Orgnr, ct);

            // 3. Upsert: update the existing, insert the new.
            foreach (var validBrreg in validBrregList)
            {
                if (existing.TryGetValue(validBrreg.Organisasjonsnummer!, out var existingCompany)) // update when the new item found in db existing list
                { 
                    // if (existing.ContainsKey(orgnr)) { var company = existing[orgnr]; ... }
                    existingCompany.Name = validBrreg.Navn!;
                    existingCompany.OrgForm = validBrreg.Organisasjonsform!.Kode!;
                    existingCompany.RegistrationDate = validBrreg.RegistreringsdatoEnhetsregisteret;

                    if (db.Entry(existingCompany).State == EntityState.Modified)
                        updated++;
                    else
                        unchanged++;
                }
                else // insert when the new item not existing in db
                {
                    db.Companies.Add(new Company
                    {
                        Orgnr = validBrreg.Organisasjonsnummer!,
                        Name = validBrreg.Navn!,
                        OrgForm = validBrreg.Organisasjonsform!.Kode!,
                        RegistrationDate = validBrreg.RegistreringsdatoEnhetsregisteret
                    });
                    inserted++;
                }
            }

            // 4. Save this page in one transaction, then forget the tracked objects.
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();

            logger.LogInformation("Imported page {Page} of {TotalPages}", page + 1, result.Page.TotalPages);

            if (page + 1 >= result.Page.TotalPages)
                break;
        }

        return new ImportSummary(read, inserted, updated, unchanged, rejected);
    }
}