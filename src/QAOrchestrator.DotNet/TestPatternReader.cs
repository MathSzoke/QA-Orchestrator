using System.Text.RegularExpressions;
using QAOrchestrator.Domain;

namespace QAOrchestrator.DotNet;

public sealed class TestPatternReader
{
    private static readonly Regex UsingRegex = new(@"^\s*using\s+(?<namespace>[A-Za-z0-9_.]+)\s*;", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex ClassRegex = new(@"\b(?:public|internal|sealed|abstract|partial|\s)*\s*class\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);
    private static readonly Regex MethodRegex = new(@"\b(?:public|internal)\s+(?:async\s+)?(?:Task|ValueTask|void|[A-Za-z_][A-Za-z0-9_<>.,?\s]*)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\(", RegexOptions.Compiled);
    private static readonly Regex AttributeRegex = new(@"\[(?<name>Fact|Theory|InlineData|MemberData|Trait|ClassData|Collection|CollectionDefinition|CollectionBehavior|ClassFixture|CollectionFixture)(?:Attribute)?(?:\(|\])", RegexOptions.Compiled);
    private static readonly string[] ClassSuffixes = ["Tests", "Test", "Specs", "Should"];

    public TestProjectPattern Read(TargetProject project)
    {
        if (!project.IsTestProject || !Directory.Exists(project.Directory))
        {
            return Empty(project.Name);
        }

        var files = Directory.EnumerateFiles(project.Directory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var documents = files
            .Select(path => new TestSourceDocument(path, File.ReadAllText(path)))
            .ToArray();

        var classes = documents
            .SelectMany(document => ClassRegex.Matches(document.Content)
                .Select(match => new TestClassPattern(
                    match.Groups["name"].Value,
                    DetectClassSuffix(match.Groups["name"].Value),
                    document.Path)))
            .ToArray();

        var methods = documents
            .SelectMany(document => MethodRegex.Matches(document.Content)
                .Select(match =>
                {
                    var name = match.Groups["name"].Value;
                    return new TestMethodPattern(name, DetectMethodNamingConvention(name), document.Path);
                }))
            .Where(method => !string.Equals(method.Name, "Dispose", StringComparison.Ordinal)
                && !string.Equals(method.Name, "InitializeAsync", StringComparison.Ordinal)
                && !string.Equals(method.Name, "DisposeAsync", StringComparison.Ordinal))
            .ToArray();

        var attributes = documents
            .SelectMany(document => AttributeRegex.Matches(document.Content).Select(match => NormalizeAttributeName(match.Groups["name"].Value)))
            .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select(group => new TestAttributeUsage(group.Key, group.Count()))
            .OrderByDescending(usage => usage.Count)
            .ThenBy(usage => usage.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var usings = documents
            .SelectMany(document => UsingRegex.Matches(document.Content).Select(match => match.Groups["namespace"].Value))
            .GroupBy(ns => ns, StringComparer.OrdinalIgnoreCase)
            .Select(group => new DetectedUsing(group.Key, group.Count()))
            .OrderByDescending(usingInfo => usingInfo.Count)
            .ThenBy(usingInfo => usingInfo.Namespace, StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToArray();

        var namingConventions = methods
            .Select(method => method.NamingConvention)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .GroupBy(name => name!, StringComparer.OrdinalIgnoreCase)
            .Select(group => new TestNamingConvention(group.Key, group.Count()))
            .OrderByDescending(convention => convention.Count)
            .ThenBy(convention => convention.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var allText = string.Join(Environment.NewLine, documents.Select(document => document.Content));
        var codePattern = new TestCodePattern(
            UsesArrangeActAssert(allText),
            Contains(allText, "Mock<"),
            Contains(allText, ".Setup("),
            Contains(allText, ".Verify("),
            Contains(allText, "Fixture") || Contains(allText, "IClassFixture<") || Contains(allText, "ICollectionFixture<"),
            Contains(allText, "WebApplicationFactory<"),
            Contains(allText, "HttpClient"),
            Contains(allText, ".Should()"),
            Contains(allText, "AwesomeAssertions"),
            Contains(allText, "RestService.For<") || Contains(allText, "AddRefitClient<"),
            Contains(allText, "IClassFixture<") || Contains(allText, "ICollectionFixture<") || Contains(allText, "[Collection("));

        var dependencies = BuildDependencyUsage(codePattern, allText);

        return new TestProjectPattern(
            project.Name,
            classes.Length,
            classes,
            methods,
            attributes,
            usings,
            namingConventions,
            codePattern,
            dependencies);
    }

    private static TestProjectPattern Empty(string projectName)
        => new(
            projectName,
            0,
            [],
            [],
            [],
            [],
            [],
            new TestCodePattern(false, false, false, false, false, false, false, false, false, false, false),
            []);

    private static IReadOnlyList<TestDependencyUsage> BuildDependencyUsage(TestCodePattern pattern, string text)
    {
        var usages = new List<TestDependencyUsage>();
        AddIf(usages, "Moq Mock<T>", pattern.UsesMoqMock, CountOccurrences(text, "Mock<"));
        AddIf(usages, "Moq Setup", pattern.UsesMoqSetup, CountOccurrences(text, ".Setup("));
        AddIf(usages, "Moq Verify", pattern.UsesMoqVerify, CountOccurrences(text, ".Verify("));
        AddIf(usages, "Fixture", pattern.UsesFixture, CountOccurrences(text, "Fixture"));
        AddIf(usages, "WebApplicationFactory", pattern.UsesWebApplicationFactory, CountOccurrences(text, "WebApplicationFactory<"));
        AddIf(usages, "HttpClient", pattern.UsesHttpClient, CountOccurrences(text, "HttpClient"));
        AddIf(usages, "Should()", pattern.UsesShouldAssertions, CountOccurrences(text, ".Should()"));
        AddIf(usages, "AwesomeAssertions", pattern.UsesAwesomeAssertions, CountOccurrences(text, "AwesomeAssertions"));
        AddIf(usages, "Refit clients", pattern.UsesRefitClients, CountOccurrences(text, "AddRefitClient<") + CountOccurrences(text, "RestService.For<"));
        AddIf(usages, "xUnit collection fixtures", pattern.UsesXUnitCollectionFixtures, CountOccurrences(text, "ICollectionFixture<") + CountOccurrences(text, "[Collection("));
        return usages.OrderByDescending(usage => usage.Count).ThenBy(usage => usage.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void AddIf(List<TestDependencyUsage> usages, string name, bool condition, int count)
    {
        if (condition)
        {
            usages.Add(new TestDependencyUsage(name, Math.Max(1, count)));
        }
    }

    private static bool UsesArrangeActAssert(string text)
    {
        return (Contains(text, "Arrange") && Contains(text, "Act") && Contains(text, "Assert"))
            || (Contains(text, "var result") && Contains(text, ".Should()"))
            || (Contains(text, "var actual") && Contains(text, ".Should()"));
    }

    private static string? DetectClassSuffix(string className)
        => ClassSuffixes.FirstOrDefault(suffix => className.EndsWith(suffix, StringComparison.Ordinal));

    private static string? DetectMethodNamingConvention(string methodName)
    {
        if (methodName.StartsWith("Should_", StringComparison.Ordinal) && methodName.Contains("_When_", StringComparison.Ordinal))
        {
            return "Should_ExpectedBehavior_When_State";
        }

        if (methodName.Contains("_Should_", StringComparison.Ordinal) && methodName.Contains("_When_", StringComparison.Ordinal))
        {
            return "Method_Should_ExpectedBehavior_When_State";
        }

        if (methodName.StartsWith("Given_", StringComparison.Ordinal) && methodName.Contains("_When_", StringComparison.Ordinal) && methodName.Contains("_Then_", StringComparison.Ordinal))
        {
            return "Given_When_Then";
        }

        if (methodName.StartsWith("Should", StringComparison.Ordinal) && methodName.Contains("When", StringComparison.Ordinal))
        {
            return "ShouldExpectedBehaviorWhenState";
        }

        if (methodName.Contains('_', StringComparison.Ordinal))
        {
            return "UnderscoreSeparated";
        }

        return "DescriptiveName";
    }

    private static string NormalizeAttributeName(string name)
        => name.EndsWith("Attribute", StringComparison.Ordinal) ? name[..^"Attribute".Length] : name;

    private static bool Contains(string text, string value)
        => text.Contains(value, StringComparison.Ordinal);

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private sealed record TestSourceDocument(string Path, string Content);
}
