using QAOrchestrator.Application;
using QAOrchestrator.DotNet;
using QAOrchestrator.Domain;
using QAOrchestrator.Infrastructure;
using QAOrchestrator.Reporting;
using System.CommandLine;

var services = new CliServices();
var root = new RootCommand("QA Orchestrator - conservative coverage booster for .NET solutions.");

root.Add(CreateInitCommand(services));
root.Add(CreateAnalyzeCommand(services));
root.Add(CreateCoverageCommand());
root.Add(CreateBoostCommand());
root.Add(CreateChangedCommand());
root.Add(CreateMutationCommand());
root.Add(CreateReportCommand(services));

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

static Command CreateCoverageCommand()
{
    var command = new Command("coverage", "Run coverage analysis. Planned for the next implementation phase.");
    AddCommonSolutionOption(command);
    command.SetAction(_ => WriteExecutionResult(new RunCoverageUseCase().Execute()));
    return command;
}

static Command CreateBoostCommand()
{
    var command = new Command("boost", "Generate and validate conservative coverage-boosting test candidates.");
    AddCommonSolutionOption(command);
    AddTargetOption(command);
    AddSafeOption(command);
    command.SetAction(_ => WriteExecutionResult(new BoostCoverageUseCase().Execute()));
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

internal sealed class CliServices
{
    private readonly ConfigurationStore _configurationStore = new();

    public InitializeConfigurationUseCase InitializeConfiguration { get; }

    public AnalyzeSolutionUseCase AnalyzeSolution { get; }

    public GenerateReportUseCase GenerateReport { get; }

    public CliServices()
    {
        InitializeConfiguration = new InitializeConfigurationUseCase(_configurationStore);
        AnalyzeSolution = new AnalyzeSolutionUseCase(
            _configurationStore,
            new DotNetSolutionReader(),
            new AnalysisReportWriter());
        GenerateReport = new GenerateReportUseCase(_configurationStore);
    }
}
