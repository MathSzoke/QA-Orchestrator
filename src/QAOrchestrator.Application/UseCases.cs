using QAOrchestrator.Domain;
using QAOrchestrator.DotNet;
using QAOrchestrator.Generation;
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

public sealed class CleanQaOrchestratorArtifactsUseCase
{
    private readonly CleanArtifactService _cleanArtifactService;

    public CleanQaOrchestratorArtifactsUseCase(CleanArtifactService cleanArtifactService)
    {
        _cleanArtifactService = cleanArtifactService;
    }

    public CleanPlan BuildPlan(string workingDirectory)
        => _cleanArtifactService.BuildPlan(Path.GetFullPath(workingDirectory));

    public CleanResult Execute(CleanPlan plan, bool dryRun)
        => _cleanArtifactService.Execute(plan, dryRun);
}

public sealed class BoostCoverageUseCase
{
    private readonly ConfigurationStore _configurationStore;
    private readonly DotNetSolutionReader _solutionReader;
    private readonly TestGenerationPlanner _planner;
    private readonly TestCandidateWriter _candidateWriter;
    private readonly BoostReportWriter _reportWriter;
    private readonly ProcessExecutor _processExecutor;

    public BoostCoverageUseCase(
        ConfigurationStore configurationStore,
        DotNetSolutionReader solutionReader,
        TestGenerationPlanner planner,
        TestCandidateWriter candidateWriter,
        BoostReportWriter reportWriter,
        ProcessExecutor processExecutor)
    {
        _configurationStore = configurationStore;
        _solutionReader = solutionReader;
        _planner = planner;
        _candidateWriter = candidateWriter;
        _reportWriter = reportWriter;
        _processExecutor = processExecutor;
    }

    public async Task<BoostReport> ExecuteAsync(BoostOptions options, CancellationToken cancellationToken = default)
    {
        var rootDirectory = Path.GetFullPath(options.WorkingDirectory);
        var config = _configurationStore.LoadOrDefault(rootDirectory);
        _configurationStore.EnsureWorkingFolders(rootDirectory, config);

        var solutionPath = ResolveSolutionPath(rootDirectory, options.Solution ?? config.Solution);
        var solution = _solutionReader.Read(solutionPath);
        var effectiveOptions = options.Targets.Count == 0
            ? options with { Targets = ParseTargets(config.Targets) }
            : options;

        var plan = _planner.CreateBoostPlan(solution, config, effectiveOptions);
        var generated = new List<BoostCandidate>();
        var accepted = new List<BoostCandidate>();
        var failed = new List<BoostCandidate>();

        if (!effectiveOptions.DryRun)
        {
            foreach (var candidate in plan.Candidates)
            {
                var written = _candidateWriter.Write(solution, candidate);
                generated.Add(written);

                if (effectiveOptions.NoValidation)
                {
                    continue;
                }

                var validation = await _processExecutor.ExecuteAsync("dotnet", $"test \"{solution.SolutionPath}\"", solution.RootDirectory, cancellationToken);
                if (validation.Success)
                {
                    accepted.Add(_candidateWriter.MoveToStatus(written, CandidateStatus.Accepted));
                }
                else
                {
                    var failure = string.IsNullOrWhiteSpace(validation.StandardError)
                        ? validation.StandardOutput
                        : validation.StandardError;
                    failed.Add(_candidateWriter.MoveToStatus(written, CandidateStatus.Failed, TrimFailure(failure)));
                }
            }
        }

        var reportDirectory = _configurationStore.ResolveConfiguredPath(rootDirectory, config.Generation.ReportFolder);
        var provisionalReport = new BoostReport(plan, generated, accepted, failed, string.Empty, string.Empty, string.Empty);
        var paths = _reportWriter.Write(provisionalReport, reportDirectory);
        var finalReport = provisionalReport with
        {
            MarkdownReportPath = paths.MarkdownPath,
            JsonReportPath = paths.JsonPath
        };

        return finalReport with { ConsoleSummary = _reportWriter.BuildConsoleSummary(finalReport) };
    }

    private static IReadOnlyList<BoostTarget> ParseTargets(IEnumerable<string> values)
        => values
            .SelectMany(value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(ParseTarget)
            .Where(target => target is not null)
            .Select(target => target!.Value)
            .Distinct()
            .ToArray();

    private static BoostTarget? ParseTarget(string value)
        => value.ToLowerInvariant() switch
        {
            "unit" => BoostTarget.Unit,
            "endpoint" or "endpoints" => BoostTarget.Endpoints,
            "integration" => BoostTarget.Integration,
            "functional" => BoostTarget.Functional,
            "mutation" => BoostTarget.Mutation,
            _ => null
        };

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

    private static string TrimFailure(string value)
        => value.Length <= 4000 ? value : value[..4000];
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
