using QAOrchestrator.Domain;

namespace QAOrchestrator.Generation;

public sealed class TestLocationResolver
{
    public TestLocationResolution Resolve(
        TargetSolution solution,
        QaOrchestratorConfig config,
        TestableSourceClass sourceClass,
        ExistingTestMatchResult match,
        TestType testType)
    {
        var isComplementary = match.HasExactOrStrongMatch;
        var fileName = sourceClass.ClassName + (isComplementary ? "AdditionalCoverageTests.cs" : "CoverageTests.cs");
        var candidatePath = BuildCandidatePath(solution.RootDirectory, config.Generation.CandidateFolder, "pending", testType, sourceClass, fileName);

        if (match.ExactMatches.Count > 0 || match.StrongMatches.Count > 0)
        {
            var reference = match.ExactMatches.Concat(match.StrongMatches).OrderByDescending(item => item.Confidence).First();
            return new TestLocationResolution(
                testType,
                FindOwningTestProject(solution, reference.TestFilePath)?.Name,
                match.ExactMatches.Select(item => item.TestFilePath).ToArray(),
                match.StrongMatches.Select(item => item.TestFilePath).ToArray(),
                Path.Combine(Path.GetDirectoryName(reference.TestFilePath)!, fileName),
                candidatePath,
                Math.Max(80, reference.Confidence),
                "Existing test file found. Candidate generated only as complementary coverage, not duplicate replacement.",
                true,
                false);
        }

        var weakReference = match.WeakMatches.OrderByDescending(item => item.Confidence).FirstOrDefault();
        if (weakReference is not null)
        {
            return new TestLocationResolution(
                testType,
                FindOwningTestProject(solution, weakReference.TestFilePath)?.Name,
                [],
                match.WeakMatches.Select(item => item.TestFilePath).ToArray(),
                Path.Combine(Path.GetDirectoryName(weakReference.TestFilePath)!, fileName),
                candidatePath,
                65,
                "Matched existing test in same feature or slice area and reused that folder convention.",
                false,
                false);
        }

        var matchedProject = FindMatchingTestProject(solution, sourceClass, testType);
        if (matchedProject is not null)
        {
            var finalPath = Path.Combine(matchedProject.Directory, BuildRelativeTestPathFromSource(sourceClass), fileName);
            return new TestLocationResolution(
                testType,
                matchedProject.Name,
                [],
                [],
                finalPath,
                candidatePath,
                55,
                "Matched test project by source/test project naming or project reference.",
                false,
                false);
        }

        return new TestLocationResolution(
            testType,
            null,
            [],
            [],
            candidatePath,
            candidatePath,
            20,
            "No reliable test project or existing convention found. Candidate remains in QA Orchestrator area and needs manual placement.",
            false,
            true);
    }

    private static string BuildCandidatePath(
        string rootDirectory,
        string candidateFolder,
        string statusFolder,
        TestType testType,
        TestableSourceClass sourceClass,
        string fileName)
    {
        var relative = BuildRelativeTestPathFromSource(sourceClass);
        return Path.Combine(rootDirectory, Normalize(candidateFolder), statusFolder, testType.ToString().ToLowerInvariant(), relative, fileName);
    }

    public static string MoveCandidatePathToStatus(string candidatePath, string currentStatus, string newStatus)
    {
        var parts = candidatePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        for (var index = 0; index < parts.Length; index++)
        {
            if (parts[index].Equals(currentStatus, StringComparison.OrdinalIgnoreCase))
            {
                parts[index] = newStatus;
                return Path.Combine(parts);
            }
        }

        return candidatePath;
    }

    private static TargetProject? FindOwningTestProject(TargetSolution solution, string testFilePath)
        => solution.Projects
            .Where(project => project.IsTestProject)
            .OrderByDescending(project => project.Directory.Length)
            .FirstOrDefault(project => testFilePath.StartsWith(project.Directory, StringComparison.OrdinalIgnoreCase));

    private static TargetProject? FindMatchingTestProject(TargetSolution solution, TestableSourceClass sourceClass, TestType testType)
    {
        var sourceStem = NormalizeProjectName(sourceClass.ProjectName);
        var preferredMarkers = testType == TestType.Unit
            ? new[] { "Unit", "UnitTests", "Tests" }
            : new[] { testType.ToString(), "Tests" };

        return solution.Projects
            .Where(project => project.IsTestProject)
            .Select(project => new
            {
                Project = project,
                Score = ScoreTestProject(project, sourceStem, preferredMarkers)
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Project.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => item.Project)
            .FirstOrDefault();
    }

    private static int ScoreTestProject(TargetProject testProject, string sourceStem, IReadOnlyList<string> preferredMarkers)
    {
        var normalized = NormalizeProjectName(testProject.Name);
        var score = 0;
        if (normalized.Contains(sourceStem, StringComparison.OrdinalIgnoreCase) || sourceStem.Contains(normalized.Replace("Tests", "", StringComparison.OrdinalIgnoreCase), StringComparison.OrdinalIgnoreCase))
        {
            score += 40;
        }

        if (testProject.ProjectReferences.Any(reference => reference.ProjectName is not null && NormalizeProjectName(reference.ProjectName).Contains(sourceStem, StringComparison.OrdinalIgnoreCase)))
        {
            score += 35;
        }

        if (preferredMarkers.Any(marker => testProject.Name.Contains(marker, StringComparison.OrdinalIgnoreCase) || testProject.Directory.Contains(marker, StringComparison.OrdinalIgnoreCase)))
        {
            score += 20;
        }

        return score;
    }

    private static string NormalizeProjectName(string value)
        => value.Replace(".Tests", "", StringComparison.OrdinalIgnoreCase)
            .Replace(".UnitTests", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Tests", "", StringComparison.OrdinalIgnoreCase);

    private static string BuildRelativeTestPathFromSource(TestableSourceClass sourceClass)
    {
        var path = sourceClass.RelativePath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        var withoutFile = Path.GetDirectoryName(path) ?? string.Empty;
        var parts = withoutFile.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

        var featuresIndex = Array.FindIndex(parts, part => part.Equals("Features", StringComparison.OrdinalIgnoreCase));
        if (featuresIndex > 0)
        {
            var slice = parts[featuresIndex - 1];
            var featureParts = parts.Skip(featuresIndex).ToArray();
            return Path.Combine(new[] { slice }.Concat(featureParts).ToArray());
        }

        var srcIndex = Array.FindIndex(parts, part => part.Equals("src", StringComparison.OrdinalIgnoreCase));
        if (srcIndex >= 0 && srcIndex + 2 < parts.Length)
        {
            return Path.Combine(parts.Skip(srcIndex + 2).ToArray());
        }

        return parts.Length == 0 ? string.Empty : Path.Combine(parts.TakeLast(Math.Min(3, parts.Length)).ToArray());
    }

    private static string Normalize(string path)
        => path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
}
