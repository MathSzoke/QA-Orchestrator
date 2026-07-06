using System.Diagnostics;

namespace QAOrchestrator.Infrastructure;

public sealed class ProcessExecutor
{
    public async Task<ProcessExecutionResult> ExecuteAsync(string fileName, string arguments, string workingDirectory, CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo(fileName, arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start process {fileName}.");
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        return new ProcessExecutionResult(process.ExitCode, await outputTask, await errorTask);
    }
}

public sealed record ProcessExecutionResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Success => ExitCode == 0;
}
