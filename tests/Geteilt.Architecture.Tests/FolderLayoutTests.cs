using System.Reflection;

namespace Geteilt.Architecture.Tests;

/// <summary>
/// The folder rule: inside a project a folder names a feature, not a
/// kind of file. Namespaces follow folders, so checking namespaces checks the layout.
/// </summary>
public sealed class FolderLayoutTests
{
    private static readonly string[] KindsOfFile =
        ["Entities", "Exceptions", "Models", "Interfaces", "Helpers", "Pages"];

    [Theory]
    [InlineData("Geteilt.Domain")]
    [InlineData("Geteilt.Application")]
    [InlineData("Geteilt.Web")]
    public void Namespaces_NameFeatures_NotKindsOfFile(string projectName)
    {
        // Reflection is safe here, unlike for references: the compiler drops unused
        // references, never types.
        Assembly assembly = Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, $"{projectName}.dll"));

        string[] offending =
        [
            .. assembly.GetTypes()
                .Select(type => type.Namespace)
                .OfType<string>()
                .Where(name => name.Split('.').Intersect(KindsOfFile, StringComparer.Ordinal).Any())
                .Distinct()
                .Order(StringComparer.Ordinal)
        ];

        offending.Should().BeEmpty(
            "a folder in {0} names a feature such as Groups or Expenses, so one idea lives in one place",
            projectName);
    }
}
