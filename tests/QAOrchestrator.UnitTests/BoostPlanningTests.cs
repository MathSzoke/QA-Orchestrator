using FluentAssertions;
using QAOrchestrator.Application;
using QAOrchestrator.Domain;
using QAOrchestrator.DotNet;
using QAOrchestrator.Generation;
using QAOrchestrator.Infrastructure;
using QAOrchestrator.Reporting;

namespace QAOrchestrator.UnitTests;

public sealed class BoostPlanningTests
{
    [Fact]
    public void ExistingTestMatcher_FindsExactMatch()
    {
        using var temp = TempDirectory.Create();
        var solution = CreateSolution(temp.Path, "App", "App.Tests");
        WriteFile(Path.Combine(temp.Path, "tests", "App.Tests", "GetUserHandlerTests.cs"), "public sealed class GetUserHandlerTests { }");
        var source = Source("App", temp.Path, "src/App/Features/GetUser/GetUserHandler.cs", "GetUserHandler");

        var result = new ExistingTestMatcher().Match(solution, source);

        result.ExactMatches.Should().ContainSingle();
        result.HasExactOrStrongMatch.Should().BeTrue();
    }

    [Fact]
    public void ExistingTestMatcher_FindsStrongMatchBySourceReference()
    {
        using var temp = TempDirectory.Create();
        var solution = CreateSolution(temp.Path, "App", "App.Tests");
        WriteFile(Path.Combine(temp.Path, "tests", "App.Tests", "GetUserFeatureTests.cs"), "var handler = typeof(GetUserHandler);");
        var source = Source("App", temp.Path, "src/App/Features/GetUser/GetUserHandler.cs", "GetUserHandler");

        var result = new ExistingTestMatcher().Match(solution, source);

        result.StrongMatches.Should().ContainSingle();
        result.HasExactOrStrongMatch.Should().BeTrue();
    }

    [Fact]
    public void ExistingTestMatcher_FindsWeakMatchByFeatureFolder()
    {
        using var temp = TempDirectory.Create();
        var solution = CreateSolution(temp.Path, "App", "App.Tests");
        WriteFile(Path.Combine(temp.Path, "tests", "App.Tests", "Features", "GetUser", "ScenarioTests.cs"), "public sealed class ScenarioTests { }");
        var source = Source("App", temp.Path, "src/App/Features/GetUser/GetUserHandler.cs", "GetUserHandler");

        var result = new ExistingTestMatcher().Match(solution, source);

        result.WeakMatches.Should().ContainSingle();
        result.HasExactOrStrongMatch.Should().BeFalse();
    }

    [Fact]
    public void ExistingTestMatcher_ReturnsNoMatch()
    {
        using var temp = TempDirectory.Create();
        var solution = CreateSolution(temp.Path, "App", "App.Tests");
        WriteFile(Path.Combine(temp.Path, "tests", "App.Tests", "OtherTests.cs"), "public sealed class OtherTests { }");
        var source = Source("App", temp.Path, "src/App/Features/GetUser/GetUserHandler.cs", "GetUserHandler");

        var result = new ExistingTestMatcher().Match(solution, source);

        result.ExactMatches.Should().BeEmpty();
        result.StrongMatches.Should().BeEmpty();
        result.WeakMatches.Should().BeEmpty();
    }

    [Fact]
    public void TestLocationResolver_UsesExistingSameFeatureTest()
    {
        using var temp = TempDirectory.Create();
        var solution = CreateSolution(temp.Path, "App", "App.Tests");
        var existing = Path.Combine(temp.Path, "tests", "App.Tests", "Features", "GetUser", "ScenarioTests.cs");
        WriteFile(existing, "public sealed class GetUserScenarioTests { }");
        var source = Source("App", temp.Path, "src/App/Features/GetUser/GetUserHandler.cs", "GetUserHandler");
        var match = new ExistingTestMatcher().Match(solution, source);

        var resolution = new TestLocationResolver().Resolve(solution, QaOrchestratorConfig.CreateDefault(), source, match, TestType.Unit);

        resolution.SuggestedFinalPath.Should().StartWith(Path.GetDirectoryName(existing)!);
        resolution.CandidatePath.Should().Contain(Path.Combine("tests", ".qa-orchestrator", "candidates", "pending", "unit"));
        resolution.NeedsManualReview.Should().BeFalse();
    }

