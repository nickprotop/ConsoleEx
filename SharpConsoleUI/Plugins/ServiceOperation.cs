// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Plugins;

/// <summary>
/// Metadata describing a service operation, including its parameters, return type,
/// and documentation. This enables runtime discovery and validation of service operations.
/// </summary>
/// <param name="Name">The operation name (used in Execute calls)</param>
/// <param name="Description">Human-readable description of what the operation does</param>
/// <param name="Parameters">List of parameters the operation accepts</param>
/// <param name="ReturnType">The type returned by the operation, or null for void operations</param>
public record ServiceOperation(
	string Name,
	string Description,
	IReadOnlyList<ServiceParameter> Parameters,
	Type? ReturnType = null
);

/// <summary>
/// Metadata describing a parameter for a service operation.
/// </summary>
/// <param name="Name">The parameter name (used as dictionary key in Execute calls)</param>
/// <param name="Type">The expected type of the parameter value</param>
/// <param name="Required">Whether this parameter is required (true) or optional (false)</param>
/// <param name="DefaultValue">The default value used when the parameter is not provided (for optional parameters)</param>
/// <param name="Description">Human-readable description of what the parameter controls</param>
public record ServiceParameter(
	string Name,
	Type Type,
	bool Required,
	object? DefaultValue = null,
	string? Description = null
);
