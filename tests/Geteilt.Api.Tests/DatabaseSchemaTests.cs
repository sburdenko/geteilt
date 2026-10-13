using Geteilt.Application.Ports;
using Geteilt.Domain.Groups;
using Geteilt.Domain.Members;
using Geteilt.Domain.Users;
using Geteilt.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Geteilt.Api.Tests;

/// <summary>
/// Indexes and constraints. These assert what the database enforces on its own — the domain already
/// has its own tests, and the point of a CHECK is to survive a hand-written UPDATE, a bad
/// migration or a bug in some other process.
/// </summary>
[Collection(SharedApiFixture.Name)]
public sealed class DatabaseSchemaTests(GeteiltApiFactory factory) : IAsyncLifetime
{
    private Guid _groupId;
    private Guid _annaId;
    private Guid _borisId;

    public async Task InitializeAsync()
    {
        await factory.ResetAsync();

        await Use(async db =>
        {
            Group group = new(Guid.CreateVersion7(), "Italien 2026", "EUR", Guid.CreateVersion7(), Now);
            Member anna = new(Guid.CreateVersion7(), group.Id, "Anna", Now, role: MemberRole.Owner);
            Member boris = new(Guid.CreateVersion7(), group.Id, "Boris", Now);

            db.Groups.Add(group);
            db.Members.AddRange(anna, boris);
            await db.SaveChangesAsync(CancellationToken.None);

            _groupId = group.Id;
            _annaId = anna.Id;
            _borisId = boris.Id;
        });
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static DateTimeOffset Now => DateTimeOffset.UtcNow;

    private async Task Use(Func<AppDbContext, Task> work)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();

        await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private async Task<PostgresException?> RawSqlShouldFail(string sql, params object[] parameters)
    {
        PostgresException? failure = null;

        await Use(async db =>
        {
            try
            {
                await db.Database.ExecuteSqlRawAsync(sql, parameters);
            }
            catch (PostgresException exception)
            {
                failure = exception;
            }
        });

        return failure;
    }

    [Fact]
    public async Task Database_RejectsAnExpenseWithANegativeAmount()
    {
        // Straight SQL on purpose: this bypasses the domain entirely, which is exactly the case a
        // CHECK constraint exists for.
        PostgresException? failure = await RawSqlShouldFail(
            """
            INSERT INTO expenses
                (id, group_id, description, paid_by_member_id, amount, currency, exchange_rate,
                 amount_in_base_currency, date, split_method, created_by_member_id, created_at)
            VALUES
                ({0}, {1}, 'Betrug', {2}, -50.00, 'EUR', 1.0, -50.00, current_date, 'Equal', {2}, now());
            """,
            Guid.CreateVersion7(), _groupId, _annaId);

        failure.Should().NotBeNull();
        failure!.ConstraintName.Should().Be("ck_expenses_amount_positive");
    }

    [Fact]
    public async Task Database_RejectsASettlementFromAMemberToThemselves()
    {
        PostgresException? failure = await RawSqlShouldFail(
            """
            INSERT INTO settlements
                (id, group_id, from_member_id, to_member_id, amount, currency,
                 amount_in_base_currency, date, method, created_at)
            VALUES ({0}, {1}, {2}, {2}, 10.00, 'EUR', 10.00, current_date, 'Cash', now());
            """,
            Guid.CreateVersion7(), _groupId, _annaId);

        failure.Should().NotBeNull();
        failure!.ConstraintName.Should().Be("ck_settlements_from_not_to");
    }

    [Fact]
    public async Task Database_RejectsANegativeShare()
    {
        Guid expenseId = Guid.CreateVersion7();

        await Use(async db => await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO expenses
                (id, group_id, description, paid_by_member_id, amount, currency, exchange_rate,
                 amount_in_base_currency, date, split_method, created_by_member_id, created_at)
            VALUES ({0}, {1}, 'Abendessen', {2}, 10.00, 'EUR', 1.0, 10.00, current_date, 'Equal', {2}, now());
            """,
            expenseId, _groupId, _annaId));

        PostgresException? failure = await RawSqlShouldFail(
            """
            INSERT INTO expense_shares (expense_id, member_id, share_amount, share_amount_in_base_currency)
            VALUES ({0}, {1}, -1.00, -1.00);
            """,
            expenseId, _borisId);

        failure.Should().NotBeNull();
        failure!.ConstraintName.Should().Be("ck_shares_non_negative");
    }

    [Fact]
    public async Task Database_RejectsTheSamePersonJoiningAGroupTwice()
    {
        Guid userId = Guid.CreateVersion7();

        await Use(async db =>
        {
            db.DomainUsers.Add(new User(userId, "clara@example.com", "Clara", Now));
            db.Members.Add(new Member(Guid.CreateVersion7(), _groupId, "Clara", Now, userId));
            await db.SaveChangesAsync(CancellationToken.None);
        });

        PostgresException? failure = await RawSqlShouldFail(
            """
            INSERT INTO members (id, group_id, user_id, display_name, role, joined_at)
            VALUES ({0}, {1}, {2}, 'Clara nochmal', 'Member', now());
            """,
            Guid.CreateVersion7(), _groupId, userId);

        failure.Should().NotBeNull();
        failure!.ConstraintName.Should().Be("ix_members_group_id_user_id");
    }

    [Fact]
    public async Task Database_AllowsManyMembersWithoutAnAccountInOneGroup()
    {
        // The uniqueness index is partial for exactly this reason: several members may have no
        // account, and a plain unique index would treat their NULLs as colliding in some databases.
        await Use(async db =>
        {
            db.Members.AddRange(
                new Member(Guid.CreateVersion7(), _groupId, "Gast 1", Now),
                new Member(Guid.CreateVersion7(), _groupId, "Gast 2", Now));

            await db.SaveChangesAsync(CancellationToken.None);
        });

        int members = 0;
        await Use(async db => members = await db.Members.CountAsync());

        members.Should().Be(4);
    }

    [Fact]
    public async Task MoneyColumns_AreNumericWithTwoDecimalPlaces()
    {
        List<(string Table, string Column, string Type)> columns = [];

        await Use(async db =>
        {
            await using Npgsql.NpgsqlCommand command =
                (Npgsql.NpgsqlCommand)db.Database.GetDbConnection().CreateCommand();

            command.CommandText =
                """
                SELECT table_name, column_name, numeric_precision || ',' || numeric_scale
                FROM information_schema.columns
                WHERE table_schema = 'public'
                  AND column_name IN ('amount', 'share_amount', 'amount_in_base_currency')
                ORDER BY table_name, column_name;
                """;

            await db.Database.OpenConnectionAsync();
            await using System.Data.Common.DbDataReader reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                columns.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
            }
        });

        columns.Should().NotBeEmpty();
        columns.Should().OnlyContain(column => column.Type == "18,2");
    }

    [Fact]
    public async Task SoftDeletedExpenses_DisappearFromOrdinaryQueries()
    {
        Guid expenseId = Guid.CreateVersion7();

        await Use(async db => await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO expenses
                (id, group_id, description, paid_by_member_id, amount, currency, exchange_rate,
                 amount_in_base_currency, date, split_method, created_by_member_id, created_at, deleted_at)
            VALUES ({0}, {1}, 'Storniert', {2}, 10.00, 'EUR', 1.0, 10.00, current_date, 'Equal', {2}, now(), now());
            """,
            expenseId, _groupId, _annaId));

        int visible = 0;
        int all = 0;

        await Use(async db =>
        {
            visible = await db.Expenses.CountAsync();
            all = await db.Expenses.IgnoreQueryFilters().CountAsync();
        });

        visible.Should().Be(0);
        all.Should().Be(1);
    }

