$ErrorActionPreference = "Continue"

dotnet tool uninstall --global QAOrchestrator.Cli

if ($LASTEXITCODE -ne 0) {
    Write-Host "QAOrchestrator.Cli was not installed globally or could not be removed."
}
