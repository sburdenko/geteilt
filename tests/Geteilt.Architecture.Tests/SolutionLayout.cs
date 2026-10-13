using System.Xml.Linq;

namespace Geteilt.Architecture.Tests;

/// <summary>
/// Reads the project files off disk rather than asking the runtime what an assembly references.
/// The compiler drops references nothing uses, so a package added to a project but not yet called
/// would be invisible to reflection — and that is exactly the moment the rule should fire.
/// </summary>
internal static class SolutionLayout
{
    internal static string RepositoryRoot { get; } = FindRepositoryRoot();

    internal static IReadOnlyList<string> PackageReferences(string projectName) =>
        ItemsOf(projectName, "PackageReference");

    internal static IReadOnlyList<string> ProjectReferences(string projectName) =>
    [
        // MSBuild writes include paths with backslashes whatever the platform, and on Unix a backslash
        // is an ordinary filename character — so the separator has to be normalised before splitting.
        .. ItemsOf(projectName, "ProjectReference")
            .Select(include => Path.GetFileNameWithoutExtension(include.Replace('\\', '/')))
            .OfType<string>()
    ];

    private static IReadOnlyList<string> ItemsOf(string projectName, string itemName)
    {
        XDocument project = XDocument.Load(ProjectFilePath(projectName));

        return
        [
            .. project.Descendants(itemName)
                .Select(item => item.Attribute("Include")?.Value)
                .OfType<string>()
        ];
    }

    private static string ProjectFilePath(string projectName)
    {
        string[] candidates =
        [
            Path.Combine(RepositoryRoot, "src", projectName, $"{projectName}.csproj"),
            Path.Combine(RepositoryRoot, "tests", projectName, $"{projectName}.csproj"),
        ];

        return Array.Find(candidates, File.Exists)
               ?? throw new FileNotFoundException($"No project file for {projectName}.");
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Geteilt.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
               ?? throw new DirectoryNotFoundException($"Geteilt.sln not found above {AppContext.BaseDirectory}.");
    }
}
