using System.Text;
using System.Text.Json;
using QAOrchestrator.Domain;

namespace QAOrchestrator.Reporting;

public sealed class BoostReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public (string MarkdownPath, string JsonPath, string ConsoleSummary) Write(BoostReport report, string reportDirectory)
    {
        Directory.CreateDirectory(reportDirectory);
        var markdownPath = Path.Combine(reportDirectory, "qa-orchestrator-boost.md");
        var jsonPath = Path.Combine(reportDirectory, "qa-orchestrator-boost.json");
        File.WriteAllText(markdownPath, BuildMarkdown(report));
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(ToSerializable(report), JsonOptions));
        return (markdownPath, jsonPath, BuildConsoleSummary(report));
    }

    public string BuildConsoleSummary(BoostReport report)
    {
        var plan = report.Plan;
        var builder = new StringBuilder();
        builder.AppendLine("Boost report");
        builder.AppendLine($"Solution: {plan.Solution.SolutionPath}");
        builder.AppendLine($"Targets: {string.Join(", ", plan.Targets)}");
        builder.AppendLine($"Total source classes analyzed: {plan.SourceClasses.Count}");
        builder.AppendLine($"Testable classes found: {plan.SourceClasses.Count(source => source.IsUnitTestable)}");
        builder.AppendLine($"Candidates planned: {plan.Candidates.Count}");
        builder.AppendLine($"Candidates generated: {report.GeneratedCandidates.Count}");
        builder.AppendLine($"Candidates accepted: {report.AcceptedCandidates.Count}");
        builder.AppendLine($"Candidates failed: {report.FailedCandidates.Count}");
        builder.AppendLine($"Candidates skipped: {plan.SkippedCandidates.Count}");
        builder.AppendLine($"Markdown report: {report.MarkdownReportPath}");
        builder.AppendLine($"JSON report: {report.JsonReportPath}");
        return builder.ToString();
    }

    public string BuildMarkdown(BoostReport report)
    {
        var plan = report.Plan;
        var builder = new StringBuilder();
        builder.AppendLine("# QA Orchestrator Boost Report");
        builder.AppendLine();
        builder.AppendLine("## Solution");
        builder.AppendLine();
        builder.AppendLine($"- `{plan.Solution.SolutionPath}`");
        builder.AppendLine();
        builder.AppendLine("## Targets");
        builder.AppendLine();
        foreach (var target in plan.Targets)
        {
            builder.AppendLine($"- {target}");
        }

        builder.AppendLine();
        builder.AppendLine("## Detected Patterns");
        builder.AppendLine();
        builder.AppendLine($"- Test framework: {JoinOrNone(plan.Solution.Projects.Where(p => p.IsTestProject).Select(p => p.TestFramework.ToString()).Where(v => v != TestFramework.Unknown.ToString()).Distinct())}");
        builder.AppendLine($"- Mock framework: {JoinOrNone(plan.Solution.Projects.Where(p => p.IsTestProject).Select(p => p.MockFramework.ToString()).Where(v => v != MockFramework.Unknown.ToString()).Distinct())}");
        builder.AppendLine($"- Assertion framework: {JoinOrNone(plan.Solution.Projects.Where(p => p.IsTestProject).Select(p => p.AssertionFramework.ToString()).Where(v => v != AssertionFramework.Unknown.ToString()).Distinct())}");
        builder.AppendLine();
        builder.AppendLine("## Plan Summary");
        builder.AppendLine();
        builder.AppendLine($"- Total source classes analyzed: {plan.SourceClasses.Count}");
        builder.AppendLine($"- Total testable classes found: {plan.SourceClasses.Count(source => source.IsUnitTestable)}");
        builder.AppendLine($"- Existing exact tests found: {plan.Candidates.Count(candidate => candidate.ExistingTestMatch.ExactMatches.Count > 0)}");
        builder.AppendLine($"- Existing strong related tests found: {plan.Candidates.Count(candidate => candidate.ExistingTestMatch.StrongMatches.Count > 0)}");
        builder.AppendLine($"- Missing tests found: {plan.Candidates.Count(candidate => !candidate.ExistingTestMatch.HasExactOrStrongMatch)}");
        builder.AppendLine($"- Complementary tests planned: {plan.Candidates.Count(candidate => candidate.Location.IsComplementary)}");
        builder.AppendLine($"- Candidates planned: {plan.Candidates.Count}");
        builder.AppendLine($"- Candidates generated: {report.GeneratedCandidates.Count}");
        builder.AppendLine($"- Candidates accepted: {report.AcceptedCandidates.Count}");
        builder.AppendLine($"- Candidates failed: {report.FailedCandidates.Count}");
        builder.AppendLine($"- Candidates skipped: {plan.SkippedCandidates.Count}");
        builder.AppendLine();

        AppendMatches(builder, "Existing Tests Matched", plan.Candidates.Where(candidate => candidate.ExistingTestMatch.HasExactOrStrongMatch));
        AppendCandidates(builder, "Generated New Candidates", report.AcceptedCandidates.Concat(report.GeneratedCandidates).Where(candidate => !candidate.Location.IsComplementary));
        AppendCandidates(builder, "Generated Complementary Candidates", report.AcceptedCandidates.Concat(report.GeneratedCandidates).Where(candidate => candidate.Location.IsComplementary));
        AppendSkipped(builder, plan.SkippedCandidates);
        AppendDetails(builder, plan.Candidates.Concat(plan.SkippedCandidates));
        return builder.ToString();
    }

    private static void AppendMatches(StringBuilder builder, string title, IEnumerable<BoostCandidate> candidates)
    {
        builder.AppendLine($"## {title}");
        builder.AppendLine();
        var materialized = candidates.ToArray();
        if (materialized.Length == 0)
        {
            builder.AppendLine("None.");
            builder.AppendLine();
            return;
        }

        foreach (var candidate in materialized)
        {
            var matches = candidate.ExistingTestMatch.ExactMatches.Concat(candidate.ExistingTestMatch.StrongMatches)
                .Select(match => match.RelativePath)
                .Distinct(StringComparer.OrdinalIgnoreCase);
            builder.AppendLine($"- {candidate.SourceClass.ClassName} -> {string.Join(", ", matches)}");
        }

        builder.AppendLine();
    }

    private static void AppendCandidates(StringBuilder builder, string title, IEnumerable<BoostCandidate> candidates)
    {
        builder.AppendLine($"## {title}");
        builder.AppendLine();
        var materialized = candidates.ToArray();
        if (materialized.Length == 0)
        {
            builder.AppendLine("None.");
            builder.AppendLine();
            return;
        }

        foreach (var candidate in materialized)
        {
            builder.AppendLine($"- {candidate.CandidateName}");
        }

        builder.AppendLine();
    }

    private static void AppendSkipped(StringBuilder builder, IEnumerable<BoostCandidate> skipped)
    {
        builder.AppendLine("## Skipped");
        builder.AppendLine();
        var materialized = skipped.ToArray();
        if (materialized.Length == 0)
        {
            builder.AppendLine("None.");
            builder.AppendLine();
            return;
        }

        foreach (var candidate in materialized)
        {
            builder.AppendLine($"- {candidate.SourceClass.ClassName} skipped because {candidate.FailureReason}");
        }

        builder.AppendLine();
    }

    private static void AppendDetails(StringBuilder builder, IEnumerable<BoostCandidate> candidates)
    {
        builder.AppendLine("## Candidate Details");
        builder.AppendLine();
        foreach (var candidate in candidates)
        {
            builder.AppendLine($"### {candidate.SourceClass.ClassName}");
            builder.AppendLine();
            builder.AppendLine($"- Source: `{candidate.SourceClass.RelativePath}`");
            builder.AppendLine($"- Candidate: `{candidate.Location.CandidatePath}`");
            builder.AppendLine($"- Suggested final location: `{candidate.Location.SuggestedFinalPath}`");
            builder.AppendLine($"- Test type: {candidate.Location.TestType}");
            builder.AppendLine($"- Status: {candidate.Status}");
            builder.AppendLine($"- Confidence: {candidate.Location.Confidence}");
            builder.AppendLine($"- Reason: {candidate.Location.Reason}");
            if (candidate.Location.NeedsManualReview)
            {
                builder.AppendLine("- Manual review: required before choosing final placement");
            }
            if (!string.IsNullOrWhiteSpace(candidate.FailureReason))
            {
                builder.AppendLine($"- Failure reason: {candidate.FailureReason}");
            }
            builder.AppendLine();
        }
    }

    private static object ToSerializable(BoostReport report)
        => new
        {
            solution = report.Plan.Solution.SolutionPath,
            targets = report.Plan.Targets.Select(target => target.ToString()),
            summary = new
            {
                sourceClasses = report.Plan.SourceClasses.Count,
                testableClasses = report.Plan.SourceClasses.Count(source => source.IsUnitTestable),
                planned = report.Plan.Candidates.Count,
                generated = report.GeneratedCandidates.Count,
                accepted = report.AcceptedCandidates.Count,
                failed = report.FailedCandidates.Count,
                skipped = report.Plan.SkippedCandidates.Count
            },
            candidates = report.Plan.Candidates.Select(candidate => new
            {
                source = candidate.SourceClass.RelativePath,
                sourceClass = candidate.SourceClass.ClassName,
                candidate = candidate.Location.CandidatePath,
                suggestedFinalPath = candidate.Location.SuggestedFinalPath,
                status = candidate.Status.ToString(),
                confidence = candidate.Location.Confidence,
                reason = candidate.Location.Reason,
                existingExactTests = candidate.Location.ExistingExactTestFiles,
                existingSimilarTests = candidate.Location.ExistingSimilarTestFiles
            }),
            skipped = report.Plan.SkippedCandidates.Select(candidate => new
            {
                source = candidate.SourceClass.RelativePath,
                sourceClass = candidate.SourceClass.ClassName,
                reason = candidate.FailureReason
            })
        };

    private static string JoinOrNone(IEnumerable<string> values)
    {
        var materialized = values.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        return materialized.Length == 0 ? "none" : string.Join(", ", materialized);
    }
}