    [Fact]
    public void TestLocationResolver_UsesSourceAndTestProjectNamingMatch()
    {
        using var temp = TempDirectory.Create();
        var solution = CreateSolution(temp.Path, "App", "App.Tests");
        var source = Source("App", temp.Path, "src/App/Features/GetUser/GetUserHandler.cs", "GetUserHandler");
        var match = new ExistingTestMatchResult(source, [], [], [], 0, "No related existing tests found.");

        var resolution = new TestLocationResolver().Resolve(solution, QaOrchestratorConfig.CreateDefault(), source, match, TestType.Unit);

        resolution.MatchingTestProject.Should().Be("App.Tests");
        resolution.SuggestedFinalPath.Should().Contain("App.Tests");
        resolution.SuggestedFinalPath.Should().EndWith("GetUserHandlerCoverageTests.cs");
    }

    [Fact]
    public void TestLocationResolver_FallsBackToCandidatesWhenNoTestProjectExists()
    {
        using var temp = TempDirectory.Create();
        var solution = CreateSolution(temp.Path, "App", testProjectName: null);
        var source = Source("App", temp.Path, "src/App/Features/GetUser/GetUserHandler.cs", "GetUserHandler");
        var match = new ExistingTestMatchResult(source, [], [], [], 0, "No related existing tests found.");

        var resolution = new TestLocationResolver().Resolve(solution, QaOrchestratorConfig.CreateDefault(), source, match, TestType.Unit);

        resolution.NeedsManualReview.Should().BeTrue();
        resolution.SuggestedFinalPath.Should().Be(resolution.CandidatePath);
    }

    [Fact]
    public void EndpointUnitTestabilityDetector_DetectsUnitTestableAndInlineEndpoint()
    {
        var detector = new EndpointUnitTestabilityDetector();
        var unitTestable = new TestableSourceClass("Api", "Endpoint.cs", "Endpoint.cs", "Api", "GetEndpoint", TestableSourceKind.Endpoint, ["HandleAsync"], [], true, "Endpoint has an isolated handler method.");
        var inline = new TestableSourceClass("Api", "Endpoint.cs", "Endpoint.cs", "Api", "GetEndpoint", TestableSourceKind.Endpoint, [], [], false, "Unit test skipped: endpoint handler is inline and should be covered by integration test.");

        detector.IsUnitTestable(unitTestable).Should().BeTrue();
        detector.RequiresIntegration(inline).Should().BeTrue();
    }

