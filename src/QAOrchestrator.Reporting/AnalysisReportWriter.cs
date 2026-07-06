using System.Text;
using QAOrchestrator.Domain;

namespace QAOrchestrator.Reporting;

public sealed class AnalysisReportWriter
{
    public string BuildConsoleSummary(TargetSolution solution)
    {
        var sourceCount = solution.Projects.Count(project => project.Kind == ProjectKind.Source);
        var testCount = solution.Projects.Count(project => project.Kind == ProjectKind.Test);

        var builder = new StringBuilder();
        builder.AppendLine($"QA Orchestrator analysis: {solution.Name}");
        builder.AppendLine($"Solution: {solution.SolutionPath}");
        builder.AppendLine($"Projects: {solution.Projects.Count} ({sourceCount} source, {testCount} test)");
        builder.AppendLine($"Primary architecture: {solution.Architecture.Primary.Style} ({solution.Architecture.Primary.Confidence}%)");

        builder.AppendLine("Test frameworks: " + JoinOrNone(GetTestFrameworks(solution)));
        builder.AppendLine("Assertion frameworks: " + JoinClassifiedTools(solution, TestPackageCategory.AssertionFramework));
        builder.AppendLine("Mock frameworks: " + JoinClassifiedTools(solution, TestPackageCategory.MockFramework));
        builder.AppendLine("Coverage tools: " + JoinClassifiedTools(solution, TestPackageCategory.CoverageTool));
        builder.AppendLine("HTTP clients: " + JoinClassifiedTools(solution, TestPackageCategory.HttpClientTool));

        var detectedPatterns = solution.Projects
            .Where(project => project.TestPattern is not null)
            .SelectMany(project => project.TestPattern!.NamingConventions.Select(convention => convention.Name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        builder.AppendLine("Detected test patterns: " + JoinOrNone(detectedPatterns));
        return builder.ToString();
    }

    public string WriteMarkdown(TargetSolution solution, string reportDirectory)
    {
        Directory.CreateDirectory(reportDirectory);
        var reportPath = Path.Combine(reportDirectory, "qa-orchestrator-analysis.md");
        File.WriteAllText(reportPath, BuildMarkdown(solution));
        return reportPath;
    }

    public string BuildMarkdown(TargetSolution solution)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# QA Orchestrator Analysis - {solution.Name}");
        builder.AppendLine();
        builder.AppendLine("## Summary");
        builder.AppendLine();
        builder.AppendLine($"- Solution: `{solution.SolutionPath}`");
        builder.AppendLine($"- Root: `{solution.RootDirectory}`");
        builder.AppendLine($"- Projects: {solution.Projects.Count}");
        builder.AppendLine($"- Source projects: {solution.Projects.Count(project => project.Kind == ProjectKind.Source)}");
        builder.AppendLine($"- Test projects: {solution.Projects.Count(project => project.Kind == ProjectKind.Test)}");
        builder.AppendLine($"- Primary architecture: **{solution.Architecture.Primary.Style}** ({solution.Architecture.Primary.Confidence}%)");
        builder.AppendLine($"- Reason: {solution.Architecture.Primary.Reason}");
        builder.AppendLine();

        builder.AppendLine("## Projects");
        builder.AppendLine();
        builder.AppendLine("| Project | Kind | Target frameworks | Test framework | Mock | Assertions | Test packages | Packages | Project references |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- | ---: | ---: |");
        foreach (var project in solution.Projects)
        {
            builder.AppendLine(
                $"| {Escape(project.Name)} | {project.Kind} | {Escape(JoinOrNone(project.TargetFrameworks))} | {project.TestFramework} | {project.MockFramework} | {project.AssertionFramework} | {Escape(JoinOrNone(project.TestPackageClassifications.Select(package => package.ToolName).Distinct(StringComparer.OrdinalIgnoreCase)))} | {project.PackageReferences.Count} | {project.ProjectReferences.Count} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Test Package Classification");
        builder.AppendLine();
        AppendClassifiedPackageSection(builder, solution, "Test frameworks", TestPackageCategory.TestFramework);
        AppendClassifiedPackageSection(builder, solution, "Assertion frameworks", TestPackageCategory.AssertionFramework);
        AppendClassifiedPackageSection(builder, solution, "Mock frameworks", TestPackageCategory.MockFramework);
        AppendClassifiedPackageSection(builder, solution, "Coverage tools", TestPackageCategory.CoverageTool);
        AppendClassifiedPackageSection(builder, solution, "Mutation tools", TestPackageCategory.MutationTool);
        AppendClassifiedPackageSection(builder, solution, "HTTP clients", TestPackageCategory.HttpClientTool);
        AppendClassifiedPackageSection(builder, solution, "Integration test tools", TestPackageCategory.IntegrationTestTool);
        AppendClassifiedPackageSection(builder, solution, "Functional test tools", TestPackageCategory.FunctionalTestTool);
        AppendClassifiedPackageSection(builder, solution, "Container tools", TestPackageCategory.ContainerTool);
        AppendClassifiedPackageSection(builder, solution, "Data generation tools", TestPackageCategory.DataGenerationTool);
        AppendClassifiedPackageSection(builder, solution, "Unknown test-related packages", TestPackageCategory.UnknownTestRelatedPackage);

        builder.AppendLine();
        builder.AppendLine("## Test Projects");
        builder.AppendLine();
        var testProjects = solution.Projects.Where(project => project.IsTestProject).ToArray();
        if (testProjects.Length == 0)
        {
            builder.AppendLine("No test projects detected.");
        }
        else
        {
            foreach (var project in testProjects)
            {
                builder.AppendLine($"### {project.Name}");
                builder.AppendLine();
                builder.AppendLine($"- Path: `{project.ProjectPath}`");
                builder.AppendLine($"- Framework: {project.TestFramework}");
                builder.AppendLine($"- Mock framework: {project.MockFramework}");
                builder.AppendLine($"- Assertion framework: {project.AssertionFramework}");
                builder.AppendLine($"- References: {JoinOrNone(project.ProjectReferences.Select(reference => reference.ProjectName ?? reference.Include))}");
                AppendTestPattern(builder, project.TestPattern);
                builder.AppendLine();
            }
        }

        builder.AppendLine("## Architecture Detection");
        builder.AppendLine();
        builder.AppendLine("| Detector | Confidence | Reason |");
        builder.AppendLine("| --- | ---: | --- |");
        foreach (var architecture in solution.Architecture.DetectedArchitectures)
        {
            builder.AppendLine($"| {architecture.Style} | {architecture.Confidence}% | {Escape(architecture.Reason)} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Next Steps");
        builder.AppendLine();
        builder.AppendLine("- Add Roslyn semantic analysis to map classes, methods, handlers, validators and endpoints.");
        builder.AppendLine("- Run coverage and map uncovered lines/branches into `CoverageGap`.");
        builder.AppendLine("- Generate conservative candidate tests under `tests/.qa-orchestrator/candidates`.");
        builder.AppendLine("- Validate candidates before accepting any generated test.");

        return builder.ToString();
    }

    private static string JoinOrNone(IEnumerable<string> values)
    {
        var materialized = values.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        return materialized.Length == 0 ? "none" : string.Join(", ", materialized);
    }

    private static IEnumerable<string> GetTestFrameworks(TargetSolution solution)
        => solution.Projects
            .Where(project => project.IsTestProject)
            .Select(project => project.TestFramework.ToString())
            .Where(framework => framework != TestFramework.Unknown.ToString())
            .Distinct(StringComparer.OrdinalIgnoreCase);

    private static string JoinClassifiedTools(TargetSolution solution, TestPackageCategory category)
        => JoinOrNone(solution.Projects
            .Where(project => project.IsTestProject)
            .SelectMany(project => project.TestPackageClassifications)
            .Where(classification => classification.Category == category)
            .Select(classification => classification.ToolName)
            .Distinct(StringComparer.OrdinalIgnoreCase));

    private static void AppendClassifiedPackageSection(StringBuilder builder, TargetSolution solution, string title, TestPackageCategory category)
    {
        var tools = solution.Projects
            .Where(project => project.IsTestProject)
            .SelectMany(project => project.TestPackageClassifications)
            .Where(classification => classification.Category == category)
            .Select(classification => string.IsNullOrWhiteSpace(classification.Version)
                ? classification.ToolName
                : $"{classification.ToolName} ({classification.Version})")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        builder.AppendLine($"- {title}: {JoinOrNone(tools)}");
    }

    private static void AppendTestPattern(StringBuilder builder, TestProjectPattern? pattern)
    {
        if (pattern is null)
        {
            builder.AppendLine("- Detected test patterns: none");
            return;
        }

        builder.AppendLine("- Detected test patterns:");
        builder.AppendLine($"  - Test classes: {pattern.TestClassCount}");
        builder.AppendLine($"  - Class suffixes: {JoinOrNone(pattern.Classes.Select(testClass => testClass.Suffix ?? string.Empty).Distinct(StringComparer.OrdinalIgnoreCase))}");
        builder.AppendLine($"  - Method naming: {JoinOrNone(pattern.NamingConventions.Select(convention => convention.Name))}");
        builder.AppendLine($"  - Attributes: {JoinOrNone(pattern.AttributeUsages.Select(attribute => attribute.Name))}");
        builder.AppendLine($"  - Common usings: {JoinOrNone(pattern.CommonUsings.Take(8).Select(usingInfo => usingInfo.Namespace))}");
        builder.AppendLine($"  - Mock usage: {DescribeMockUsage(pattern.CodePattern)}");
        builder.AppendLine($"  - Assertion style: {DescribeAssertionStyle(pattern.CodePattern)}");
        builder.AppendLine($"  - Integration style: {DescribeIntegrationStyle(pattern.CodePattern)}");
        builder.AppendLine($"  - Fixture style: {DescribeFixtureStyle(pattern.CodePattern)}");
    }

    private static string DescribeMockUsage(TestCodePattern pattern)
    {
        var values = new List<string>();
        if (pattern.UsesMoqMock)
        {
            values.Add("Moq Mock<T>");
        }

        if (pattern.UsesMoqSetup)
        {
            values.Add("Setup");
        }

        if (pattern.UsesMoqVerify)
        {
            values.Add("Verify");
        }

        return JoinOrNone(values);
    }

    private static string DescribeAssertionStyle(TestCodePattern pattern)
    {
        var values = new List<string>();
        if (pattern.UsesShouldAssertions)
        {
            values.Add("Should()");
        }

        if (pattern.UsesAwesomeAssertions)
        {
            values.Add("AwesomeAssertions");
        }

        return JoinOrNone(values);
    }

    private static string DescribeIntegrationStyle(TestCodePattern pattern)
    {
        var values = new List<string>();
        if (pattern.UsesWebApplicationFactory)
        {
            values.Add("WebApplicationFactory");
        }

        if (pattern.UsesHttpClient)
        {
            values.Add("HttpClient");
        }

        if (pattern.UsesRefitClients)
        {
            values.Add("Refit clients");
        }

        return JoinOrNone(values);
    }

    private static string DescribeFixtureStyle(TestCodePattern pattern)
    {
        var values = new List<string>();
        if (pattern.UsesFixture)
        {
            values.Add("Fixture");
        }

        if (pattern.UsesXUnitCollectionFixtures)
        {
            values.Add("IClassFixture/ICollectionFixture/Collection");
        }

        return JoinOrNone(values);
    }

    private static string Escape(string value)
        => value.Replace("|", "\\|", StringComparison.Ordinal);
}
