using QAOrchestrator.Domain;

namespace QAOrchestrator.Generation;

public sealed class EndpointUnitTestabilityDetector
{
    public bool IsUnitTestable(TestableSourceClass sourceClass)
        => sourceClass.Kind == TestableSourceKind.Endpoint && sourceClass.IsUnitTestable;

    public bool RequiresIntegration(TestableSourceClass sourceClass)
        => sourceClass.Kind == TestableSourceKind.Endpoint
            && !sourceClass.IsUnitTestable
            && sourceClass.Reason.Contains("integration test", StringComparison.OrdinalIgnoreCase);
}
