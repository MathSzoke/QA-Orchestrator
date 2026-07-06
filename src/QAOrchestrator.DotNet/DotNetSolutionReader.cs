using System.Text.RegularExpressions;
using System.Xml.Linq;
using QAOrchestrator.Domain;

namespace QAOrchestrator.DotNet;

public sealed class DotNetSolutionReader
{
    private readonly TestPackageClassifier _packageClassifier;
    private readonly TestPatternReader _testPatternReader;

    private static readonly Regex SolutionProjectRegex = new(
        "^Project\\(\"(?<typeGuid>[^\"]+)\"\\) = \"(?<name>[^\"]+)\", \"(?<path>[^\"]+)\", \"(?<guid>[^\"]+)\"",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public DotNetSolutionReader()
        : this(new TestPackageClassifier(), new TestPatternReader())
    {
    }

    public DotNetSolutionReader(TestPackageClassifier packageClassifier, TestPatternReader testPatternReader)
    {
        _packageClassifier = packageClassifier;
        _testPatternReader = testPatternReader;
    }

    public TargetSolution Read(string solutionPath)
    {
        if (string.IsNullOrWhiteSpace(solutionPath))
        {
            throw new ArgumentException("Solution path is required.", nameof(solutionPath));
        }

        var fullSolutionPath = Path.GetFullPath(solutionPath);
        if (!File.Exists(fullSolutionPath))
        {
            throw new FileNotFoundException($"Solution file was not found: {fullSolutionPath}", fullSolutionPath);
        }

        var rootDirectory = Path.GetDirectoryName(fullSolutionPath)
            ?? throw new InvalidOperationException($"Unable to resolve solution directory for {fullSolutionPath}.");

        var projects = File.ReadLines(fullSolutionPath)
            .Select(line => SolutionProjectRegex.Match(line))
            .Where(match => match.Success)
            .Select(match => match.Groups["path"].Value)
            .Where(path => path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.GetFullPath(Path.Combine(rootDirectory, path.Replace('\\', Path.DirectorySeparatorChar))))
            .Where(File.Exists)
            .Select(ReadProject)
            .OrderBy(project => project.Kind)
            .ThenBy(project => project.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var architecture = new ArchitectureDetector().Detect(rootDirectory, projects);

        return new TargetSolution(
            Path.GetFileNameWithoutExtension(fullSolutionPath),
            fullSolutionPath,
            rootDirectory,
            projects,
            architecture);
    }

    public TargetProject ReadProject(string projectPath)
    {
        var fullProjectPath = Path.GetFullPath(projectPath);
        var document = XDocument.Load(fullProjectPath, LoadOptions.PreserveWhitespace);
        var root = document.Root ?? throw new InvalidOperationException($"Invalid project file: {fullProjectPath}");

        var packageReferences = root
            .Descendants()
            .Where(element => element.Name.LocalName == "PackageReference")
            .Select(ReadPackageReference)
            .Where(package => !string.IsNullOrWhiteSpace(package.Name))
            .DistinctBy(package => package.Name, StringComparer.OrdinalIgnoreCase)
            .OrderBy(package => package.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var projectReferences = root
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => ReadProjectReference(element, Path.GetDirectoryName(fullProjectPath)!))
            .ToArray();

        var targetFrameworks = root
            .Descendants()
            .Where(element => element.Name.LocalName is "TargetFramework" or "TargetFrameworks")
            .SelectMany(element => (element.Value ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(framework => framework, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var projectName = Path.GetFileNameWithoutExtension(fullProjectPath);
        var packageClassifications = _packageClassifier.Classify(packageReferences);
        var testFramework = _packageClassifier.DetectTestFramework(packageClassifications);
        var mockFramework = _packageClassifier.DetectMockFramework(packageClassifications);
        var assertionFramework = _packageClassifier.DetectAssertionFramework(packageClassifications);
        var kind = IsTestProject(projectName, root, packageReferences, testFramework)
            ? ProjectKind.Test
            : ProjectKind.Source;

        var project = new TargetProject(
            projectName,
            fullProjectPath,
            Path.GetDirectoryName(fullProjectPath)!,
            kind,
            targetFrameworks,
            packageReferences,
            projectReferences,
            testFramework,
            mockFramework,
            assertionFramework,
            packageClassifications,
            null);

        return project.IsTestProject
            ? project with { TestPattern = _testPatternReader.Read(project) }
            : project;
    }

    private static PackageReferenceInfo ReadPackageReference(XElement element)
    {
        var name = element.Attribute("Include")?.Value
            ?? element.Attribute("Update")?.Value
            ?? string.Empty;

        var version = element.Attribute("Version")?.Value
            ?? element.Elements().FirstOrDefault(child => child.Name.LocalName == "Version")?.Value
            ?? string.Empty;

        return new PackageReferenceInfo(name, version);
    }

    private static ProjectReferenceInfo ReadProjectReference(XElement element, string projectDirectory)
    {
        var include = element.Attribute("Include")?.Value ?? string.Empty;
        var fullPath = string.IsNullOrWhiteSpace(include)
            ? null
            : Path.GetFullPath(Path.Combine(projectDirectory, include));

        return new ProjectReferenceInfo(include, fullPath is null ? null : Path.GetFileNameWithoutExtension(fullPath));
    }

    private static bool IsTestProject(
        string projectName,
        XElement root,
        IReadOnlyCollection<PackageReferenceInfo> packageReferences,
        TestFramework testFramework)
    {
        if (testFramework != TestFramework.Unknown)
        {
            return true;
        }

        var isTestProjectProperty = root
            .Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "IsTestProject")
            ?.Value;

        if (bool.TryParse(isTestProjectProperty, out var isTestProject) && isTestProject)
        {
            return true;
        }

        if (projectName.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase)
            || projectName.EndsWith(".UnitTests", StringComparison.OrdinalIgnoreCase)
            || projectName.EndsWith(".IntegrationTests", StringComparison.OrdinalIgnoreCase)
            || projectName.EndsWith(".FunctionalTests", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return HasPackage(packageReferences, "Microsoft.NET.Test.Sdk");
    }

    private static bool HasPackage(IEnumerable<PackageReferenceInfo> packages, string packageName)
        => packages.Any(package => string.Equals(package.Name, packageName, StringComparison.OrdinalIgnoreCase));
}
