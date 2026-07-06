using QAOrchestrator.Domain;

namespace QAOrchestrator.DotNet;

public sealed class TestPackageClassifier
{
    private static readonly Dictionary<string, (TestPackageCategory Category, string ToolName)> KnownPackages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["xunit"] = (TestPackageCategory.TestFramework, "XUnit"),
        ["xunit.runner.visualstudio"] = (TestPackageCategory.TestFramework, "XUnit"),
        ["NUnit"] = (TestPackageCategory.TestFramework, "NUnit"),
        ["MSTest.TestFramework"] = (TestPackageCategory.TestFramework, "MSTest"),
        ["AwesomeAssertions"] = (TestPackageCategory.AssertionFramework, "AwesomeAssertions"),
        ["FluentAssertions"] = (TestPackageCategory.AssertionFramework, "FluentAssertions"),
        ["Shouldly"] = (TestPackageCategory.AssertionFramework, "Shouldly"),
        ["Moq"] = (TestPackageCategory.MockFramework, "Moq"),
        ["NSubstitute"] = (TestPackageCategory.MockFramework, "NSubstitute"),
        ["FakeItEasy"] = (TestPackageCategory.MockFramework, "FakeItEasy"),
        ["coverlet.collector"] = (TestPackageCategory.CoverageTool, "coverlet.collector"),
        ["coverlet.msbuild"] = (TestPackageCategory.CoverageTool, "coverlet.msbuild"),
        ["Stryker.NET"] = (TestPackageCategory.MutationTool, "Stryker.NET"),
        ["dotnet-stryker"] = (TestPackageCategory.MutationTool, "dotnet-stryker"),
        ["Refit"] = (TestPackageCategory.HttpClientTool, "Refit"),
        ["Refit.HttpClientFactory"] = (TestPackageCategory.HttpClientTool, "Refit.HttpClientFactory"),
        ["Microsoft.AspNetCore.Mvc.Testing"] = (TestPackageCategory.IntegrationTestTool, "Microsoft.AspNetCore.Mvc.Testing"),
        ["Microsoft.AspNetCore.TestHost"] = (TestPackageCategory.IntegrationTestTool, "Microsoft.AspNetCore.TestHost"),
        ["WireMock.Net"] = (TestPackageCategory.IntegrationTestTool, "WireMock.Net"),
        ["Testcontainers"] = (TestPackageCategory.ContainerTool, "Testcontainers"),
        ["DotNet.Testcontainers"] = (TestPackageCategory.ContainerTool, "DotNet.Testcontainers"),
        ["Testcontainers.PostgreSql"] = (TestPackageCategory.ContainerTool, "Testcontainers.PostgreSql"),
        ["Testcontainers.Redis"] = (TestPackageCategory.ContainerTool, "Testcontainers.Redis"),
        ["Testcontainers.RabbitMq"] = (TestPackageCategory.ContainerTool, "Testcontainers.RabbitMq"),
        ["Testcontainers.Kafka"] = (TestPackageCategory.ContainerTool, "Testcontainers.Kafka"),
        ["Bogus"] = (TestPackageCategory.DataGenerationTool, "Bogus"),
        ["AutoFixture"] = (TestPackageCategory.DataGenerationTool, "AutoFixture")
    };

    public IReadOnlyList<TestPackageClassification> Classify(IEnumerable<PackageReferenceInfo> packages)
    {
        return packages
            .Select(Classify)
            .Where(classification => classification is not null)
            .Select(classification => classification!)
            .OrderBy(classification => classification.Category)
            .ThenBy(classification => classification.PackageName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public TestFramework DetectTestFramework(IEnumerable<TestPackageClassification> classifications)
    {
        var tools = classifications
            .Where(classification => classification.Category == TestPackageCategory.TestFramework)
            .Select(classification => classification.ToolName)
            .ToArray();

        if (tools.Any(tool => tool.Equals("XUnit", StringComparison.OrdinalIgnoreCase)))
        {
            return TestFramework.XUnit;
        }

        if (tools.Any(tool => tool.Equals("NUnit", StringComparison.OrdinalIgnoreCase)))
        {
            return TestFramework.NUnit;
        }

        if (tools.Any(tool => tool.Equals("MSTest", StringComparison.OrdinalIgnoreCase)))
        {
            return TestFramework.MSTest;
        }

        return TestFramework.Unknown;
    }

    public MockFramework DetectMockFramework(IEnumerable<TestPackageClassification> classifications)
    {
        var tools = classifications
            .Where(classification => classification.Category == TestPackageCategory.MockFramework)
            .Select(classification => classification.ToolName)
            .ToArray();

        if (tools.Any(tool => tool.Equals("Moq", StringComparison.OrdinalIgnoreCase)))
        {
            return MockFramework.Moq;
        }

        if (tools.Any(tool => tool.Equals("NSubstitute", StringComparison.OrdinalIgnoreCase)))
        {
            return MockFramework.NSubstitute;
        }

        if (tools.Any(tool => tool.Equals("FakeItEasy", StringComparison.OrdinalIgnoreCase)))
        {
            return MockFramework.FakeItEasy;
        }

        return MockFramework.Unknown;
    }

    public AssertionFramework DetectAssertionFramework(IEnumerable<TestPackageClassification> classifications)
    {
        var tools = classifications
            .Where(classification => classification.Category == TestPackageCategory.AssertionFramework)
            .Select(classification => classification.ToolName)
            .ToArray();

        if (tools.Any(tool => tool.Equals("AwesomeAssertions", StringComparison.OrdinalIgnoreCase)))
        {
            return AssertionFramework.AwesomeAssertions;
        }

        if (tools.Any(tool => tool.Equals("FluentAssertions", StringComparison.OrdinalIgnoreCase)))
        {
            return AssertionFramework.FluentAssertions;
        }

        if (tools.Any(tool => tool.Equals("Shouldly", StringComparison.OrdinalIgnoreCase)))
        {
            return AssertionFramework.Shouldly;
        }

        return AssertionFramework.Unknown;
    }

    private static TestPackageClassification? Classify(PackageReferenceInfo package)
    {
        if (KnownPackages.TryGetValue(package.Name, out var known))
        {
            return new TestPackageClassification(package.Name, package.Version, known.Category, known.ToolName);
        }

        return LooksTestRelated(package.Name)
            ? new TestPackageClassification(package.Name, package.Version, TestPackageCategory.UnknownTestRelatedPackage, package.Name)
            : null;
    }

    private static bool LooksTestRelated(string packageName)
    {
        var markers = new[]
        {
            "test",
            "testing",
            "assert",
            "mock",
            "fixture",
            "faker",
            "cover",
            "stryker",
            "wiremock",
            "container"
        };

        return markers.Any(marker => packageName.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }
}
