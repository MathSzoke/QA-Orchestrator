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

        var detectedTestFrameworks = solution.Projects
            .Where(project => project.IsTestProject)
            .Select(project => project.TestFramework)
            .Where(framework => framework != TestFramework.Unknown)
            .Distinct()
            .ToArray();

        builder.AppendLine("Test frameworks: " + (detectedTestFrameworks.Length == 0 ? "none detected" : string.Join(", ", detectedTestFrameworks)));
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
        builder.AppendLine("| Project | Kind | Target frameworks | Test framework | Mock | Assertions | Packages | Project references |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- | ---: | ---: |");
        foreach (var project in solution.Projects)
        {
            builder.AppendLine(
                $"| {Escape(project.Name)} | {project.Kind} | {Escape(JoinOrNone(project.TargetFrameworks))} | {project.TestFramework} | {project.MockFramework} | {project.AssertionFramework} | {project.PackageReferences.Count} | {project.ProjectReferences.Count} |");
        }

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

    private static string Escape(string value)
        => value.Replace("|", "\\|", StringComparison.Ordinal);
}
