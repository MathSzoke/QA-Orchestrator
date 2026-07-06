using QAOrchestrator.Domain;

namespace QAOrchestrator.Mutation;

public sealed class MutationAnalysisService
{
    public MutationReport CreateEmptyReport()
        => new(0, []);
}
