using QAOrchestrator.Domain;

namespace QAOrchestrator.Generation;

public sealed class TestGenerationPlanner
{
    private readonly SourceClassAnalyzer _sourceClassAnalyzer = new();
    private readonly ExistingTestMatcher _existingTestMatcher = new();
    private readonly TestLocationResolver _testLocationResolver = new();

    public TestGenerationPlan CreateEmptyPlan()
        => new([]);

    public BoostPlan CreateBoostPlan(TargetSolution solution, QaOrchestratorConfig config, BoostOptions options)
    {
        var sourceClasses = _sourceClassAnalyzer.Analyze(solution, config).ToArray();
        var supportedTargets = options.Targets.Where(target => target is BoostTarget.Unit or BoostTarget.Endpoints).ToArray();
        var candidates = new List<BoostCandidate>();
        var skipped = new List<BoostCandidate>();

        foreach (var sourceClass in sourceClasses)
        {
            var target = sourceClass.Kind == TestableSourceKind.Endpoint ? BoostTarget.Endpoints : BoostTarget.Unit;
            if (!supportedTargets.Contains(target))
            {
                continue;
            }

            var match = _existingTestMatcher.Match(solution, sourceClass);
            var testType = sourceClass.Kind == TestableSourceKind.Endpoint ? TestType.Endpoint : TestType.Unit;
            var location = _testLocationResolver.Resolve(solution, config, sourceClass, match, testType);
            var candidateName = Path.GetFileName(location.CandidatePath);

            if (!sourceClass.IsUnitTestable)
            {
                skipped.Add(new BoostCandidate(sourceClass, match, location, CandidateStatus.Skipped, candidateName, sourceClass.Reason, sourceClass.Reason));
                continue;
            }

            if (match.HasExactOrStrongMatch && !options.IncludeExisting)
            {
                candidates.Add(new BoostCandidate(
                    sourceClass,
                    match,
                    location,
                    CandidateStatus.Planned,
                    candidateName,
                    "Existing related test found. Planning complementary candidate only.",
                    null));
                continue;
            }

            candidates.Add(new BoostCandidate(
                sourceClass,
                match,
                location,
                CandidateStatus.Planned,
                candidateName,
                match.HasExactOrStrongMatch ? "Complementary coverage candidate." : "No existing test found. New coverage candidate planned.",
                null));
        }

        var limitedCandidates = candidates.Take(Math.Max(0, options.MaxCandidates)).ToArray();
        return new BoostPlan(solution, supportedTargets, sourceClasses, limitedCandidates, skipped);
    }
}
