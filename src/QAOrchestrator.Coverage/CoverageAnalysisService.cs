using QAOrchestrator.Domain;

namespace QAOrchestrator.Coverage;

public sealed class CoverageAnalysisService
{
    public CoverageReport CreateEmptyReport()
        => new(0, 0, []);
}
