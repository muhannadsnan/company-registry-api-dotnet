using System.Net;
using System.Net.Http.Json;
using Practice2.Api.Import;

namespace Practice2.Tests.Infrastructure;

// What the fake Brreg knows. A test fills it before starting an import.
public class FakeBrreg
{
    public List<BrregEnhet> Companies { get; } = [];
    public Dictionary<string, List<BrregRole>> Roles { get; } = [];
}

// Replaces the real network: answers BrregClient's two URLs from FakeBrreg.
public class FakeBrregHandler(FakeBrreg brreg) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = request.RequestUri!.AbsolutePath;   // e.g. /enhetsregisteret/api/enheter/923609016/roller

        object body;
        if (path.EndsWith("/roller"))
        {
            var orgnr = path.Split('/')[^2];            // the second-last part
            body = new BrregRoles([new BrregRoleGroup(brreg.Roles.GetValueOrDefault(orgnr, []))]);
        }
        else
        {
            body = new BrregPage(new BrregEmbedded(brreg.Companies),
                new BrregPageInfo(500, brreg.Companies.Count, 1, 0));   // one page only
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(body) });
    }
}
