$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$projectPath = Join-Path $repoRoot "src/QAOrchestrator.Cli/QAOrchestrator.Cli.csproj"
$packageSource = Join-Path $repoRoot "src/QAOrchestrator.Cli/nupkg"

if (Test-Path $packageSource) {
    Remove-Item -LiteralPath $packageSource -Recurse -Force
}

dotnet pack $projectPath -c Release

$installedTools = dotnet tool list --global
if ($installedTools -match "qaorchestrator\.cli") {
    dotnet tool uninstall --global qaorchestrator.cli
}
else {
    Write-Host "QAOrchestrator.Cli was not installed globally. Continuing with install..."
}

dotnet tool install --global --add-source $packageSource QAOrchestrator.Cli

qa-orchestrator --help
qa-orchestrator doctor
qa-orchestrator boost --help
