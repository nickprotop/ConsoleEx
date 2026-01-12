namespace OpenCodeShowcase.Models;

/// <summary>
/// Represents the status of a tool execution
/// </summary>
public enum ToolStatus
{
    Running,
    Complete,
    Error
}

/// <summary>
/// Represents the type of tool being executed
/// </summary>
public enum ToolType
{
    ReadFile,
    WriteFile,
    RunCommand,
    Analyze,
    Diff,
    Test
}

/// <summary>
/// Immutable tool call record
/// </summary>
/// <param name="Type">Type of tool being called</param>
/// <param name="Name">Display name of the tool</param>
/// <param name="Parameters">Tool parameters (e.g., "File: src/auth/login.cs")</param>
/// <param name="Output">Tool execution output/results</param>
/// <param name="Status">Current execution status</param>
/// <param name="ExecutionTime">Time taken to execute in seconds</param>
public record ToolCall(
    ToolType Type,
    string Name,
    string Parameters,
    string? Output = null,
    ToolStatus Status = ToolStatus.Running,
    double? ExecutionTime = null
);

/// <summary>
/// Represents a security finding or analysis result
/// </summary>
public enum Severity
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Analysis finding record
/// </summary>
/// <param name="Severity">Severity level of the finding</param>
/// <param name="Title">Short title/description</param>
/// <param name="Location">File location (e.g., "line 23")</param>
public record Finding(
    Severity Severity,
    string Title,
    string? Location = null
);
