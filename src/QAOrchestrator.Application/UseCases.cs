using QAOrchestrator.Domain;
using QAOrchestrator.DotNet;
using QAOrchestrator.Infrastructure;
using QAOrchestrator.Reporting;

namespace QAOrchestrator.Application;

public sealed class InitializeConfigurationUseCase
{
    private readonly ConfigurationStore _configurationStore;

    public InitializeConfigurationUseCase(ConfigurationStore configurationStore)
    {
        _configurationStore = configurationStore;
    }

    public ExecutionResult Execute(string workingDirectory)
    {
        var rootDirectory = Path.GetFullPath(workingDirectory);
        if (_configurationStore.Exists(rootDirectory))
        {
            return ExecutionResult.Fail($"Configuration already exists: {_configurationStore.GetConfigurationPath(rootDirectory)}");
        }

        var path = _configurationStore.CreateDefault(rootDirectory);
        return ExecutionResult.Ok($"Created {path}");
    }
}

public sealed class AnalyzeSolutionUseCase
{
    private readonly ConfigurationStore _configurationStore;
    private readonly DotNetSolutionReader _solutionReader;
    private readonly AnalysisReportWriter _reportWriter;

    public AnalyzeSolutionUseCase(
        ConfigurationStore configurationStore,
        DotNetSolutionReader solutionReader,
        AnalysisReportWriter reportWriter)
    {
        _configurationStore = configurationStore;
        _solutionReader = solutionReader;
        _reportWriter = reportWriter;
    }

    public AnalyzeSolutionResult Execute(string workingDirectory, string? solution)
    {
        var rootDirectory = Path.GetFullPath(workingDirectory);
        var config = _configurationStore.LoadOrDefault(rootDirectory);
        _configurationStore.EnsureWorkingFolders(rootDirectory, config);

        var solutionPath = ResolveSolutionPath(rootDirectory, solution ?? config.Solution);
        var targetSolution = _solutionReader.Read(solutionPath);
        var reportDirectory = _configurationStore.ResolveConfiguredPath(rootDirectory, config.Generation.ReportFolder);
        var reportPath = _reportWriter.WriteMarkdown(targetSolution, reportDirectory);
        var consoleSummary = _reportWriter.BuildConsoleSummary(targetSolution);

        return new AnalyzeSolutionResult(targetSolution, reportPath, consoleSummary);
    }

    private static string ResolveSolutionPath(string rootDirectory, string? solution)
    {
        if (!string.IsNullOrWhiteSpace(solution))
        {
            return Path.IsPathRooted(solution)
                ? solution
                : Path.Combine(rootDirectory, solution);
        }

        var solutions = Directory.EnumerateFiles(rootDirectory, "*.sln", SearchOption.TopDirectoryOnly).ToArray();
        return solutions.Length switch
        {
            1 => solutions[0],
            0 => throw new FileNotFoundException("No .sln file was found. Pass --solution <file.sln>."),
            _ => throw new InvalidOperationException("More than one .sln file was found. Pass --solution <file.sln>.")
        };
    }
}

public sealed record AnalyzeSolutionResult(TargetSolution Solution, string ReportPath, string ConsoleSummary);

public sealed class BoostCoverageUseCase
{
    public ExecutionResult Execute()
        => ExecutionResult.Fail("boost is planned for the next implementation phase. The current foundation supports init and analyze.");
}

public sealed class AnalyzeChangedFilesUseCase
{
    public ExecutionResult Execute()
        => ExecutionResult.Fail("changed is planned for the next implementation phase. The current foundation supports init and analyze.");
}

public sealed class RunCoverageUseCase
{
    public ExecutionResult Execute()
        => ExecutionResult.Fail("coverage is planned for the next implementation phase. The current foundation supports init and analyze.");
}

public sealed class RunMutationAnalysisUseCase
{
    public ExecutionResult Execute()
        => ExecutionResult.Fail("mutation is planned for the next implementation phase. The current foundation supports init and analyze.");
}

public sealed class GenerateReportUseCase
{
    private readonly ConfigurationStore _configurationStore;

    public GenerateReportUseCase(ConfigurationStore configurationStore)
    {
        _configurationStore = configurationStore;
    }

    public ExecutionResult Execute(string workingDirectory)
    {
        var rootDirectory = Path.GetFullPath(workingDirectory);
        var config = _configurationStore.LoadOrDefault(rootDirectory);
        var reportPath = Path.Combine(
            _configurationStore.ResolveConfiguredPath(rootDirectory, config.Generation.ReportFolder),
            "qa-orchestrator-analysis.md");

        return File.Exists(reportPath)
            ? ExecutionResult.Ok(reportPath)
            : ExecutionResult.Fail($"No analysis report found at {reportPath}. Run qa-orchestrator analyze first.");
    }
}
