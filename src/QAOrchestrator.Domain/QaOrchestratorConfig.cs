namespace QAOrchestrator.Domain;

public sealed record QaOrchestratorConfig
{
    public string? Solution { get; init; }

    public string Mode { get; init; } = "coverage-booster";

    public bool SafeMode { get; init; } = true;

    public string[] Targets { get; init; } = ["unit", "integration", "functional", "mutation"];

    public CoverageConfig Coverage { get; init; } = new();

    public GenerationConfig Generation { get; init; } = new();

    public DotNetConfig DotNet { get; init; } = new();

    public GitConfig Git { get; init; } = new();

    public static QaOrchestratorConfig CreateDefault() => new();
}

public sealed record CoverageConfig
{
    public int Line { get; init; } = 100;

    public int Branch { get; init; } = 80;

    public int Unit { get; init; } = 80;

    public int Integration { get; init; } = 80;

    public int Functional { get; init; } = 80;

    public int Mutation { get; init; } = 80;

    public string[] Exclude { get; init; } =
    [
        "**/Migrations/**",
        "**/Program.cs",
        "**/*Options.cs",
        "**/*Settings.cs",
        "**/*Dto.cs",
        "**/*Request.cs",
        "**/*Response.cs",
        "**/bin/**",
        "**/obj/**"
    ];
}

public sealed record GenerationConfig
{
    public bool PreserveExistingTests { get; init; } = true;

    public string CandidateFolder { get; init; } = "tests/.qa-orchestrator/candidates";

    public string FailedCandidateFolder { get; init; } = "tests/.qa-orchestrator/failed-candidates";

    public string ReportFolder { get; init; } = "tests/.qa-orchestrator/reports";

    public bool PreferExistingPatterns { get; init; } = true;

    public bool AllowEditExistingTests { get; init; }

    public bool AllowCreateNewTestFiles { get; init; } = true;

    public bool AllowAppendToExistingTestFiles { get; init; }

    public bool KeepFailedCandidates { get; init; } = true;

    public bool KeepLowValueCandidates { get; init; }
}

public sealed record DotNetConfig
{
    public string TestCommand { get; init; } = "dotnet test";

    public string CoverageFormat { get; init; } = "cobertura";

    public string StrykerCommand { get; init; } = "dotnet stryker";
}

public sealed record GitConfig
{
    public string DefaultBaseBranch { get; init; } = "main";
}
