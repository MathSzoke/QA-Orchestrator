using QAOrchestrator.Domain;

namespace QAOrchestrator.Generation;

public sealed class ExistingTestMatcher
{
    private static readonly string[] ExactSuffixes = ["Tests", "Test", "Specs", "Should", "CoverageTests", "AdditionalCoverageTests"];

    public ExistingTestMatchResult Match(TargetSolution solution, TestableSourceClass sourceClass)
    {
        var testFiles = EnumerateTestFiles(solution).ToArray();
        var exact = new List<ExistingTestMatch>();
        var strong = new List<ExistingTestMatch>();
        var weak = new List<ExistingTestMatch>();

        foreach (var testFile in testFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(testFile);
            var relative = Path.GetRelativePath(solution.RootDirectory, testFile);
            var content = SafeRead(testFile);

            if (IsExactFileName(fileName, sourceClass.ClassName))
            {
                exact.Add(new ExistingTestMatch(ExistingTestMatchKind.Exact, testFile, relative, 100, "Test file name matches the source class."));
                continue;
            }

            if (content.Contains(sourceClass.ClassName, StringComparison.Ordinal))
            {
                strong.Add(new ExistingTestMatch(ExistingTestMatchKind.Strong, testFile, relative, 85, "Test file references the source class name."));
                continue;
            }

            var featureToken = GetFeatureToken(sourceClass);
            var sourceStem = StripKnownSourceSuffix(sourceClass.ClassName);
            if (!string.IsNullOrWhiteSpace(featureToken)
                && relative.Contains(featureToken, StringComparison.OrdinalIgnoreCase)
                && fileName.Contains(sourceStem, StringComparison.OrdinalIgnoreCase))
            {
                strong.Add(new ExistingTestMatch(ExistingTestMatchKind.Strong, testFile, relative, 75, "Test file matches source feature and related type name."));
                continue;
            }

            if (!string.IsNullOrWhiteSpace(featureToken) && relative.Contains(featureToken, StringComparison.OrdinalIgnoreCase))
            {
                weak.Add(new ExistingTestMatch(ExistingTestMatchKind.Weak, testFile, relative, 45, "Test file is in the same feature or slice area."));
            }
        }

        var confidence = exact.Concat(strong).Concat(weak).Select(match => match.Confidence).DefaultIfEmpty(0).Max();
        var reason = exact.Count > 0
            ? "Existing exact test found."
            : strong.Count > 0
                ? "Existing strong related test found."
                : weak.Count > 0
                    ? "Existing weak related test found."
                    : "No related existing tests found.";

        return new ExistingTestMatchResult(sourceClass, exact, strong, weak, confidence, reason);
    }

    private static IEnumerable<string> EnumerateTestFiles(TargetSolution solution)
        => solution.Projects
            .Where(project => project.IsTestProject && Directory.Exists(project.Directory))
            .SelectMany(project => Directory.EnumerateFiles(project.Directory, "*.cs", SearchOption.AllDirectories))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

    private static bool IsExactFileName(string fileName, string className)
        => ExactSuffixes.Any(suffix => string.Equals(fileName, className + suffix, StringComparison.OrdinalIgnoreCase));

    private static string StripKnownSourceSuffix(string className)
    {
        foreach (var suffix in new[] { "Handler", "Validator", "Service", "Rule", "Policy", "Mapper", "Factory", "Guard", "Endpoint" })
        {
            if (className.EndsWith(suffix, StringComparison.Ordinal))
            {
                return className[..^suffix.Length];
            }
        }

        return className;
    }

    private static string GetFeatureToken(TestableSourceClass sourceClass)
    {
        var parts = sourceClass.RelativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var featuresIndex = Array.FindIndex(parts, part => part.Equals("Features", StringComparison.OrdinalIgnoreCase));
        if (featuresIndex >= 0 && featuresIndex + 1 < parts.Length)
        {
            return parts[featuresIndex + 1];
        }

        return parts.Length >= 2 ? parts[^2] : string.Empty;
    }

    private static string SafeRead(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch
        {
            return string.Empty;
        }
    }
}
