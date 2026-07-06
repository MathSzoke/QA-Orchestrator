using QAOrchestrator.Domain;

namespace QAOrchestrator.Infrastructure;

public sealed class CleanArtifactService
{
    private static readonly string[] ReportExtensions = [".md", ".json", ".html"];

    public CleanPlan BuildPlan(string rootDirectory)
    {
        var root = Path.GetFullPath(rootDirectory);
        var targets = new List<CleanTarget>();

        AddFileIfExists(targets, root, Path.Combine(root, ConfigurationStore.FileName));
        AddDirectoryIfExists(targets, root, Path.Combine(root, "tests", ".qa-orchestrator", "candidates"));
        AddDirectoryIfExists(targets, root, Path.Combine(root, "tests", ".qa-orchestrator", "failed-candidates"));
        AddDirectoryIfExists(targets, root, Path.Combine(root, "tests", ".qa-orchestrator", "reports"));
        AddDirectoryIfExists(targets, root, Path.Combine(root, "tests", ".qa-orchestrator"));
        AddDirectoryIfExists(targets, root, Path.Combine(root, ".qa-orchestrator"));

        foreach (var directory in EnumerateQaOrchestratorDirectories(root))
        {
            AddDirectoryIfExists(targets, root, directory);
        }

        foreach (var report in EnumerateQaOrchestratorReports(root))
        {
            AddFileIfExists(targets, root, report);
        }

        var distinctTargets = targets
            .GroupBy(target => target.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(target => target.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new CleanPlan(root, distinctTargets);
    }

    public CleanResult Execute(CleanPlan plan, bool dryRun)
    {
        if (dryRun || !plan.HasTargets)
        {
            return new CleanResult(plan, dryRun, false, []);
        }

        var removed = new List<string>();
        foreach (var target in plan.Targets.OrderByDescending(target => target.Path.Length))
        {
            if (!IsSafeTarget(plan.RootDirectory, target.Path))
            {
                throw new InvalidOperationException($"Refusing to remove unsafe target: {target.Path}");
            }

            if (target.IsDirectory)
            {
                if (Directory.Exists(target.Path))
                {
                    Directory.Delete(target.Path, recursive: true);
                    removed.Add(target.RelativePath);
                }

                continue;
            }

            if (File.Exists(target.Path))
            {
                File.Delete(target.Path);
                removed.Add(target.RelativePath);
            }
        }

        DeleteEmptyKnownParent(plan.RootDirectory, Path.Combine(plan.RootDirectory, "tests", ".qa-orchestrator"));

        return new CleanResult(plan, dryRun, removed.Count > 0, removed);
    }

    private static IEnumerable<string> EnumerateQaOrchestratorDirectories(string root)
    {
        return Directory.EnumerateDirectories(root, ".qa-orchestrator", SearchOption.AllDirectories)
            .Where(path => !IsUnderBuildOutput(path));
    }

    private static IEnumerable<string> EnumerateQaOrchestratorReports(string root)
    {
        return Directory.EnumerateFiles(root, "qa-orchestrator*.*", SearchOption.AllDirectories)
            .Where(path => !IsUnderBuildOutput(path))
            .Where(path => ReportExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .Where(path => !IsProjectFile(path));
    }

    private static void AddFileIfExists(List<CleanTarget> targets, string root, string path)
    {
        if (File.Exists(path) && IsSafeTarget(root, path))
        {
            targets.Add(new CleanTarget(path, Path.GetRelativePath(root, path), false));
        }
    }

    private static void AddDirectoryIfExists(List<CleanTarget> targets, string root, string path)
    {
        if (Directory.Exists(path) && IsSafeTarget(root, path))
        {
            targets.Add(new CleanTarget(path, Path.GetRelativePath(root, path), true));
        }
    }

    private static bool IsSafeTarget(string root, string path)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(path);
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (IsProjectFile(fullPath))
        {
            return false;
        }

        if (File.Exists(fullPath))
        {
            var fileName = Path.GetFileName(fullPath);
            var extension = Path.GetExtension(fullPath);
            return fileName.Equals(ConfigurationStore.FileName, StringComparison.OrdinalIgnoreCase)
                || (fileName.StartsWith("qa-orchestrator", StringComparison.OrdinalIgnoreCase)
                    && ReportExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase));
        }

        return Directory.Exists(fullPath)
            && Path.GetFileName(fullPath).Equals(".qa-orchestrator", StringComparison.OrdinalIgnoreCase)
            || IsKnownQaChildDirectory(fullPath);
    }

    private static bool IsKnownQaChildDirectory(string path)
    {
        var name = Path.GetFileName(path);
        var parentName = Path.GetFileName(Path.GetDirectoryName(path) ?? string.Empty);
        return parentName.Equals(".qa-orchestrator", StringComparison.OrdinalIgnoreCase)
            && (name.Equals("candidates", StringComparison.OrdinalIgnoreCase)
                || name.Equals("failed-candidates", StringComparison.OrdinalIgnoreCase)
                || name.Equals("reports", StringComparison.OrdinalIgnoreCase)
                || name.Equals("temp", StringComparison.OrdinalIgnoreCase)
                || name.Equals("tmp", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsProjectFile(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUnderBuildOutput(string path)
    {
        var parts = Path.GetFullPath(path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(part => part.Equals("bin", StringComparison.OrdinalIgnoreCase)
            || part.Equals("obj", StringComparison.OrdinalIgnoreCase));
    }

    private static void DeleteEmptyKnownParent(string root, string path)
    {
        if (!Directory.Exists(path) || !IsSafeTarget(root, path))
        {
            return;
        }

        if (!Directory.EnumerateFileSystemEntries(path).Any())
        {
            Directory.Delete(path);
        }
    }
}
