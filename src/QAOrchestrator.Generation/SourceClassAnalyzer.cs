using System.Text.RegularExpressions;
using QAOrchestrator.Domain;

namespace QAOrchestrator.Generation;

public sealed class SourceClassAnalyzer
{
    private static readonly Regex NamespaceRegex = new(@"namespace\s+(?<namespace>[A-Za-z0-9_.]+)\s*;|namespace\s+(?<namespace2>[A-Za-z0-9_.]+)\s*\{", RegexOptions.Compiled);
    private static readonly Regex ClassRegex = new(@"\b(?:public|internal|sealed|abstract|partial|\s)*\s*(?:class|record)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);
    private static readonly Regex PublicMethodRegex = new(@"\bpublic\s+(?:static\s+)?(?:async\s+)?(?:[A-Za-z_][A-Za-z0-9_<>.,?\s]*|Task|ValueTask)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\(", RegexOptions.Compiled);

    public IReadOnlyList<TestableSourceClass> Analyze(TargetSolution solution, QaOrchestratorConfig config)
    {
        var sourceProjects = solution.Projects.Where(project => project.Kind == ProjectKind.Source).ToArray();
        return sourceProjects
            .SelectMany(project => AnalyzeProject(solution.RootDirectory, project, config))
            .OrderBy(source => source.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<TestableSourceClass> AnalyzeProject(string rootDirectory, TargetProject project, QaOrchestratorConfig config)
    {
        if (!Directory.Exists(project.Directory))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(project.Directory, "*.cs", SearchOption.AllDirectories)
            .Where(file => IsEligible(file, rootDirectory, config)))
        {
            var text = File.ReadAllText(file);
            var classMatch = ClassRegex.Match(text);
            if (!classMatch.Success)
            {
                continue;
            }

            var className = classMatch.Groups["name"].Value;
            var publicMethods = PublicMethodRegex.Matches(text)
                .Select(match => match.Groups["name"].Value)
                .Where(name => !string.Equals(name, className, StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            var namespaceMatch = NamespaceRegex.Match(text);
            var ns = namespaceMatch.Success
                ? (namespaceMatch.Groups["namespace"].Success ? namespaceMatch.Groups["namespace"].Value : namespaceMatch.Groups["namespace2"].Value)
                : string.Empty;

            var kind = DetectKind(file, className, text);
            var testability = DetermineTestability(file, className, text, publicMethods, kind);

            yield return new TestableSourceClass(
                project.Name,
                file,
                Path.GetRelativePath(rootDirectory, file),
                ns,
                className,
                kind,
                publicMethods,
                DetectConstructorDependencies(className, text),
                testability.IsUnitTestable,
                testability.Reason);
        }
    }

    private static bool IsEligible(string file, string rootDirectory, QaOrchestratorConfig config)
    {
        var full = Path.GetFullPath(file);
        if (full.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            || full.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            || full.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
            || full.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var relative = Path.GetRelativePath(rootDirectory, file).Replace('\\', '/');
        foreach (var pattern in config.Coverage.Exclude)
        {
            if (MatchesSimpleGlob(relative, pattern))
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchesSimpleGlob(string relativePath, string pattern)
    {
        var normalized = pattern.Replace('\\', '/').Trim();
        if (normalized.StartsWith("**/", StringComparison.Ordinal))
        {
            normalized = normalized[3..];
        }

        if (normalized.EndsWith("/**", StringComparison.Ordinal))
        {
            var folder = normalized[..^3].Trim('/');
            return relativePath.Contains($"/{folder}/", StringComparison.OrdinalIgnoreCase)
                || relativePath.StartsWith($"{folder}/", StringComparison.OrdinalIgnoreCase);
        }

        if (normalized.StartsWith("*", StringComparison.Ordinal))
        {
            return relativePath.EndsWith(normalized[1..], StringComparison.OrdinalIgnoreCase);
        }

        return relativePath.EndsWith(normalized, StringComparison.OrdinalIgnoreCase);
    }

    private static TestableSourceKind DetectKind(string file, string className, string text)
    {
        if (text.Contains("AbstractValidator<", StringComparison.Ordinal))
        {
            return TestableSourceKind.Validator;
        }

        if (text.Contains("ICommandHandler<", StringComparison.Ordinal)
            || text.Contains("IQueryHandler<", StringComparison.Ordinal)
            || text.Contains("IRequestHandler<", StringComparison.Ordinal)
            || className.EndsWith("Handler", StringComparison.Ordinal))
        {
            return TestableSourceKind.Handler;
        }

        if (file.Contains($"{Path.DirectorySeparatorChar}Endpoints{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            || className.EndsWith("Endpoint", StringComparison.Ordinal))
        {
            return TestableSourceKind.Endpoint;
        }

        if (className.EndsWith("Service", StringComparison.Ordinal)) return TestableSourceKind.Service;
        if (className.EndsWith("Rule", StringComparison.Ordinal)) return TestableSourceKind.Rule;
        if (className.EndsWith("Policy", StringComparison.Ordinal)) return TestableSourceKind.Policy;
        if (className.EndsWith("Mapper", StringComparison.Ordinal)) return TestableSourceKind.Mapper;
        if (className.EndsWith("Factory", StringComparison.Ordinal)) return TestableSourceKind.Factory;
        if (className.EndsWith("Guard", StringComparison.Ordinal)) return TestableSourceKind.Guard;

        return TestableSourceKind.GeneralClass;
    }

    private static (bool IsUnitTestable, string Reason) DetermineTestability(
        string file,
        string className,
        string text,
        IReadOnlyList<string> publicMethods,
        TestableSourceKind kind)
    {
        if (IsPureDataShape(className, publicMethods, kind))
        {
            return (false, "Skipped because the class looks like a pure DTO/request/response/options/settings type.");
        }

        if (kind == TestableSourceKind.Endpoint)
        {
            if (publicMethods.Any(IsEndpointHandlerMethod) || text.Contains("static async", StringComparison.Ordinal) && text.Contains("IResult", StringComparison.Ordinal))
            {
                return (true, "Endpoint has an isolated handler method.");
            }

            if (text.Contains(".MapGet(", StringComparison.Ordinal)
                || text.Contains(".MapPost(", StringComparison.Ordinal)
                || text.Contains(".MapPut(", StringComparison.Ordinal)
                || text.Contains(".MapDelete(", StringComparison.Ordinal))
            {
                return (false, "Unit test skipped: endpoint handler is inline and should be covered by integration test.");
            }
        }

        if (publicMethods.Count == 0 && kind is not TestableSourceKind.Handler and not TestableSourceKind.Validator)
        {
            return (false, "Skipped because no public testable method was detected.");
        }

        return (true, $"Detected testable {kind}.");
    }

    private static bool IsPureDataShape(string className, IReadOnlyCollection<string> publicMethods, TestableSourceKind kind)
    {
        if (kind is not TestableSourceKind.GeneralClass)
        {
            return false;
        }

        return publicMethods.Count == 0
            && (className.EndsWith("Dto", StringComparison.Ordinal)
                || className.EndsWith("Request", StringComparison.Ordinal)
                || className.EndsWith("Response", StringComparison.Ordinal)
                || className.EndsWith("Options", StringComparison.Ordinal)
                || className.EndsWith("Settings", StringComparison.Ordinal));
    }

    private static bool IsEndpointHandlerMethod(string method)
        => method is "Handle" or "HandleAsync" or "Execute" or "ExecuteAsync";

    private static IReadOnlyList<string> DetectConstructorDependencies(string className, string text)
    {
        var match = Regex.Match(text, $@"public\s+{Regex.Escape(className)}\s*\((?<parameters>[^)]*)\)");
        if (!match.Success)
        {
            return [];
        }

        return match.Groups["parameters"].Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(parameter => parameter.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
    }
}
