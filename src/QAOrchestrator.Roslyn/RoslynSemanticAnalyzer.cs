using QAOrchestrator.Domain;

namespace QAOrchestrator.Roslyn;

public sealed class RoslynSemanticAnalyzer
{
    public IReadOnlyList<ClassSymbol> AnalyzeClasses(string projectPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        return [];
    }
}
