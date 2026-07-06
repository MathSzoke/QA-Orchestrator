using FluentAssertions;
using QAOrchestrator.Domain;
using QAOrchestrator.DotNet;

namespace QAOrchestrator.UnitTests;

public sealed class DotNetSolutionReaderTests
{
    [Fact]
    public void Read_DetectsSourceAndTestProjectsFromSolution()
    {
        using var temp = TempDirectory.Create();
        var sourceProject = WriteProject(temp.Path, "src/App/App.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var testProject = WriteProject(temp.Path, "tests/App.Tests/App.Tests.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.0.0" />
                <PackageReference Include="xunit" Version="2.9.3" />
                <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5" />
                <PackageReference Include="Moq" Version="4.20.72" />
                <PackageReference Include="AwesomeAssertions" Version="9.0.0" />
                <PackageReference Include="Refit.HttpClientFactory" Version="8.0.0" />
                <PackageReference Include="coverlet.collector" Version="6.0.4" />
                <PackageReference Include="coverlet.msbuild" Version="6.0.4" />
                <ProjectReference Include="..\..\src\App\App.csproj" />
              </ItemGroup>
            </Project>
            """);
        Directory.CreateDirectory(Path.GetDirectoryName(testProject)!);
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(testProject)!, "UserHandlerTests.cs"), """
            using AwesomeAssertions;
            using Moq;
            using Refit;
            using Xunit;

            public sealed class UserHandlerTests : IClassFixture<ApiFixture>
            {
                [Fact]
                [Trait("Category", "Unit")]
                public void Should_ReturnUser_When_UserExists()
                {
                    // Arrange
                    var repository = new Mock<IUserRepository>();
                    repository.Setup(x => x.Get()).Returns("user");

                    // Act
                    var result = repository.Object.Get();

                    // Assert
                    result.Should().Be("user");
                    repository.Verify(x => x.Get(), Times.Once);
                }
            }

            public sealed class ApiFixture;
            public interface IUserRepository { string Get(); }
            """);

        var solutionPath = Path.Combine(temp.Path, "Sample.sln");
        File.WriteAllText(solutionPath, $$"""
            Microsoft Visual Studio Solution File, Format Version 12.00
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "App", "{{Relative(temp.Path, sourceProject)}}", "{11111111-1111-1111-1111-111111111111}"
            EndProject
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "App.Tests", "{{Relative(temp.Path, testProject)}}", "{22222222-2222-2222-2222-222222222222}"
            EndProject
            Global
            EndGlobal
            """);

        var solution = new DotNetSolutionReader().Read(solutionPath);

        solution.Projects.Should().HaveCount(2);
        solution.Projects.Single(project => project.Name == "App").Kind.Should().Be(ProjectKind.Source);

        var detectedTestProject = solution.Projects.Single(project => project.Name == "App.Tests");
        detectedTestProject.Kind.Should().Be(ProjectKind.Test);
        detectedTestProject.TestFramework.Should().Be(TestFramework.XUnit);
        detectedTestProject.MockFramework.Should().Be(MockFramework.Moq);
        detectedTestProject.AssertionFramework.Should().Be(AssertionFramework.AwesomeAssertions);
        detectedTestProject.ProjectReferences.Should().Contain(reference => reference.ProjectName == "App");
        detectedTestProject.TestPackageClassifications.Should().Contain(classification => classification.Category == TestPackageCategory.CoverageTool && classification.ToolName == "coverlet.collector");
        detectedTestProject.TestPackageClassifications.Should().Contain(classification => classification.Category == TestPackageCategory.HttpClientTool && classification.ToolName == "Refit.HttpClientFactory");
        detectedTestProject.TestPattern.Should().NotBeNull();
        detectedTestProject.TestPattern!.TestClassCount.Should().BeGreaterThan(0);
        detectedTestProject.TestPattern.CodePattern.UsesMoqMock.Should().BeTrue();
        detectedTestProject.TestPattern.CodePattern.UsesMoqSetup.Should().BeTrue();
        detectedTestProject.TestPattern.CodePattern.UsesMoqVerify.Should().BeTrue();
        detectedTestProject.TestPattern.CodePattern.UsesShouldAssertions.Should().BeTrue();
        detectedTestProject.TestPattern.CodePattern.UsesAwesomeAssertions.Should().BeTrue();
        detectedTestProject.TestPattern.CodePattern.UsesXUnitCollectionFixtures.Should().BeTrue();
    }

    [Fact]
    public void Read_DetectsMinimalApiArchitecture()
    {
        using var temp = TempDirectory.Create();
        var project = WriteProject(temp.Path, "src/Api/Api.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        File.WriteAllText(Path.Combine(Path.GetDirectoryName(project)!, "Program.cs"), """
            var builder = WebApplication.CreateBuilder(args);
            var app = builder.Build();
            app.MapGet("/health", () => Results.Ok());
            app.Run();
            """);

        var solutionPath = Path.Combine(temp.Path, "Api.sln");
        File.WriteAllText(solutionPath, $$"""
            Microsoft Visual Studio Solution File, Format Version 12.00
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Api", "{{Relative(temp.Path, project)}}", "{11111111-1111-1111-1111-111111111111}"
            EndProject
            Global
            EndGlobal
            """);

        var solution = new DotNetSolutionReader().Read(solutionPath);

        solution.Architecture.Primary.Style.Should().Be(ArchitectureStyle.MinimalApi);
    }

    private static string WriteProject(string root, string relativePath, string content)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private static string Relative(string root, string path)
        => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '\\');
}
