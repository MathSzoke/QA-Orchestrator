using QAOrchestrator.Application;
using QAOrchestrator.DotNet;
using QAOrchestrator.Domain;
using QAOrchestrator.Generation;
using QAOrchestrator.Infrastructure;
using QAOrchestrator.Reporting;
using System.CommandLine;
using System.Reflection;

var services = new CliServices();
var root = new RootCommand("QA Orchestrator - conservative coverage booster for .NET solutions.");

root.Add(CreateInitCommand(services));
root.Add(CreateAnalyzeCommand(services));
root.Add(CreateCleanCommand(services));
root.Add(CreateCoverageCommand());
root.Add(CreateBoostCommand(services));
root.Add(CreateChangedCommand());
root.Add(CreateMutationCommand());
root.Add(CreateReportCommand(services));
root.Add(CreateVersionCommand());
root.Add(CreateDoctorCommand());

return await root.Parse(args).InvokeAsync();

static Command CreateInitCommand(CliServices services)
{
    var command = new Command("init", "Create qa-orchestrator.json and working folders.");
    command.SetAction(_ =>
    {
        var result = services.InitializeConfiguration.Execute(Environment.CurrentDirectory);
        return WriteExecutionResult(result);
    });

    return command;
}

static Command CreateAnalyzeCommand(CliServices services)
{
    var solutionOption = new Option<string?>("--solution", "-s")
    {
        Description = "Path to the target .sln file. If omitted, qa-orchestrator.json or the current directory is used."
    };

    var command = new Command("analyze", "Analyze solution projects, test frameworks and architecture.");
    command.Add(solutionOption);
    command.SetAction(parseResult =>
    {
        try
        {
            var result = services.AnalyzeSolution.Execute(
                Environment.CurrentDirectory,
                parseResult.GetValue(solutionOption));

            Console.WriteLine(result.ConsoleSummary);
            Console.WriteLine($"Markdown report: {result.ReportPath}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"analyze failed: {exception.Message}");
            return 1;
        }
    });

    return command;
}

static Command CreateCleanCommand(CliServices services)
{
    var dryRunOption = new Option<bool>("--dry-run")
    {
        Description = "Show QA Orchestrator artifacts that would be removed without deleting them."
    };

    var forceOption = new Option<bool>("--force")
    {
        Description = "Remove artifacts without asking for confirmation."
    };

    var command = new Command("clean", "Remove QA Orchestrator artifacts from the current target repository.");
    command.Add(dryRunOption);
    command.Add(forceOption);
    command.SetAction(parseResult =>
    {
        try
        {
            var dryRun = parseResult.GetValue(dryRunOption);
            var force = parseResult.GetValue(forceOption);
            var plan = services.CleanArtifacts.BuildPlan(Environment.CurrentDirectory);

            if (!plan.HasTargets)
            {
                Console.WriteLine("No QA Orchestrator artifacts were found in the current directory.");
                return 0;
            }

            WriteCleanPlan(plan, dryRun);

            if (dryRun)
            {
                Console.WriteLine();
                Console.WriteLine("No files were deleted.");
                return 0;
            }

            if (!force && !ConfirmClean())
            {
                Console.WriteLine("Clean cancelled. No files were deleted.");
                return 1;
            }

            var result = services.CleanArtifacts.Execute(plan, dryRun: false);
            Console.WriteLine();
            Console.WriteLine(result.Deleted
                ? $"Removed {result.RemovedPaths.Count} QA Orchestrator artifact(s)."
                : "No QA Orchestrator artifacts were removed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"clean failed: {exception.Message}");
            return 1;
        }
    });

    return command;
}

static Command CreateCoverageCommand()
{
    var command = new Command("coverage", "Run coverage analysis. Planned for the next implementation phase.");
    AddCommonSolutionOption(command);
    command.SetAction(_ => WriteExecutionResult(new RunCoverageUseCase().Execute()));
    return command;
}

static Command CreateBoostCommand(CliServices services)
{
    var solutionOption = new Option<string?>("--solution", "-s")
    {
        Description = "Path to the target .sln file."
    };

    var targetOption = new Option<string?>("--target", "-t")
    {
        Description = "Comma-separated target list. Initially supported: unit,endpoints."
    };

    var dryRunOption = new Option<bool>("--dry-run")
    {
        Description = "Build and report the boost plan without generating candidate files."
    };

    var maxCandidatesOption = new Option<int>("--max-candidates")
    {
        Description = "Maximum number of candidates generated in this run. Defaults to 10."
    };

    var includeExistingOption = new Option<bool>("--include-existing")
    {
        Description = "Include classes with existing tests in the report and complementary planning."
    };

    var noValidationOption = new Option<bool>("--no-validation")
    {
        Description = "Generate candidates without running dotnet test."
    };

    var command = new Command("boost", "Generate and validate conservative coverage-boosting test candidates.");
    command.Add(solutionOption);
    command.Add(targetOption);
    AddSafeOption(command);
    command.Add(dryRunOption);
    command.Add(maxCandidatesOption);
    command.Add(includeExistingOption);
    command.Add(noValidationOption);
    command.SetAction(async parseResult =>
    {
        try
        {
            var options = new BoostOptions(
                Environment.CurrentDirectory,
                parseResult.GetValue(solutionOption),
                ParseBoostTargets(parseResult.GetValue(targetOption)),
                SafeMode: true,
                DryRun: parseResult.GetValue(dryRunOption),
                MaxCandidates: parseResult.GetValue(maxCandidatesOption) <= 0 ? 10 : parseResult.GetValue(maxCandidatesOption),
                IncludeExisting: parseResult.GetValue(includeExistingOption),
                NoValidation: parseResult.GetValue(noValidationOption));

            var report = await services.BoostCoverage.ExecuteAsync(options);
            Console.WriteLine(report.ConsoleSummary);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"boost failed: {exception.Message}");
            return 1;
        }
    });
    return command;
}

static Command CreateChangedCommand()
{
    var baseOption = new Option<string?>("--base", "-b")
    {
        Description = "Base branch used to discover changed files."
    };

    var command = new Command("changed", "Analyze changed files and boost only impacted areas.");
    AddCommonSolutionOption(command);
    AddTargetOption(command);
    AddSafeOption(command);
    command.Add(baseOption);
    command.SetAction(_ => WriteExecutionResult(new AnalyzeChangedFilesUseCase().Execute()));
    return command;
}

static Command CreateMutationCommand()
{
    var command = new Command("mutation", "Run Stryker.NET mutation analysis and plan tests for survived mutants.");
    AddCommonSolutionOption(command);
    AddSafeOption(command);
    command.SetAction(_ => WriteExecutionResult(new RunMutationAnalysisUseCase().Execute()));
    return command;
}

static Command CreateReportCommand(CliServices services)
{
    var command = new Command("report", "Show the path to the latest generated report.");
    command.SetAction(_ => WriteExecutionResult(services.GenerateReport.Execute(Environment.CurrentDirectory)));
    return command;
}

static Command CreateVersionCommand()
{
    var command = new Command("version", "Show QA Orchestrator version and runtime location.");
    command.SetAction(_ =>
    {
        var assembly = typeof(CliServices).Assembly;
        var info = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";
        Console.WriteLine("Tool name: qa-orchestrator");
        Console.WriteLine($"Version: {assembly.GetName().Version}");
        Console.WriteLine($"Informational version: {info}");
        Console.WriteLine($"Assembly location: {assembly.Location}");
        Console.WriteLine($"Current directory: {Environment.CurrentDirectory}");
        return 0;
    });
    return command;
}

static Command CreateDoctorCommand()
{
    var command = new Command("doctor", "Show environment diagnostics and global tool update guidance.");
    command.SetAction(_ =>
    {
        var assembly = typeof(CliServices).Assembly;
        var assemblyLocation = assembly.Location;
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var dotnetTools = Path.Combine(userProfile, ".dotnet", "tools");
        var pathEntries = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var globalToolsInPath = pathEntries.Any(entry => string.Equals(Path.GetFullPath(entry), Path.GetFullPath(dotnetTools), StringComparison.OrdinalIgnoreCase));
        var looksGlobalTool = assemblyLocation.Contains($"{Path.DirectorySeparatorChar}.dotnet{Path.DirectorySeparatorChar}tools", StringComparison.OrdinalIgnoreCase)
            || assemblyLocation.Contains($"{Path.DirectorySeparatorChar}.store{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

        Console.WriteLine("QA Orchestrator doctor");
        Console.WriteLine($"Configuration in current directory: {(File.Exists(Path.Combine(Environment.CurrentDirectory, ConfigurationStore.FileName)) ? "found" : "not found")}");
        Console.WriteLine($"Execution mode: {(looksGlobalTool ? "global tool" : "dotnet run/local assembly")}");
        Console.WriteLine($"Assembly location: {assemblyLocation}");
        Console.WriteLine($"Version: {assembly.GetName().Version}");
        Console.WriteLine($".NET global tools path: {dotnetTools}");
        Console.WriteLine($"Global tools path in PATH: {(globalToolsInPath ? "yes" : "no")}");
        Console.WriteLine();
        Console.WriteLine("Update after git pull:");
        Console.WriteLine(@".\scripts\update-global-tool.ps1");
        Console.WriteLine();
        Console.WriteLine("Manual update:");
        Console.WriteLine(@"dotnet pack .\src\QAOrchestrator.Cli\QAOrchestrator.Cli.csproj -c Release");
        Console.WriteLine(@"dotnet tool update --global --add-source .\src\QAOrchestrator.Cli\nupkg QAOrchestrator.Cli");
        Console.WriteLine();
        Console.WriteLine("Manual reinstall fallback:");
        Console.WriteLine(@"dotnet tool uninstall --global QAOrchestrator.Cli");
        Console.WriteLine(@"dotnet tool install --global --add-source .\src\QAOrchestrator.Cli\nupkg QAOrchestrator.Cli");
        return 0;
    });
    return command;
}

static void AddCommonSolutionOption(Command command)
{
    command.Add(new Option<string?>("--solution", "-s")
    {
        Description = "Path to the target .sln file."
    });
}

static void AddTargetOption(Command command)
{
    command.Add(new Option<string?>("--target", "-t")
    {
        Description = "Comma-separated target list: unit,integration,functional,mutation."
    });
}

static IReadOnlyList<BoostTarget> ParseBoostTargets(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return [];
    }

    return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(target => target.ToLowerInvariant() switch
        {
            "unit" => (BoostTarget?)BoostTarget.Unit,
            "endpoint" or "endpoints" => BoostTarget.Endpoints,
            "integration" => BoostTarget.Integration,
            "functional" => BoostTarget.Functional,
            "mutation" => BoostTarget.Mutation,
            _ => null
        })
        .Where(target => target is not null)
        .Select(target => target!.Value)
        .Distinct()
        .ToArray();
}

static void AddSafeOption(Command command)
{
    command.Add(new Option<bool>("--safe")
    {
        Description = "Run in safe mode. Existing passing tests are preserved."
    });
}

static int WriteExecutionResult(ExecutionResult result)
{
    var writer = result.Success ? Console.Out : Console.Error;
    writer.WriteLine(result.Message);
    return result.Success ? 0 : 1;
}

static void WriteCleanPlan(CleanPlan plan, bool dryRun)
{
    Console.WriteLine(dryRun ? "QA Orchestrator clean dry-run:" : "QA Orchestrator clean plan:");
    Console.WriteLine();
    Console.WriteLine(dryRun ? "Would remove:" : "Will remove:");
    foreach (var target in plan.Targets)
    {
        Console.WriteLine($"- {target.RelativePath}");
    }

    if (!dryRun)
    {
        Console.WriteLine();
        Console.WriteLine("Use --dry-run to preview without deleting.");
        Console.WriteLine("Use --force to skip confirmation.");
    }
}

static bool ConfirmClean()
{
    Console.WriteLine();
    Console.Write("Remove these QA Orchestrator artifacts? Type 'yes' to continue: ");
    var answer = Console.ReadLine();
    return string.Equals(answer, "yes", StringComparison.OrdinalIgnoreCase);
}

internal sealed class CliServices
{
    private readonly ConfigurationStore _configurationStore = new();

    public InitializeConfigurationUseCase InitializeConfiguration { get; }

    public AnalyzeSolutionUseCase AnalyzeSolution { get; }

    public GenerateReportUseCase GenerateReport { get; }

    public CleanQaOrchestratorArtifactsUseCase CleanArtifacts { get; }

    public BoostCoverageUseCase BoostCoverage { get; }

    public CliServices()
    {
        InitializeConfiguration = new InitializeConfigurationUseCase(_configurationStore);
        AnalyzeSolution = new AnalyzeSolutionUseCase(
            _configurationStore,
            new DotNetSolutionReader(),
            new AnalysisReportWriter());
        GenerateReport = new GenerateReportUseCase(_configurationStore);
        CleanArtifacts = new CleanQaOrchestratorArtifactsUseCase(new CleanArtifactService());
        BoostCoverage = new BoostCoverageUseCase(
            _configurationStore,
            new DotNetSolutionReader(),
            new TestGenerationPlanner(),
            new TestCandidateWriter(),
            new BoostReportWriter(),
            new ProcessExecutor());
    }
}
