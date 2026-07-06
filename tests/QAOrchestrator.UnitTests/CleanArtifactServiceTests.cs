using FluentAssertions;
using QAOrchestrator.Infrastructure;

namespace QAOrchestrator.UnitTests;

public sealed class CleanArtifactServiceTests
{
    [Fact]
    public void BuildPlan_FindsOnlyQaOrchestratorArtifacts()
    {
        using var temp = TempDirectory.Create();
        File.WriteAllText(Path.Combine(temp.Path, "qa-orchestrator.json"), "{}");
        File.WriteAllText(Path.Combine(temp.Path, "App.sln"), "");
        Directory.CreateDirectory(Path.Combine(temp.Path, "src"));
        File.WriteAllText(Path.Combine(temp.Path, "src", "App.csproj"), "");
        Directory.CreateDirectory(Path.Combine(temp.Path, "tests", ".qa-orchestrator", "reports"));
        File.WriteAllText(Path.Combine(temp.Path, "tests", ".qa-orchestrator", "reports", "qa-orchestrator-analysis.md"), "");
        File.WriteAllText(Path.Combine(temp.Path, "qa-orchestrator-summary.html"), "");

        var plan = new CleanArtifactService().BuildPlan(temp.Path);

        plan.Targets.Select(target => target.RelativePath.Replace('\\', '/')).Should().Contain("qa-orchestrator.json");
        plan.Targets.Select(target => target.RelativePath.Replace('\\', '/')).Should().Contain("tests/.qa-orchestrator");
        plan.Targets.Select(target => target.RelativePath.Replace('\\', '/')).Should().Contain("qa-orchestrator-summary.html");
        plan.Targets.Should().NotContain(target => target.RelativePath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase));
        plan.Targets.Should().NotContain(target => target.RelativePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Execute_DryRunDoesNotDeleteArtifacts()
    {
        using var temp = TempDirectory.Create();
        File.WriteAllText(Path.Combine(temp.Path, "qa-orchestrator.json"), "{}");
        var service = new CleanArtifactService();
        var plan = service.BuildPlan(temp.Path);

        var result = service.Execute(plan, dryRun: true);

        result.Deleted.Should().BeFalse();
        File.Exists(Path.Combine(temp.Path, "qa-orchestrator.json")).Should().BeTrue();
    }

    [Fact]
    public void Execute_RemovesQaArtifactsAndKeepsProjectFiles()
    {
        using var temp = TempDirectory.Create();
        File.WriteAllText(Path.Combine(temp.Path, "qa-orchestrator.json"), "{}");
        File.WriteAllText(Path.Combine(temp.Path, "App.sln"), "");
        Directory.CreateDirectory(Path.Combine(temp.Path, "tests", ".qa-orchestrator", "candidates"));
        File.WriteAllText(Path.Combine(temp.Path, "tests", ".qa-orchestrator", "candidates", "CandidateTests.cs"), "");

        var service = new CleanArtifactService();
        var plan = service.BuildPlan(temp.Path);
        var result = service.Execute(plan, dryRun: false);

        result.Deleted.Should().BeTrue();
        File.Exists(Path.Combine(temp.Path, "qa-orchestrator.json")).Should().BeFalse();
        Directory.Exists(Path.Combine(temp.Path, "tests", ".qa-orchestrator")).Should().BeFalse();
        File.Exists(Path.Combine(temp.Path, "App.sln")).Should().BeTrue();
    }
}