    [Fact]
    public void GeneratedKeys_AreVersionSeven()
    {
        List<Guid> keys = [.. Enumerable.Range(0, 50).Select(_ => Guid.CreateVersion7())];

        keys.Should().OnlyContain(key => key.Version == 7);
        keys.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void GeneratedKeys_SortInTheOrderTheyWereCreated()
    {
        // The timestamp sits in the high bits, so keys made at different moments sort by moment —
        // which is what makes them land at the end of the B-tree instead of scattering.
        //
        // Only across milliseconds: inside one millisecond .NET fills the remaining bits at random,
        // so two keys made in the same instant have no defined order. Approximately monotonic is
        // all the index needs, and all UUIDv7 promises.
        DateTimeOffset start = new(2026, 8, 16, 12, 0, 0, TimeSpan.Zero);

        List<Guid> keys =
        [
            .. Enumerable.Range(0, 50).Select(offset => Guid.CreateVersion7(start.AddMilliseconds(offset)))
        ];

        List<string> asText = [.. keys.Select(key => key.ToString())];

        asText.Should().BeInAscendingOrder(StringComparer.Ordinal);
    }

    [Fact]
    public void IAppDbContext_ExposesTheDomainTablesAndNothingElse()
    {
        // Identity and refresh tokens must stay unreachable from the application layer.
        string[] properties = [.. typeof(IAppDbContext).GetProperties().Select(property => property.Name)];

        properties.Should().BeEquivalentTo(
            "Users", "Groups", "Members", "Expenses", "Settlements", "IdempotencyRecords");
    }
}
