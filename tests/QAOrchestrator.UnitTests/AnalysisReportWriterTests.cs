using FluentAssertions;
using QAOrchestrator.Domain;
using QAOrchestrator.Reporting;

namespace QAOrchestrator.UnitTests;

public sealed class AnalysisReportWriterTests
{
    [Fact]
    public void BuildMarkdown_IncludesArchitectureAndProjects()
    {
        var solution = new TargetSolution(
            "Sample",
            @"C:\repo\Sample.sln",
            @"C:\repo",
            [
                new TargetProject(
                    "Sample.Api",
                    @"C:\repo\src\Sample.Api\Sample.Api.csproj",
                    @"C:\repo\src\Sample.Api",
                    ProjectKind.Source,
                    ["net10.0"],
                    [],
                    [],
                    TestFramework.Unknown,
                    MockFramework.Unknown,
                    AssertionFramework.Unknown)
            ],
            new ArchitectureDetectionResult(
                new ProjectArchitecture(ArchitectureStyle.MinimalApi, 70, "found Minimal API mappings"),
                [new ProjectArchitecture(ArchitectureStyle.MinimalApi, 70, "found Minimal API mappings")]));

        var markdown = new AnalysisReportWriter().BuildMarkdown(solution);

        markdown.Should().Contain("# QA Orchestrator Analysis - Sample");
        markdown.Should().Contain("Sample.Api");
        markdown.Should().Contain("MinimalApi");
    }
}
