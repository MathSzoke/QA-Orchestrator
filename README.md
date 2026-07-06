# QA Orchestrator

QA Orchestrator is a conservative .NET CLI foundation for analyzing a target solution before generating coverage-boosting test candidates.

Current foundation:

- `qa-orchestrator init`
- `qa-orchestrator analyze --solution <solution.sln>`
- `qa-orchestrator clean`
- `qa-orchestrator boost --solution <solution.sln> --target unit --safe`
- `qa-orchestrator version`
- `qa-orchestrator doctor`
- .NET project/test detection
- package classification for test frameworks, assertion frameworks, mock frameworks, coverage tools, mutation tools, HTTP clients, integration tools, containers and data generation packages
- test pattern reading from existing `.cs` test files
- basic architecture detection
- console and Markdown analysis reports

Clean examples:

```powershell
qa-orchestrator clean --dry-run
qa-orchestrator clean --force
```

Boost examples:

```powershell
qa-orchestrator boost --solution MinhaSolution.sln --target unit --safe
qa-orchestrator boost --solution MinhaSolution.sln --target unit,endpoints --safe --dry-run
```

The first boost implementation is intentionally conservative. It matches existing tests, resolves candidate and suggested final locations, writes candidates only under `tests/.qa-orchestrator`, and never edits existing tests or source files.

After `git pull`, update the global tool with:

```powershell
.\scripts\update-global-tool.ps1
```

Manual update:

```powershell
dotnet pack .\src\QAOrchestrator.Cli\QAOrchestrator.Cli.csproj -c Release
dotnet tool update --global --add-source .\src\QAOrchestrator.Cli\nupkg QAOrchestrator.Cli
```

If update fails:

```powershell
dotnet tool uninstall --global QAOrchestrator.Cli
dotnet tool install --global --add-source .\src\QAOrchestrator.Cli\nupkg QAOrchestrator.Cli
```
