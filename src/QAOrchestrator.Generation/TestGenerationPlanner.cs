using QAOrchestrator.Domain;

namespace QAOrchestrator.Generation;

public sealed class TestGenerationPlanner
{
    public TestGenerationPlan CreateEmptyPlan()
        => new([]);
}
