using FluentAssertions;
using QAOrchestrator.Domain;
using QAOrchestrator.DotNet;

namespace QAOrchestrator.UnitTests;

public sealed class TestPackageClassifierTests
{
    [Fact]
    public void Classify_MapsKnownAuxiliaryTestPackages()
    {
        var packages = new[]
        {
            new PackageReferenceInfo("AwesomeAssertions", "9.0.0"),
            new PackageReferenceInfo("Moq", "4.20.72"),
            new PackageReferenceInfo("coverlet.collector", "6.0.4"),
            new PackageReferenceInfo("coverlet.msbuild", "6.0.4"),
            new PackageReferenceInfo("Refit.HttpClientFactory", "8.0.0"),
            new PackageReferenceInfo("Testcontainers.PostgreSql", "4.0.0"),
            new PackageReferenceInfo("Bogus", "35.0.0")
        };

        var classifications = new TestPackageClassifier().Classify(packages);

        classifications.Should().Contain(x => x.PackageName == "AwesomeAssertions" && x.Category == TestPackageCategory.AssertionFramework);
        classifications.Should().Contain(x => x.PackageName == "Moq" && x.Category == TestPackageCategory.MockFramework);
        classifications.Should().Contain(x => x.PackageName == "coverlet.collector" && x.Category == TestPackageCategory.CoverageTool);
        classifications.Should().Contain(x => x.PackageName == "coverlet.msbuild" && x.Category == TestPackageCategory.CoverageTool);
        classifications.Should().Contain(x => x.PackageName == "Refit.HttpClientFactory" && x.Category == TestPackageCategory.HttpClientTool);
        classifications.Should().Contain(x => x.PackageName == "Testcontainers.PostgreSql" && x.Category == TestPackageCategory.ContainerTool);
        classifications.Should().Contain(x => x.PackageName == "Bogus" && x.Category == TestPackageCategory.DataGenerationTool);
    }
}
