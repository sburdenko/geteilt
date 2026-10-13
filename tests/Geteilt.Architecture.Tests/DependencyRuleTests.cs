using System.Reflection;

namespace Geteilt.Architecture.Tests;

/// <summary>
/// The dependency rule between the projects (see the README). These tests are the reason the rule
/// survives contact with a deadline: a violation fails the normal build, not a review checklist.
/// </summary>
public sealed class DependencyRuleTests
{
    [Theory]
    [InlineData("Geteilt.Domain")]
    [InlineData("Geteilt.Contracts")]
    public void InnerProject_ReferencesNothingAtAll(string projectName)
    {
        SolutionLayout.PackageReferences(projectName).Should().BeEmpty(
            "{0} must stay dependency-free — anything it needs from outside is declared as an interface " +
            "and implemented in Infrastructure",
            projectName);

        SolutionLayout.ProjectReferences(projectName).Should().BeEmpty();
    }

    [Fact]
    public void Application_KnowsDomainAndContracts_ButNotInfrastructureOrApi()
    {
        SolutionLayout.ProjectReferences("Geteilt.Application")
            .Should().BeEquivalentTo("Geteilt.Domain", "Geteilt.Contracts");
    }

    [Fact]
    public void Application_DoesNotReferenceEntityFrameworkPackagesDirectly()
    {
        // Application talks to the database through IAppDbContext, which lives in Application
        // and exposes DbSet<T>. The EF Core packages themselves stay in Infrastructure.
        SolutionLayout.PackageReferences("Geteilt.Application")
            .Should().NotContain(package => package.StartsWith("Npgsql", StringComparison.Ordinal));
    }

    [Fact]
    public void Web_KnowsContractsAndTheDomain_ButNoWayToTheData()
    {
        // The browser client goes through the same REST API as every other client.
        // Application or Infrastructure would mean a second path to the data, and the API would
        // stop being the thing that is actually proven to work.
        //
        // Domain is not that path. It references nothing, holds no data access, and its point is
        // to be compiled into a client and run there. Splitting a bill is the
        // first place that pays off: the form previews each person's share as the total is typed,
        // and total/n would show three times 3,33 for ten euro — contradicting the "Rest 0,00 ✓"
        // the same screen promises.
        SolutionLayout.ProjectReferences("Geteilt.Web")
            .Should().BeEquivalentTo("Geteilt.Contracts", "Geteilt.Domain");
    }

    [Fact]
    public void Infrastructure_KnowsDomainAndApplication_ButNotApi()
    {
        SolutionLayout.ProjectReferences("Geteilt.Infrastructure")
            .Should().BeEquivalentTo("Geteilt.Domain", "Geteilt.Application");
    }

    [Theory]
    [InlineData("Geteilt.Domain")]
    [InlineData("Geteilt.Contracts")]
    [InlineData("Geteilt.Application")]
    [InlineData("Geteilt.Web")]
    [InlineData("Geteilt.Infrastructure")]
    public void NoProjectReferencesTheApi(string projectName)
    {
        SolutionLayout.ProjectReferences(projectName)
            .Should().NotContain("Geteilt.Api", "the Api is a composition root, nothing depends on it");
    }

    [Theory]
    [InlineData("Geteilt.Domain")]
    [InlineData("Geteilt.Contracts")]
    public void InnerAssembly_LoadsNothingBeyondTheBaseClassLibrary(string projectName)
    {
        // Loaded from the output folder rather than picked out of AppDomain: an empty assembly is never
        // touched by the test run, so it would simply not be there to check.
        Assembly assembly = Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, $"{projectName}.dll"));

        string[] referenced =
        [
            .. assembly.GetReferencedAssemblies().Select(reference => reference.Name).OfType<string>()
        ];

        referenced.Should().OnlyContain(name => IsBaseClassLibrary(name));
    }

    private static bool IsBaseClassLibrary(string assemblyName) =>
        assemblyName.StartsWith("System.", StringComparison.Ordinal)
        || assemblyName is "System" or "netstandard" or "mscorlib";
}
