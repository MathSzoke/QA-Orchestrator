using FluentAssertions;
using QAOrchestrator.Cli;
using System.CommandLine;

namespace QAOrchestrator.UnitTests;

public sealed class CliRegistrationTests
{
    [Fact]
    public void RootCommand_RegistersExpectedSubcommands()
    {
        var command = CommandRegistration.CreateRootCommand();

        command.Subcommands.Select(subcommand => subcommand.Name)
            .Should()
            .Contain(["init", "analyze", "clean", "boost", "coverage", "mutation", "report", "doctor", "version"]);
    }

    [Theory]
    [InlineData("doctor")]
    [InlineData("version")]
    [InlineData("boost")]
    public void RootCommand_ParsesRegisteredSubcommands(string subcommand)
    {
        var result = CommandRegistration.CreateRootCommand().Parse(subcommand);

        result.Errors.Should().BeEmpty();
        result.CommandResult.Command.Name.Should().Be(subcommand);
    }

    [Theory]
    [InlineData("--dry-run")]
    [InlineData("--include-existing")]
    [InlineData("--no-validation")]
    [InlineData("--max-candidates 3")]
    [InlineData("--solution QAOrchestrator.sln")]
    [InlineData("--target unit,endpoints")]
    [InlineData("--safe")]
    public void BoostCommand_ParsesExpectedOptions(string option)
    {
        var result = CommandRegistration.CreateRootCommand().Parse($"boost {option}");

        result.Errors.Should().BeEmpty();
        result.CommandResult.Command.Name.Should().Be("boost");
    }

    [Fact]
    public void BoostHelp_ParsesWithoutErrors()
    {
        var result = CommandRegistration.CreateRootCommand().Parse("boost --help");

        result.Errors.Should().BeEmpty();
        result.CommandResult.Command.Name.Should().Be("boost");
    }

    [Fact]
    public void RootHelp_ContainsBoostDoctorAndVersion()
    {
        var root = CommandRegistration.CreateRootCommand();

        root.Subcommands.Select(subcommand => subcommand.Name)
            .Should()
            .Contain(["boost", "doctor", "version"]);
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
