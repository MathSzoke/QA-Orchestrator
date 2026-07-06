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
                    ProjectKind.Test,
                    ["net10.0"],
                    [
                        new PackageReferenceInfo("AwesomeAssertions", "9.0.0"),
                        new PackageReferenceInfo("Moq", "4.20.72"),
                        new PackageReferenceInfo("coverlet.collector", "6.0.4"),
                        new PackageReferenceInfo("Refit.HttpClientFactory", "8.0.0")
                    ],
                    [],
                    TestFramework.Unknown,
                    MockFramework.Moq,
                    AssertionFramework.AwesomeAssertions,
                    [
                        new TestPackageClassification("AwesomeAssertions", "9.0.0", TestPackageCategory.AssertionFramework, "AwesomeAssertions"),
                        new TestPackageClassification("Moq", "4.20.72", TestPackageCategory.MockFramework, "Moq"),
                        new TestPackageClassification("coverlet.collector", "6.0.4", TestPackageCategory.CoverageTool, "coverlet.collector"),
                        new TestPackageClassification("Refit.HttpClientFactory", "8.0.0", TestPackageCategory.HttpClientTool, "Refit.HttpClientFactory")
                    ],
                    new TestProjectPattern(
                        "Sample.Api.Tests",
                        1,
                        [new TestClassPattern("SampleApiTests", "Tests", @"C:\repo\tests\SampleApiTests.cs")],
                        [new TestMethodPattern("Should_ReturnOk_When_RequestIsValid", "Should_ExpectedBehavior_When_State", @"C:\repo\tests\SampleApiTests.cs")],
                        [new TestAttributeUsage("Fact", 1)],
                        [new DetectedUsing("AwesomeAssertions", 1), new DetectedUsing("Moq", 1)],
                        [new TestNamingConvention("Should_ExpectedBehavior_When_State", 1)],
                        new TestCodePattern(true, true, true, true, true, true, true, true, true, true, true),
                        [new TestDependencyUsage("Moq Mock<T>", 1)]))
            ],
            new ArchitectureDetectionResult(
                new ProjectArchitecture(ArchitectureStyle.MinimalApi, 70, "found Minimal API mappings"),
                [new ProjectArchitecture(ArchitectureStyle.MinimalApi, 70, "found Minimal API mappings")]));

        var markdown = new AnalysisReportWriter().BuildMarkdown(solution);

        markdown.Should().Contain("# QA Orchestrator Analysis - Sample");
        markdown.Should().Contain("Sample.Api");
        markdown.Should().Contain("MinimalApi");
        markdown.Should().Contain("AwesomeAssertions");
        markdown.Should().Contain("Moq");
        markdown.Should().Contain("coverlet.collector");
        markdown.Should().Contain("Refit.HttpClientFactory");
        markdown.Should().Contain("Should_ExpectedBehavior_When_State");
    }
}
