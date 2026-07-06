using System.Text.Json;
using QAOrchestrator.Domain;

namespace QAOrchestrator.Infrastructure;

public sealed class ConfigurationStore
{
    public const string FileName = "qa-orchestrator.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public string GetConfigurationPath(string rootDirectory)
        => Path.Combine(Path.GetFullPath(rootDirectory), FileName);

    public string ResolveConfiguredPath(string rootDirectory, string configuredRelativePath)
        => Path.Combine(
            Path.GetFullPath(rootDirectory),
            configuredRelativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar));

    public bool Exists(string rootDirectory)
        => File.Exists(GetConfigurationPath(rootDirectory));

    public QaOrchestratorConfig LoadOrDefault(string rootDirectory)
    {
        var path = GetConfigurationPath(rootDirectory);
        if (!File.Exists(path))
        {
            return QaOrchestratorConfig.CreateDefault();
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<QaOrchestratorConfig>(json, JsonOptions)
            ?? QaOrchestratorConfig.CreateDefault();
    }

    public string CreateDefault(string rootDirectory)
    {
        var path = GetConfigurationPath(rootDirectory);
        if (File.Exists(path))
        {
            throw new InvalidOperationException($"Configuration already exists: {path}");
        }

        var config = QaOrchestratorConfig.CreateDefault();
        File.WriteAllText(path, JsonSerializer.Serialize(config, JsonOptions));
        EnsureWorkingFolders(rootDirectory, config);
        return path;
    }

    public void EnsureWorkingFolders(string rootDirectory, QaOrchestratorConfig config)
    {
        Directory.CreateDirectory(ResolveConfiguredPath(rootDirectory, config.Generation.CandidateFolder));
        Directory.CreateDirectory(ResolveConfiguredPath(rootDirectory, config.Generation.FailedCandidateFolder));
        Directory.CreateDirectory(ResolveConfiguredPath(rootDirectory, config.Generation.ReportFolder));
    }
}
