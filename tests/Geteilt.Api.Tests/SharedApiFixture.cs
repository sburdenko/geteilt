namespace Geteilt.Api.Tests;

/// <summary>
/// One container and one host for every integration test. Starting Postgres per test class would
/// add seconds each time for no benefit — tests clear the data instead.
/// </summary>
[CollectionDefinition(Name)]
public sealed class SharedApiFixture : ICollectionFixture<GeteiltApiFactory>
{
    public const string Name = "api";
}
