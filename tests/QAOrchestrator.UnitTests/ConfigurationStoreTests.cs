using FluentAssertions;
using QAOrchestrator.Infrastructure;

namespace QAOrchestrator.UnitTests;

public sealed class ConfigurationStoreTests
{
    [Fact]
    public void CreateDefault_WritesConfigurationAndWorkingFolders()
    {
        using var temp = TempDirectory.Create();
        var store = new ConfigurationStore();

        var path = store.CreateDefault(temp.Path);

        File.Exists(path).Should().BeTrue();
        Directory.Exists(Path.Combine(temp.Path, "tests/.qa-orchestrator/candidates")).Should().BeTrue();
        Directory.Exists(Path.Combine(temp.Path, "tests/.qa-orchestrator/failed-candidates")).Should().BeTrue();
        Directory.Exists(Path.Combine(temp.Path, "tests/.qa-orchestrator/reports")).Should().BeTrue();
    }

    [Fact]
    public void CreateDefault_DoesNotOverwriteExistingConfiguration()
    {
        using var temp = TempDirectory.Create();
        var store = new ConfigurationStore();
        store.CreateDefault(temp.Path);

        var action = () => store.CreateDefault(temp.Path);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Configuration already exists:*");
    }
}
