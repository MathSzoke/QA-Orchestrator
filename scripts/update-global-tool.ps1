$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$projectPath = Join-Path $repoRoot "src/QAOrchestrator.Cli/QAOrchestrator.Cli.csproj"
$packageSource = Join-Path $repoRoot "src/QAOrchestrator.Cli/nupkg"

dotnet pack $projectPath -c Release

try {
    dotnet tool update --global --add-source $packageSource QAOrchestrator.Cli
}
catch {
    Write-Host "Tool update failed. Trying install instead..."
    dotnet tool install --global --add-source $packageSource QAOrchestrator.Cli
}

qa-orchestrator --help
