namespace QAOrchestrator.Domain;

public enum ProjectKind
{
    Source,
    Test
}

public enum TestFramework
{
    Unknown,
    XUnit,
    NUnit,
    MSTest
}

public enum MockFramework
{
    Unknown,
    Moq,
    NSubstitute,
    FakeItEasy
}

public enum AssertionFramework
{
    Unknown,
    AwesomeAssertions,
    FluentAssertions,
    Shouldly
}

public enum TestPackageCategory
{
    TestFramework,
    AssertionFramework,
    MockFramework,
    CoverageTool,
    MutationTool,
    HttpClientTool,
    IntegrationTestTool,
    FunctionalTestTool,
    ContainerTool,
    DataGenerationTool,
    UnknownTestRelatedPackage
}

public enum ArchitectureStyle
{
    GenericDotNet,
    VerticalSliceCqrs,
    MinimalApi,
    ControllerApi,
    CleanArchitecture,
    SimpleServiceLayer
}

public enum BoostTarget
{
    Unit,
    Endpoints,
    Integration,
    Functional,
    Mutation
}

public enum TestType
{
    Unit,
    Integration,
    Functional,
    Endpoint
}

public enum CandidateStatus
{
    Planned,
    Generated,
    Accepted,
    Failed,
    Skipped
}

public enum ExistingTestMatchKind
{
    Exact,
    Strong,
    Weak,
    None
}

public enum TestableSourceKind
{
    Handler,
    Validator,
    Service,
    Rule,
    Policy,
    Mapper,
    Factory,
    Guard,
    Endpoint,
    GeneralClass
}

public sealed record TargetSolution(
    string Name,
    string SolutionPath,
    string RootDirectory,
    IReadOnlyList<TargetProject> Projects,
    ArchitectureDetectionResult Architecture);

public sealed record TargetProject(
    string Name,
    string ProjectPath,
    string Directory,
    ProjectKind Kind,
    IReadOnlyList<string> TargetFrameworks,
    IReadOnlyList<PackageReferenceInfo> PackageReferences,
    IReadOnlyList<ProjectReferenceInfo> ProjectReferences,
    TestFramework TestFramework,
    MockFramework MockFramework,
    AssertionFramework AssertionFramework,
    IReadOnlyList<TestPackageClassification> TestPackageClassifications,
    TestProjectPattern? TestPattern)
{
    public bool IsTestProject => Kind == ProjectKind.Test;
}

public sealed record SourceProject(TargetProject Project);

public sealed record TestProject(TargetProject Project);

public sealed record PackageReferenceInfo(string Name, string Version);

public sealed record TestPackageClassification(
    string PackageName,
    string Version,
    TestPackageCategory Category,
    string ToolName);

public sealed record ProjectReferenceInfo(string Include, string? ProjectName);

public sealed record ProjectArchitecture(ArchitectureStyle Style, int Confidence, string Reason);

public sealed record ArchitectureDetectionResult(
    ProjectArchitecture Primary,
    IReadOnlyList<ProjectArchitecture> DetectedArchitectures);

public sealed record CodeSymbol(string Name, string Namespace, string FilePath);

public sealed record ClassSymbol(string Name, string Namespace, string FilePath, IReadOnlyList<MethodSymbol> Methods);

public sealed record MethodSymbol(string Name, string ReturnType, int LineNumber, bool IsPublic);

public sealed record ConstructorSymbol(string ClassName, IReadOnlyList<DependencySymbol> Dependencies);

public sealed record DependencySymbol(string Name, string TypeName);

public sealed record TestPattern(
    TestFramework TestFramework,
    MockFramework MockFramework,
    AssertionFramework AssertionFramework,
    IReadOnlyList<string> NamingConventions,
    IReadOnlyList<string> FixtureTypes);

public sealed record TestProjectPattern(
    string ProjectName,
    int TestClassCount,
    IReadOnlyList<TestClassPattern> Classes,
    IReadOnlyList<TestMethodPattern> Methods,
    IReadOnlyList<TestAttributeUsage> AttributeUsages,
    IReadOnlyList<DetectedUsing> CommonUsings,
    IReadOnlyList<TestNamingConvention> NamingConventions,
    TestCodePattern CodePattern,
    IReadOnlyList<TestDependencyUsage> DependencyUsages);

public sealed record TestCodePattern(
    bool UsesArrangeActAssert,
    bool UsesMoqMock,
    bool UsesMoqSetup,
    bool UsesMoqVerify,
    bool UsesFixture,
    bool UsesWebApplicationFactory,
    bool UsesHttpClient,
    bool UsesShouldAssertions,
    bool UsesAwesomeAssertions,
    bool UsesRefitClients,
    bool UsesXUnitCollectionFixtures);

