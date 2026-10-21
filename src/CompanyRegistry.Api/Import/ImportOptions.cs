using System.ComponentModel.DataAnnotations;

namespace CompanyRegistry.Api.Import;

public sealed class ImportOptions
{
    public const string Section = "Import";

    [Required, Url]
    public string BaseUrl { get; set; } = "https://data.brreg.no/";

    /// <summary>Kommunenummer filter, e.g. 3901 (Horten) and 3905 (Tønsberg). Empty imports every municipality.</summary>
    public List<string> MunicipalityNumbers { get; set; } = [];

    /// <summary>Organisasjonsform filter, e.g. AS and ASA. Empty imports every form.</summary>
    public List<string> OrganisationForms { get; set; } = [];

    [Range(1, 1000)]
    public int PageSize { get; set; } = 500;

    /// <summary>The Brreg search API refuses to page past 10 000 rows; the bulk download is the way past that.</summary>
    [Range(1, 10_000)]
    public int MaxRows { get; set; } = 10_000;

    /// <summary>How often the scheduler queues an import. Null turns the schedule off.</summary>
    public TimeSpan? ScheduleInterval { get; set; }
}
