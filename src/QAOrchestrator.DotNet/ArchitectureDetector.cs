using QAOrchestrator.Domain;

namespace QAOrchestrator.DotNet;

public interface IArchitectureDetector
{
    ProjectArchitecture Detect(string rootDirectory, IReadOnlyList<TargetProject> projects);
}

public sealed class ArchitectureDetector
{
    private readonly IArchitectureDetector[] _detectors =
    [
        new VerticalSliceCqrsArchitectureDetector(),
        new MinimalApiArchitectureDetector(),
        new ControllerApiArchitectureDetector(),
        new CleanArchitectureDetector(),
        new SimpleServiceLayerArchitectureDetector(),
        new GenericDotNetArchitectureDetector()
    ];

    public ArchitectureDetectionResult Detect(string rootDirectory, IReadOnlyList<TargetProject> projects)
    {
        var detected = _detectors
            .Select(detector => detector.Detect(rootDirectory, projects))
            .OrderByDescending(result => result.Confidence)
            .ThenBy(result => result.Style == ArchitectureStyle.GenericDotNet ? 1 : 0)
            .ToArray();

        var primary = detected.FirstOrDefault(result => result.Style != ArchitectureStyle.GenericDotNet && result.Confidence > 0)
            ?? detected.First(result => result.Style == ArchitectureStyle.GenericDotNet);

        return new ArchitectureDetectionResult(primary, detected);
    }
}

public sealed class VerticalSliceCqrsArchitectureDetector : IArchitectureDetector
{
    private static readonly string[] CqrsMarkers =
    [
        "ICommand",
        "ICommandHandler",
        "IQuery",
        "IQueryHandler",
        "Dispatcher",
        "ValidationBehavior",
        "Result<"
    ];

    public ProjectArchitecture Detect(string rootDirectory, IReadOnlyList<TargetProject> projects)
    {
        var score = 0;
        var reasons = new List<string>();

        if (Directory.EnumerateDirectories(rootDirectory, "*", SearchOption.AllDirectories)
            .Any(path => path.Contains("VerticalSlices", StringComparison.OrdinalIgnoreCase)))
        {
            score += 35;
            reasons.Add("found VerticalSlices directory");
        }

        if (Directory.EnumerateDirectories(rootDirectory, "*", SearchOption.AllDirectories)
            .Any(path => path.EndsWith("Features", StringComparison.OrdinalIgnoreCase)))
        {
            score += 20;
            reasons.Add("found Features directories");
        }

        var markerHits = CountCqrsMarkerHits(rootDirectory);
        if (markerHits > 0)
        {
            score += Math.Min(35, markerHits * 7);
            reasons.Add($"found {markerHits} CQRS marker(s)");
        }

        if (projects.Any(project => project.Name.Contains("Application", StringComparison.OrdinalIgnoreCase)))
        {
            score += 5;
            reasons.Add("found Application project");
        }

        return new ProjectArchitecture(
            ArchitectureStyle.VerticalSliceCqrs,
            Math.Min(score, 100),
            reasons.Count == 0 ? "no vertical slice or CQRS markers found" : string.Join("; ", reasons));
    }

    private static int CountCqrsMarkerHits(string rootDirectory)
    {
        return EnumerateCSharpFiles(rootDirectory)
            .SelectMany(file => CqrsMarkers.Select(marker => File.ReadAllText(file).Contains(marker, StringComparison.Ordinal) ? marker : null))
            .Count(marker => marker is not null);
    }

    private static IEnumerable<string> EnumerateCSharpFiles(string rootDirectory)
        => Directory.EnumerateFiles(rootDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
}

public sealed class MinimalApiArchitectureDetector : IArchitectureDetector
{
    public ProjectArchitecture Detect(string rootDirectory, IReadOnlyList<TargetProject> projects)
    {
        var hits = Directory.EnumerateFiles(rootDirectory, "Program.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Select(File.ReadAllText)
            .Count(text => text.Contains(".MapGet(", StringComparison.Ordinal)
                || text.Contains(".MapPost(", StringComparison.Ordinal)
                || text.Contains(".MapPut(", StringComparison.Ordinal)
                || text.Contains(".MapDelete(", StringComparison.Ordinal)
                || text.Contains(".MapGroup(", StringComparison.Ordinal));

        var score = hits == 0 ? 0 : Math.Min(80, 45 + (hits * 10));
        return new ProjectArchitecture(
            ArchitectureStyle.MinimalApi,
            score,
            hits == 0 ? "no Minimal API route mappings found" : $"found Minimal API mappings in {hits} Program.cs file(s)");
    }
}

public sealed class ControllerApiArchitectureDetector : IArchitectureDetector
{
    public ProjectArchitecture Detect(string rootDirectory, IReadOnlyList<TargetProject> projects)
    {
        var controllerFiles = Directory.EnumerateFiles(rootDirectory, "*Controller.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Count();

        var mvcPackage = projects.Any(project => project.PackageReferences.Any(package =>
            package.Name.Contains("AspNetCore.Mvc", StringComparison.OrdinalIgnoreCase)));

        var score = Math.Min(85, (controllerFiles * 12) + (mvcPackage ? 20 : 0));
        return new ProjectArchitecture(
            ArchitectureStyle.ControllerApi,
            score,
            controllerFiles == 0 && !mvcPackage
                ? "no controller files or MVC packages found"
                : $"found {controllerFiles} controller file(s){(mvcPackage ? " and MVC package reference" : string.Empty)}");
    }
}

public sealed class CleanArchitectureDetector : IArchitectureDetector
{
    public ProjectArchitecture Detect(string rootDirectory, IReadOnlyList<TargetProject> projects)
    {
        var names = projects.Select(project => project.Name).ToArray();
        var score = 0;
        var reasons = new List<string>();

        score += AddIfProjectExists(names, "Domain", "Domain project", reasons);
        score += AddIfProjectExists(names, "Application", "Application project", reasons);
        score += AddIfProjectExists(names, "Infrastructure", "Infrastructure project", reasons);
        score += AddIfProjectExists(names, "Web", "Web/API project", reasons);

        return new ProjectArchitecture(
            ArchitectureStyle.CleanArchitecture,
            Math.Min(score, 90),
            reasons.Count == 0 ? "clean architecture project naming not found" : string.Join("; ", reasons));
    }

    private static int AddIfProjectExists(IEnumerable<string> projectNames, string marker, string reason, List<string> reasons)
    {
        if (!projectNames.Any(name => name.Contains(marker, StringComparison.OrdinalIgnoreCase)))
        {
            return 0;
        }

        reasons.Add(reason);
        return 20;
    }
}

public sealed class SimpleServiceLayerArchitectureDetector : IArchitectureDetector
{
    public ProjectArchitecture Detect(string rootDirectory, IReadOnlyList<TargetProject> projects)
    {
        var serviceFiles = Directory.EnumerateFiles(rootDirectory, "*Service.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Count();

        var repositoryFiles = Directory.EnumerateFiles(rootDirectory, "*Repository.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Count();

        var score = Math.Min(75, (serviceFiles * 8) + (repositoryFiles * 5));
        return new ProjectArchitecture(
            ArchitectureStyle.SimpleServiceLayer,
            score,
            score == 0 ? "no service/repository naming markers found" : $"found {serviceFiles} service file(s) and {repositoryFiles} repository file(s)");
    }
}

public sealed class GenericDotNetArchitectureDetector : IArchitectureDetector
{
    public ProjectArchitecture Detect(string rootDirectory, IReadOnlyList<TargetProject> projects)
        => new(ArchitectureStyle.GenericDotNet, 100, "fallback available for any .NET solution");
}
