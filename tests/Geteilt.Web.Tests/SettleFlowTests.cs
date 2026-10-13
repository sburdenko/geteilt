using System.Net;
using System.Text;
using Geteilt.Web.Api;
using Geteilt.Web.Loading;
using Geteilt.Web.Settlements;

namespace Geteilt.Web.Tests;

/// <summary>The QR screen's flows, through the real API client and a stubbed server.</summary>
public sealed class SettleFlowTests
{
    private const string Group = "0198f000-0000-7000-8000-000000000001";
    private static readonly Guid Anna = Guid.Parse("0198f000-0000-7000-8000-0000000000a1");
    private static readonly Guid Ben = Guid.Parse("0198f000-0000-7000-8000-0000000000b2");
    private static readonly Guid Cleo = Guid.Parse("0198f000-0000-7000-8000-0000000000c3");

    private static readonly string Plan = $$"""
        {"baseCurrency":"EUR","calculatedAt":"2026-10-09T12:00:00+00:00","transfers":[
          {"fromMemberId":"{{Ben}}","fromName":"Ben","toMemberId":"{{Anna}}","toName":"Anna","amount":3.33,
           "currency":"EUR","toIban":"DE89370400440532013000","canGenerateQr":true}]}
        """;

    private const string Code = """
        {"beneficiaryName":"Anna","iban":"DE89370400440532013000","amount":3.33,"currency":"EUR",
         "remittance":"Geteilt: Kaffee","pngBase64":"iVA="}
        """;

    private const string Settlement = """
        {"id":"0198f000-0000-7000-8000-0000000000f1","fromMemberId":"0198f000-0000-7000-8000-0000000000b2",
         "fromName":"Ben","toMemberId":"0198f000-0000-7000-8000-0000000000a1","toName":"Anna","amount":3.33,
         "currency":"EUR","date":"2026-10-10","method":"BankTransfer","note":null,
         "createdAt":"2026-10-09T22:30:00+00:00"}
        """;

    [Fact]
    public async Task AnOpenTransfer_ShowsTheCodesOwnFields()
    {
        (SettleFlow flow, _) = Flow(girocode: Json(HttpStatusCode.OK, Code));

        Loadable<SettleView> view = await flow.LoadAsync(Guid.Parse(Group), Ben, Anna);

        OpenTransfer open = view.Should().BeOfType<Loadable<SettleView>.Loaded>().Subject
            .Value.Should().BeOfType<SettleView.Open>().Subject.Transfer;
        open.Transfer.FromName.Should().Be("Ben");
        open.Remittance.Should().Be("Geteilt: Kaffee");
        open.IbanTail.Should().Be("3000");
    }

    [Fact]
    public async Task APairNoLongerInThePlan_IsNothingOpen_AndNoCodeIsAskedFor()
    {
        (SettleFlow flow, StubHandler server) = Flow(girocode: Json(HttpStatusCode.OK, Code));

        Loadable<SettleView> view = await flow.LoadAsync(Guid.Parse(Group), Cleo, Anna);

        view.Should().Be(new Loadable<SettleView>.Loaded(new SettleView.NothingOpen()));
        server.Seen.Should().ContainSingle("only the plan was read");
    }

    [Fact]
    public async Task ARecipientWithoutAnIban_IsAFailureWithItsCode()
    {
        (SettleFlow flow, _) = Flow(girocode: Json(
            HttpStatusCode.UnprocessableEntity, """{"status":422,"title":"No IBAN","code":"epc_no_iban"}"""));

        Loadable<SettleView> view = await flow.LoadAsync(Guid.Parse(Group), Ben, Anna);

        view.Match(() => "loading", _ => "loaded", error => error.Code).Should().Be("epc_no_iban");
    }

    [Fact]
    public async Task PressingPaidTwice_SendsTheSameKeyBothTimes()
    {
        // The first answer got lost on the way back; the server remembers the key and does not
        // record the transfer a second time.
        (SettleFlow flow, StubHandler server) = Flow(girocode: Json(HttpStatusCode.OK, Code));
        OpenTransfer open = await OpenAsync(flow);

        await flow.RecordAsync(Guid.Parse(Group), open, new DateOnly(2026, 10, 10));
        await flow.RecordAsync(Guid.Parse(Group), open, new DateOnly(2026, 10, 10));

        string?[] keys = [.. server.Seen.Skip(2).Select(request => request.IdempotencyKey)];
        keys.Should().HaveCount(2).And.OnlyContain(key => key == open.IdempotencyKey.ToString());
    }

    [Fact]
    public async Task Recording_SendsWhatTheScreenShowed_DatedTheLocalDay()
    {
        (SettleFlow flow, StubHandler server) = Flow(girocode: Json(HttpStatusCode.OK, Code));
        OpenTransfer open = await OpenAsync(flow);

        await flow.RecordAsync(Guid.Parse(Group), open, new DateOnly(2026, 10, 10));

        server.Seen[^1].Body.Should()
            .Contain($"\"fromMemberId\":\"{Ben}\"").And
            .Contain($"\"toMemberId\":\"{Anna}\"").And
            .Contain("\"amount\":3.33").And
            .Contain("\"date\":\"2026-10-10\"").And
            .Contain("\"method\":\"BankTransfer\"");
    }

    private static async Task<OpenTransfer> OpenAsync(SettleFlow flow) =>
        ((SettleView.Open)((Loadable<SettleView>.Loaded)await flow.LoadAsync(Guid.Parse(Group), Ben, Anna)).Value).Transfer;

    private static (SettleFlow Flow, StubHandler Server) Flow(HttpResponseMessage girocode)
    {
        StubHandler server = new((request, _) => request.RequestUri!.AbsolutePath switch
        {
            var path when path.EndsWith("/settlement-plan", StringComparison.Ordinal) => Json(HttpStatusCode.OK, Plan),
            var path when path.EndsWith("/girocode/preview", StringComparison.Ordinal) => girocode,
            var path when path.EndsWith("/settlements", StringComparison.Ordinal) => Json(HttpStatusCode.Created, Settlement),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        });
        HttpClient http = new(server) { BaseAddress = new Uri("https://api.test/") };

        return (new SettleFlow(new BalancesApi(http), new GirocodeApi(http), new SettlementsApi(http)), server);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status)
    {
        Content = new StringContent(
            body, Encoding.UTF8, status == HttpStatusCode.OK || status == HttpStatusCode.Created
                ? "application/json"
                : "application/problem+json"),
    };
}