public sealed record DetectedUsing(string Namespace, int Count);

public sealed record TestClassPattern(string Name, string? Suffix, string FilePath);

public sealed record TestMethodPattern(string Name, string? NamingConvention, string FilePath);

public sealed record TestAttributeUsage(string Name, int Count);

public sealed record TestNamingConvention(string Name, int Count);

public sealed record TestDependencyUsage(string Name, int Count);

public sealed record CleanPlan(string RootDirectory, IReadOnlyList<CleanTarget> Targets)
{
    public bool HasTargets => Targets.Count > 0;
}

public sealed record CleanTarget(string Path, string RelativePath, bool IsDirectory);

public sealed record CleanResult(CleanPlan Plan, bool DryRun, bool Deleted, IReadOnlyList<string> RemovedPaths);

public sealed record BoostOptions(
    string WorkingDirectory,
    string? Solution,
    IReadOnlyList<BoostTarget> Targets,
    bool SafeMode,
    bool DryRun,
    int MaxCandidates,
    bool IncludeExisting,
    bool NoValidation);

public sealed record TestableSourceClass(
    string ProjectName,
    string FilePath,
    string RelativePath,
    string Namespace,
    string ClassName,
    TestableSourceKind Kind,
    IReadOnlyList<string> PublicMethods,
    IReadOnlyList<string> ConstructorDependencies,
    bool IsUnitTestable,
    string Reason);

public sealed record ExistingTestMatch(
    ExistingTestMatchKind Kind,
    string TestFilePath,
    string RelativePath,
    int Confidence,
    string Reason);

public sealed record ExistingTestMatchResult(
    TestableSourceClass SourceClass,
    IReadOnlyList<ExistingTestMatch> ExactMatches,
    IReadOnlyList<ExistingTestMatch> StrongMatches,
    IReadOnlyList<ExistingTestMatch> WeakMatches,
    int Confidence,
    string Reason)
{
    public bool HasExactOrStrongMatch => ExactMatches.Count > 0 || StrongMatches.Count > 0;
}

public sealed record TestLocationResolution(
    TestType TestType,
    string? MatchingTestProject,
    IReadOnlyList<string> ExistingExactTestFiles,
    IReadOnlyList<string> ExistingSimilarTestFiles,
    string SuggestedFinalPath,
    string CandidatePath,
    int Confidence,
    string Reason,
    bool IsComplementary,
    bool NeedsManualReview);

public sealed record BoostCandidate(
    TestableSourceClass SourceClass,
    ExistingTestMatchResult ExistingTestMatch,
    TestLocationResolution Location,
    CandidateStatus Status,
    string CandidateName,
    string Reason,
    string? FailureReason);

public sealed record BoostPlan(
    TargetSolution Solution,
    IReadOnlyList<BoostTarget> Targets,
    IReadOnlyList<TestableSourceClass> SourceClasses,
    IReadOnlyList<BoostCandidate> Candidates,
    IReadOnlyList<BoostCandidate> SkippedCandidates);

public sealed record BoostReport(
    BoostPlan Plan,
    IReadOnlyList<BoostCandidate> GeneratedCandidates,
    IReadOnlyList<BoostCandidate> AcceptedCandidates,
    IReadOnlyList<BoostCandidate> FailedCandidates,
    string MarkdownReportPath,
    string JsonReportPath,
    string ConsoleSummary);

public sealed record CoverageReport(decimal LineCoverage, decimal BranchCoverage, IReadOnlyList<CoverageGap> Gaps);

public sealed record CoverageGap(
    string FilePath,
    string? ClassName,
    string? MethodName,
    int? LineNumber,
    string GapType,
    string TestTarget);

public sealed record BranchCoverageGap(
    string FilePath,
    string? ClassName,
    string? MethodName,
    int LineNumber,
    string TestTarget);

public sealed record LineCoverageGap(
    string FilePath,
    string? ClassName,
    string? MethodName,
    int LineNumber,
    string TestTarget);

public sealed record MutationReport(decimal MutationScore, IReadOnlyList<SurvivedMutation> SurvivedMutations);

public sealed record SurvivedMutation(
    string FilePath,
    string? MethodName,
    int? LineNumber,
    string Mutator,
    string Description);

public sealed record TestGenerationPlan(IReadOnlyList<TestCandidate> Candidates);

public sealed record TestCandidate(string Name, string TargetFilePath, string TestType, string Reason);

public sealed record GeneratedTestResult(TestCandidate Candidate, bool Passed, bool ImprovedCoverage, string? FailureReason);

public sealed record ExecutionResult(bool Success, string Message)
{
    public static ExecutionResult Ok(string message) => new(true, message);

    public static ExecutionResult Fail(string message) => new(false, message);
}
