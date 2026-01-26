namespace SharpConsoleUI.Plugins;

/// <summary>
/// Plugin action provider using reflection-free pattern.
/// Allows plugins from external DLLs to export Start menu actions without shared interfaces.
/// </summary>
public interface IPluginActionProvider
{
    /// <summary>
    /// Gets the unique name for this action provider (usually plugin name).
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Gets all available actions this provider exports.
    /// </summary>
    IReadOnlyList<ActionDescriptor> GetAvailableActions();

    /// <summary>
    /// Executes an action by name.
    /// </summary>
    /// <param name="actionName">Name of the action to execute</param>
    /// <param name="context">Execution context (window system reference, etc.)</param>
    void ExecuteAction(string actionName, Dictionary<string, object>? context = null);
}

/// <summary>
/// Describes an available action (metadata only, no delegates).
/// </summary>
public record ActionDescriptor(
    string Name,
    string Description,
    IReadOnlyList<ActionParameter>? Parameters = null
);

/// <summary>
/// Describes a parameter for an action.
/// </summary>
public record ActionParameter(
    string Name,
    Type Type,
    bool Required,
    object? DefaultValue = null,
    string? Description = null
);
