$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$projectPath = Join-Path $repoRoot "src/QAOrchestrator.Cli/QAOrchestrator.Cli.csproj"
$packageSource = Join-Path $repoRoot "src/QAOrchestrator.Cli/nupkg"

dotnet pack $projectPath -c Release
dotnet tool install --global --add-source $packageSource QAOrchestrator.Cli
qa-orchestrator --help
