using Microsoft.EntityFrameworkCore;
using Practice2.Api.Data;

namespace Practice2.Api.Import;

public record RoleImportSummary(int Companies, int Saved);

public class RoleImporter(AppDbContext db, BrregClient brreg, ILogger<RoleImporter> logger)
{
    // Also how many Brreg calls run at the same time.
    private const int ChunkSize = 5;

    public async Task<RoleImportSummary> RunAsync(CancellationToken ct)
    {
        var allOrgnrs = await db.Companies.OrderBy(c => c.Orgnr).Select(c => c.Orgnr).ToListAsync(ct);
        int done = 0, saved = 0;

        foreach (var chunk in allOrgnrs.Chunk(ChunkSize))
        {
            // 1. Start one call per company (no await: each call starts and runs on),
            //    keyed by orgnr, then wait until all of them have answered.
            var calls = new Dictionary<string, Task<IReadOnlyList<BrregRole>>>();
            foreach (var orgnr in chunk)
                calls[orgnr] = brreg.GetRolesAsync(orgnr, ct);
            await Task.WhenAll(calls.Values);

            // 2. Map every role, as Brreg sends it: for each company, for each of its roles.
            var newRoles = new List<Role>();
            foreach (var (orgnr, call) in calls)
            {
                var brregRoles = await call;   // already finished: returns at once
                foreach (var brregRole in brregRoles)
                    newRoles.Add(FillRole(orgnr, brregRole));
            }

            // 3. Replace the chunk's roles: delete the old, insert the new, all or nothing.
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await db.Roles.Where(r => chunk.Contains(r.Orgnr)).ExecuteDeleteAsync(ct);
            db.Roles.AddRange(newRoles);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            db.ChangeTracker.Clear();

            done += chunk.Length;
            saved += newRoles.Count;
            if (done % 500 == 0 || done == allOrgnrs.Count)
                logger.LogInformation("Roles: {Done} of {Total} companies", done, allOrgnrs.Count);
        }

        return new RoleImportSummary(done, saved);
    }

    // The holder is a person, another unit, or a bostyrer (a bankruptcy estate's
    // administrator: only a name, stored as the person's name).
    private static Role FillRole(string orgnr, BrregRole brregRole)
    {
        var role = new Role
        {
            Orgnr = orgnr,
            RoleType = brregRole.Type!.Kode!,
            Resigned = brregRole.Avregistrert
        };

        if (brregRole.Enhet is not null)
        {
            role.UnitOrgnr = brregRole.Enhet.Organisasjonsnummer;
            role.UnitName = string.Join(" ", brregRole.Enhet.Navn ?? []);
        }
        else if(brregRole.Person?.Navn is not null)
        {
            var navn = brregRole.Person.Navn;
            role.PersonName = string.Join(" ",
                new[] { navn.Fornavn, navn.Mellomnavn, navn.Etternavn }.Where(p => !string.IsNullOrWhiteSpace(p)));
            role.BirthYear = brregRole.Person.Fodselsdato?.Year;
        }
        else if (brregRole.Bostyrer is not null)
        {
            role.PersonName = brregRole.Bostyrer.Navn;
        }

        return role;
    }
}
