using FluentAssertions;

namespace QAOrchestrator.UnitTests;

public sealed class CliRegistrationTests
{
    [Fact]
    public void Program_RegistersBoostVersionAndDoctorCommands()
    {
        var program = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "QAOrchestrator.Cli", "Program.cs"));

        program.Should().Contain("CreateBoostCommand");
        program.Should().Contain("CreateVersionCommand");
        program.Should().Contain("CreateDoctorCommand");
        program.Should().Contain("--max-candidates");
        program.Should().Contain("--no-validation");
    }

    [Fact]
    public void GlobalToolScriptsExist()
    {
        var root = GetRepositoryRoot();

        File.Exists(Path.Combine(root, "scripts", "install-global-tool.ps1")).Should().BeTrue();
        File.Exists(Path.Combine(root, "scripts", "update-global-tool.ps1")).Should().BeTrue();
        File.Exists(Path.Combine(root, "scripts", "uninstall-global-tool.ps1")).Should().BeTrue();
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QAOrchestrator.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
