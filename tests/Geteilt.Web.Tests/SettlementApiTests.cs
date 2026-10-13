using System.Net;
using System.Text;
using Geteilt.Contracts.Settlements;
using Geteilt.Web.Api;

namespace Geteilt.Web.Tests;

/// <summary>Recording a transfer.</summary>
public sealed class SettlementApiTests
{
    private static readonly Guid Group = Guid.Parse("0198f000-0000-7000-8000-000000000001");
    private static readonly Guid Anna = Guid.Parse("0198f000-0000-7000-8000-0000000000a1");
    private static readonly Guid Ben = Guid.Parse("0198f000-0000-7000-8000-0000000000b2");

    [Fact]
    public async Task RecordingATransfer_PostsWithTheKeyItWasGiven()
    {
        StubHandler stub = new((_, _) => new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new StringContent(
                $$"""
                {"id":"0198f000-0000-7000-8000-0000000000f1","fromMemberId":"{{Ben}}","fromName":"Ben",
                 "toMemberId":"{{Anna}}","toName":"Anna","amount":3.33,"currency":"EUR","date":"2026-10-09",
                 "method":"BankTransfer","note":null,"createdAt":"2026-10-09T12:00:00+00:00"}
                """,
                Encoding.UTF8,
                "application/json"),
        });

        Guid key = Guid.CreateVersion7();

        Result<SettlementDto> result = await new SettlementsApi(Client(stub)).CreateAsync(
            Group,
            new CreateSettlementRequest(Ben, Anna, 3.33m, "EUR", new DateOnly(2026, 10, 9), SettlementMethod.BankTransfer, null),
            key);

        result.Match(settlement => settlement.Amount, _ => 0m).Should().Be(3.33m);
        stub.Seen.Single().IdempotencyKey.Should().Be(key.ToString());
    }

    private static HttpClient Client(StubHandler stub) => new(stub) { BaseAddress = new Uri("https://api.test/") };
}
