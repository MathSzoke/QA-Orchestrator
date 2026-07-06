using FluentAssertions;
using QAOrchestrator.Application;
using QAOrchestrator.DotNet;
using QAOrchestrator.Infrastructure;
using QAOrchestrator.Reporting;

namespace QAOrchestrator.IntegrationTests;

public sealed class AnalyzeSolutionUseCaseTests
{
    [Fact]
    public void Execute_ReadsSolutionAndWritesMarkdownReport()
    {
        using var temp = TempDirectory.Create();
        var projectPath = Path.Combine(temp.Path, "src", "App", "App.csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);
        File.WriteAllText(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var solutionPath = Path.Combine(temp.Path, "App.sln");
        File.WriteAllText(solutionPath, $$"""
            Microsoft Visual Studio Solution File, Format Version 12.00
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "App", "src\App\App.csproj", "{11111111-1111-1111-1111-111111111111}"
            EndProject
            Global
            EndGlobal
            """);

        var useCase = new AnalyzeSolutionUseCase(
            new ConfigurationStore(),
            new DotNetSolutionReader(),
            new AnalysisReportWriter());

        var result = useCase.Execute(temp.Path, "App.sln");

        File.Exists(result.ReportPath).Should().BeTrue();
        File.ReadAllText(result.ReportPath).Should().Contain("App");
        result.ConsoleSummary.Should().Contain("Projects: 1");
    }
}

internal sealed class TempDirectory : IDisposable
{
    private TempDirectory(string path)
    {
        Path = path;
    }

    public string Path { get; }

    public static TempDirectory Create()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"qa-orchestrator-integration-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return new TempDirectory(path);
    }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
