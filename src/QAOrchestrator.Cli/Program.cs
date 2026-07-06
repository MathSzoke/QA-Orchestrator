using QAOrchestrator.Cli;

return await CommandRegistration.CreateRootCommand().Parse(args).InvokeAsync();
