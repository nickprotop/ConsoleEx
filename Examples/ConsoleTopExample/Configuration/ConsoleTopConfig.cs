namespace ConsoleTopExample.Configuration;

internal sealed record ConsoleTopConfig
{
    public int RefreshIntervalMs { get; init; } = Helpers.UIConstants.RefreshIntervalMs;
    public int PrimeDelayMs { get; init; } = Helpers.UIConstants.PrimeDelayMs;
    public int MaxHistoryPoints { get; init; } = Helpers.UIConstants.MaxHistoryPoints;

    public bool ShowProcessesTab { get; init; } = true;
    public bool ShowMemoryTab { get; init; } = true;
    public bool ShowCpuTab { get; init; } = true;
    public bool ShowNetworkTab { get; init; } = true;
    public bool ShowStorageTab { get; init; } = true;

    public static ConsoleTopConfig Default { get; } = new();
}