    [Fact]
    public async Task Boost_DryRun_DoesNotCreateCandidateFiles()
    {
        using var temp = TempDirectory.Create();
        WriteFile(Path.Combine(temp.Path, "src", "App", "GetUserHandler.cs"), """
            namespace App;
            public sealed class GetUserHandler
            {
                public string Handle() => "ok";
            }
            """);
        WriteFile(Path.Combine(temp.Path, "src", "App", "App.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        WriteFile(Path.Combine(temp.Path, "tests", "App.Tests", "App.Tests.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <ItemGroup>
                <PackageReference Include="xunit" Version="2.9.3" />
                <PackageReference Include="AwesomeAssertions" Version="9.0.0" />
                <PackageReference Include="Moq" Version="4.20.72" />
                <ProjectReference Include="..\..\src\App\App.csproj" />
              </ItemGroup>
            </Project>
            """);
        WriteFile(Path.Combine(temp.Path, "App.sln"), """
            Microsoft Visual Studio Solution File, Format Version 12.00
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "App", "src\App\App.csproj", "{11111111-1111-1111-1111-111111111111}"
            EndProject
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "App.Tests", "tests\App.Tests\App.Tests.csproj", "{22222222-2222-2222-2222-222222222222}"
            EndProject
            Global
            EndGlobal
            """);

        var useCase = new BoostCoverageUseCase(
            new ConfigurationStore(),
            new DotNetSolutionReader(),
            new TestGenerationPlanner(),
            new TestCandidateWriter(),
            new BoostReportWriter(),
            new ProcessExecutor());

        var report = await useCase.ExecuteAsync(new BoostOptions(temp.Path, "App.sln", [BoostTarget.Unit], true, true, 10, false, true));

        report.Plan.Candidates.Should().NotBeEmpty();
        Directory.EnumerateFiles(temp.Path, "*CoverageTests.cs", SearchOption.AllDirectories).Should().BeEmpty();
        File.Exists(report.MarkdownReportPath).Should().BeTrue();
    }

    [Fact]
    public void BoostReportWriter_GeneratesMarkdown()
    {
        using var temp = TempDirectory.Create();
        var solution = CreateSolution(temp.Path, "App", "App.Tests");
        var source = Source("App", temp.Path, "src/App/Features/GetUser/GetUserHandler.cs", "GetUserHandler");
        var match = new ExistingTestMatchResult(source, [], [], [], 0, "No related existing tests found.");
        var location = new TestLocationResolution(TestType.Unit, "App.Tests", [], [], Path.Combine(temp.Path, "tests", "App.Tests", "GetUserHandlerCoverageTests.cs"), Path.Combine(temp.Path, "tests", ".qa-orchestrator", "candidates", "pending", "unit", "GetUserHandlerCoverageTests.cs"), 55, "Matched test project.", false, false);
        var candidate = new BoostCandidate(source, match, location, CandidateStatus.Planned, "GetUserHandlerCoverageTests.cs", "No existing test found.", null);
        var plan = new BoostPlan(solution, [BoostTarget.Unit], [source], [candidate], []);
        var report = new BoostReport(plan, [], [], [], "", "", "");

        var markdown = new BoostReportWriter().BuildMarkdown(report);

        markdown.Should().Contain("Boost Report");
        markdown.Should().Contain("GetUserHandlerCoverageTests.cs");
    }

    private static TargetSolution CreateSolution(string root, string sourceProjectName, string? testProjectName)
    {
        var sourceProject = Project(sourceProjectName, Path.Combine(root, "src", sourceProjectName, $"{sourceProjectName}.csproj"), ProjectKind.Source, [], null);
        var projects = new List<TargetProject> { sourceProject };
        if (testProjectName is not null)
        {
            projects.Add(Project(testProjectName, Path.Combine(root, "tests", testProjectName, $"{testProjectName}.csproj"), ProjectKind.Test, [new ProjectReferenceInfo(@"..\..\src\App\App.csproj", sourceProjectName)], new TestProjectPattern(testProjectName, 0, [], [], [], [], [], new TestCodePattern(false, false, false, false, false, false, false, false, false, false, false), [])));
            Directory.CreateDirectory(Path.Combine(root, "tests", testProjectName));
        }

        Directory.CreateDirectory(Path.Combine(root, "src", sourceProjectName));
        return new TargetSolution("App", Path.Combine(root, "App.sln"), root, projects, new ArchitectureDetectionResult(new ProjectArchitecture(ArchitectureStyle.GenericDotNet, 100, "fallback"), []));
    }

    private static TargetProject Project(string name, string path, ProjectKind kind, IReadOnlyList<ProjectReferenceInfo> refs, TestProjectPattern? pattern)
        => new(name, path, Path.GetDirectoryName(path)!, kind, ["net10.0"], [], refs, kind == ProjectKind.Test ? TestFramework.XUnit : TestFramework.Unknown, MockFramework.Moq, AssertionFramework.AwesomeAssertions, [], pattern);

    private static TestableSourceClass Source(string projectName, string root, string relativePath, string className)
        => new(projectName, Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)), relativePath.Replace('/', Path.DirectorySeparatorChar), "App.Features", className, TestableSourceKind.Handler, ["Handle"], [], true, "Detected testable Handler.");

    private static void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
