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
