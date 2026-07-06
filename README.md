# QA Orchestrator

QA Orchestrator is a conservative .NET CLI foundation for analyzing a target solution before generating coverage-boosting test candidates.

Current foundation:

- `qa-orchestrator init`
- `qa-orchestrator analyze --solution <solution.sln>`
- `qa-orchestrator clean`
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
