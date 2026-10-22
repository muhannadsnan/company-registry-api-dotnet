using CompanyRegistry.Api.Data;
using CompanyRegistry.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CompanyRegistry.Api.Import;

/// <summary>
/// Runs one import: pages through Brreg, validates each unit, and upserts into companies.
/// Each page is committed on its own, so a failure halfway keeps the pages before it,
/// and running the import again is safe because rows are matched on organisation number.
/// </summary>
public sealed class CompanyImporter(
    AppDbContext db,
    BrregClient brreg,
    IOptions<ImportOptions> options,
    ILogger<CompanyImporter> logger)
{
    public async Task RunAsync(long importRunId, CancellationToken ct)
    {
        var run = await db.ImportRuns.FindAsync([importRunId], ct)
            ?? throw new InvalidOperationException($"Import run {importRunId} does not exist.");

        using var scope = logger.BeginScope("ImportRun {ImportRunId}, CorrelationId {CorrelationId}", run.Id, run.CorrelationId);

        run.Status = ImportStatus.Running;
        run.StartedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Import started, triggered by {Trigger}", run.Trigger);

        try
        {
            await ImportAllPagesAsync(run, ct);
            run.Status = ImportStatus.Succeeded;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            run.Status = ImportStatus.Cancelled;
            logger.LogWarning("Import cancelled because the application is stopping");
        }
        catch (Exception ex)
        {
            run.Status = ImportStatus.Failed;
            run.Error = ex.Message;
            logger.LogError(ex, "Import failed");
        }

        // Throw away any half-applied page, then record how the run ended.
        db.ChangeTracker.Clear();
        db.ImportRuns.Attach(run).State = EntityState.Modified;
        run.FinishedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(CancellationToken.None);

        logger.LogInformation(
            "Import finished with status {Status}: read {RowsRead}, inserted {RowsInserted}, updated {RowsUpdated}, unchanged {RowsUnchanged}, rejected {RowsRejected}",
            run.Status, run.RowsRead, run.RowsInserted, run.RowsUpdated, run.RowsUnchanged, run.RowsRejected);
    }

    private async Task ImportAllPagesAsync(ImportRun run, CancellationToken ct)
    {
        var o = options.Value;
        for (var page = 0; ; page++)
        {
            if ((page + 1) * o.PageSize > o.MaxRows)
            {
                logger.LogWarning("Stopped at {MaxRows} rows, the most the Brreg search API will page through", o.MaxRows);
                return;
            }

            var result = await brreg.GetPageAsync(page, ct);
            var units = result.Embedded?.Enheter ?? [];

            await UpsertPageAsync(units, run, ct);
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
            db.ImportRuns.Attach(run);

            logger.LogInformation("Imported page {Page} of {TotalPages}", page + 1, result.Page.TotalPages);

            if (page + 1 >= result.Page.TotalPages)
            {
                return;
            }
        }
    }

    private async Task UpsertPageAsync(IReadOnlyList<BrregEnhet> units, ImportRun run, CancellationToken ct)
    {
        run.RowsRead += units.Count;

        var accepted = new List<BrregEnhet>();
        foreach (var unit in units)
        {
            var reason = RejectionReason(unit);
            if (reason is null)
            {
                accepted.Add(unit);
                continue;
            }

            run.RowsRejected++;
            logger.LogWarning("Rejected {OrganisationNumber}: {Reason}", unit.Organisasjonsnummer, reason);
        }

        // One query for the whole page, not one per row.
        var numbers = accepted.Select(u => u.Organisasjonsnummer!).ToList();
        var existing = await db.Companies
            .Where(c => numbers.Contains(c.OrganisationNumber))
            .ToDictionaryAsync(c => c.OrganisationNumber, ct);

        var now = DateTimeOffset.UtcNow;
        foreach (var unit in accepted)
        {
            if (existing.TryGetValue(unit.Organisasjonsnummer!, out var company))
            {
                Apply(unit, company);

                // Change tracking compares against the values loaded from the database,
                // so a company whose data did not change is not written at all.
                if (db.Entry(company).State == EntityState.Modified)
                {
                    company.LastChangedAt = now;
                    run.RowsUpdated++;
                }
                else
                {
                    run.RowsUnchanged++;
                }
            }
            else
            {
                company = new Company { OrganisationNumber = unit.Organisasjonsnummer!, FirstImportedAt = now, LastChangedAt = now };
                Apply(unit, company);
                db.Companies.Add(company);
                existing[company.OrganisationNumber] = company;
                run.RowsInserted++;
            }
        }
    }

    private static string? RejectionReason(BrregEnhet unit) => unit switch
    {
        { Organisasjonsnummer: var n } when !OrganisationNumber.IsValid(n) => "organisation number fails MOD11 check",
        { Navn: null or "" } => "missing name",
        { Organisasjonsform.Kode: null or "" } => "missing organisation form",
        _ => null,
    };

    private static void Apply(BrregEnhet unit, Company company)
    {
        var address = unit.Forretningsadresse;
        company.Name = unit.Navn!;
        company.OrganisationFormCode = unit.Organisasjonsform!.Kode!;
        company.StreetAddress = address?.Adresse is { Count: > 0 } lines ? string.Join(", ", lines) : null;
        company.PostalCode = address?.Postnummer;
        company.PostalPlace = address?.Poststed;
        company.MunicipalityNumber = address?.Kommunenummer;
        company.Municipality = address?.Kommune;
        company.RegistrationDate = unit.RegistreringsdatoEnhetsregisteret;
    }
}
