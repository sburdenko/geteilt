using System.Net;
using System.Text;
using Geteilt.Contracts.Groups;
using Geteilt.Contracts.Members;
using Geteilt.Web.Api;
using Geteilt.Web.Expenses;

namespace Geteilt.Web.Tests;

/// <summary>
/// Pressing Save again after an answer got lost must not create a second expense. The
/// server compares the raw body bytes under one key, so these tests compare what actually went
/// over the wire, not what the draft looked like.
/// </summary>
public sealed class ExpenseSaveRepeatTests
{
    private static readonly Guid AnnaUser = Guid.Parse("00000000-0000-0000-0000-00000000aaaa");
    private static readonly DateOnly Today = new(2026, 10, 9);

    private static readonly GroupDto Trip = new(
        Guid.Parse("00000000-0000-0000-0000-0000000000f1"), "Trip", "EUR", DateTimeOffset.UnixEpoch, null, 2, 0m);

    private static readonly MemberDto Anna = Member("a1", "Anna", AnnaUser);
    private static readonly MemberDto Boris = Member("b2", "Boris", null);

    [Fact]
    public async Task SavingTheSameDraftAgain_SendsTheSameKey()
    {
        ExpenseForm form = Dinner(12.30m);

        StubHandler stub = await SaveAsync(form, form);

        stub.Seen[1].IdempotencyKey.Should().Be(stub.Seen[0].IdempotencyKey);
    }

    [Fact]
    public async Task AnEditedDraft_IsSentWithANewKey()
    {
        ExpenseForm form = Dinner(12.30m);

        StubHandler stub = await SaveAsync(form, form.With(form.Draft.WithAmount(13.30m)));

        stub.Seen[1].IdempotencyKey.Should().NotBe(stub.Seen[0].IdempotencyKey);
    }

    [Fact]
    public async Task ADraftEditedAndEditedBack_IsSentWithItsFirstKey()
    {
        // Unticking and ticking Boris again builds a new set with the same people in it. If the
        // first press was stored, the third must be recognised as that same expense.
        ExpenseForm form = Dinner(12.30m);
        ExpenseForm unticked = form.With(form.Draft.Toggle(Boris.Id));
        ExpenseForm ticked = unticked.With(unticked.Draft.Toggle(Boris.Id));

        StubHandler stub = await SaveAsync(form, unticked, ticked);

        stub.Seen[2].IdempotencyKey.Should().Be(stub.Seen[0].IdempotencyKey);
        stub.Seen[1].IdempotencyKey.Should().NotBe(stub.Seen[0].IdempotencyKey);
    }

    [Fact]
    public async Task TheSameAmountWithAnotherScale_IsADifferentBodyAndSoADifferentKey()
    {
        // "12,3" and "12,30" are equal decimals but serialise differently. Under one key the
        // server would see a reused key with another body and refuse every further press (409).
        ExpenseForm form = Dinner(12.3m);

        StubHandler stub = await SaveAsync(form, form.With(form.Draft.WithAmount(12.30m)));

        stub.Seen[1].Body.Should().NotBe(stub.Seen[0].Body);
        stub.Seen[1].IdempotencyKey.Should().NotBe(stub.Seen[0].IdempotencyKey);
    }

    [Fact]
    public async Task TheSameExpenseInASecondForm_IsANewExpense()
    {
        // Two coffees on purpose: the form was closed after the first one was saved.
        StubHandler stub = await SaveAsync(Dinner(3.50m), Dinner(3.50m));

        stub.Seen[1].Body.Should().Be(stub.Seen[0].Body);
        stub.Seen[1].IdempotencyKey.Should().NotBe(stub.Seen[0].IdempotencyKey);
    }

    [Fact]
    public async Task TheKeySent_IsTheKeyOfTheBytesSent()
    {
        ExpenseForm form = Dinner(12.30m);

        StubHandler stub = await SaveAsync(form);

        StubHandler.Recorded sent = stub.Seen.Single();
        sent.IdempotencyKey.Should().Be(form.Idempotency.KeyFor(Encoding.UTF8.GetBytes(sent.Body!)).ToString());
        sent.ContentType.Should().Be("application/json");
    }

    [Fact]
    public void TheKey_IsAVersion8Uuid()
    {
        // The API expects a UUID; RFC 9562 reserves version 8 for UUIDs built by the application.
        Guid key = IdempotencyScope.New().KeyFor("{}"u8);

        key.Version.Should().Be(8);
        (key.Variant >> 2).Should().Be(0b10, "Guid.Variant is the whole top nibble; the RFC variant is its two high bits");
    }

    [Fact]
    public void EditingTheDraft_KeepsTheFormsScope()
    {
        ExpenseForm form = Dinner(12.30m);

        form.With(form.Draft.WithDescription("Lunch")).Idempotency.Should().Be(form.Idempotency);
    }

    private static ExpenseForm Dinner(decimal amount)
    {
        ExpenseForm form = ExpenseForm.Open(Trip, [Anna, Boris], AnnaUser, Today);

        return form.With(form.Draft.WithDescription("Dinner").WithAmount(amount));
    }

    /// <summary>Every press fails as a lost answer would, so each next press is a retry.</summary>
    private static async Task<StubHandler> SaveAsync(params ExpenseForm[] presses)
    {
        StubHandler stub = new((_, _) => new HttpResponseMessage(HttpStatusCode.GatewayTimeout));
        ExpensesApi api = new(new HttpClient(stub) { BaseAddress = new Uri("https://api.test/") });

        foreach (ExpenseForm form in presses)
        {
            await api.CreateAsync(Trip.Id, form.Draft.ToRequest(form.Currency), form.Idempotency);
        }

        return stub;
    }

    private static MemberDto Member(string suffix, string name, Guid? userId) => new(
        Guid.Parse($"00000000-0000-0000-0000-0000000000{suffix}"), userId, name, null, MemberRole.Member,
        userId is not null, null);
}
