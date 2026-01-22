// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Plugins;

/// <summary>
/// Base interface for all service plugins that can be invoked without reflection.
/// Services expose operations that can be discovered and invoked dynamically through
/// a convention-based Execute method.
/// </summary>
/// <remarks>
/// This interface enables a reflection-free service plugin pattern where external DLLs
/// don't need to share specific interfaces with the host application. Services define
/// their own operations with rich metadata for discoverability, and the host invokes
/// them through a generic Execute method with string-based operation names and
/// dictionary-based parameters.
///
/// Example usage:
/// <code>
/// var service = windowSystem.GetPluginService("Diagnostics");
/// long memory = (long)service.Execute("GetMemoryUsage");
///
/// var report = (string)service.Execute("GetDetailedReport", new Dictionary&lt;string, object&gt;
/// {
///     ["includeGC"] = true,
///     ["includeThreads"] = false
/// });
/// </code>
/// </remarks>
public interface IPluginService
{
	/// <summary>
	/// Gets the unique name of this service. This is used to retrieve the service
	/// from the plugin system.
	/// </summary>
	/// <example>"Diagnostics", "Logger", "Authentication"</example>
	string ServiceName { get; }

	/// <summary>
	/// Gets a human-readable description of what this service provides.
	/// </summary>
	string Description { get; }

	/// <summary>
	/// Gets the list of operations this service supports, with full metadata about
	/// parameters, return types, and descriptions. This enables runtime discovery
	/// and self-documenting service interfaces.
	/// </summary>
	/// <returns>A read-only list of operation metadata</returns>
	IReadOnlyList<ServiceOperation> GetAvailableOperations();

	/// <summary>
	/// Executes a named operation with optional parameters.
	/// </summary>
	/// <param name="operationName">The name of the operation to execute</param>
	/// <param name="parameters">Optional dictionary of parameter name/value pairs</param>
	/// <returns>The result of the operation, or null if the operation returns void</returns>
	/// <exception cref="InvalidOperationException">Thrown if the operation name is unknown or parameters are invalid</exception>
	/// <remarks>
	/// Parameter values should match the types declared in the operation metadata.
	/// The caller is responsible for casting the return value to the expected type
	/// based on the operation metadata.
	/// </remarks>
	object? Execute(string operationName, Dictionary<string, object>? parameters = null);
}
