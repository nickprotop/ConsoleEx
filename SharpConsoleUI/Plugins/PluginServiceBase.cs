// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Plugins;

/// <summary>
/// Abstract base class for implementing service plugins. This class simplifies
/// the implementation of IPluginService by providing helper methods for registering
/// operations and handling parameter extraction.
/// </summary>
/// <remarks>
/// Plugin authors can inherit from this class and use the RegisterOperation methods
/// in their constructor or initialization code to define available operations.
/// The Execute method is automatically implemented to dispatch to registered handlers.
///
/// Example:
/// <code>
/// public class MyService : PluginServiceBase
/// {
///     public override string ServiceName => "MyService";
///     public override string Description => "My custom service";
///
///     public MyService()
///     {
///         RegisterOperation("GetValue", "Gets a value", () => GetValue());
///         RegisterOperation("SetValue", "Sets a value",
///             new[] { new ServiceParameter("value", typeof(int), true) },
///             (p) => SetValue(GetParameter&lt;int&gt;(p, "value")));
///     }
///
///     private int GetValue() => 42;
///     private void SetValue(int value) { /* ... */ }
/// }
/// </code>
/// </remarks>
public abstract class PluginServiceBase : IPluginService
{
	private readonly Dictionary<string, (Func<Dictionary<string, object>?, object?> Handler, ServiceOperation Metadata)> _operations = new();

	/// <inheritdoc />
	public abstract string ServiceName { get; }

	/// <inheritdoc />
	public abstract string Description { get; }

	/// <inheritdoc />
	public IReadOnlyList<ServiceOperation> GetAvailableOperations()
	{
		return _operations.Values.Select(v => v.Metadata).ToList();
	}

	/// <inheritdoc />
	public object? Execute(string operationName, Dictionary<string, object>? parameters = null)
	{
		if (!_operations.TryGetValue(operationName, out var operation))
		{
			throw new InvalidOperationException($"Unknown operation: {operationName}. Available operations: {string.Join(", ", _operations.Keys)}");
		}

		return operation.Handler(parameters);
	}

	/// <summary>
	/// Registers an operation with no parameters and no return value.
	/// </summary>
	/// <param name="name">The operation name</param>
	/// <param name="description">Human-readable description</param>
	/// <param name="handler">The handler function</param>
	protected void RegisterOperation(
		string name,
		string description,
		Action handler)
	{
		var metadata = new ServiceOperation(name, description, Array.Empty<ServiceParameter>(), null);
		_operations[name] = (_ => { handler(); return null; }, metadata);
	}

	/// <summary>
	/// Registers an operation with no parameters that returns a value.
	/// </summary>
	/// <typeparam name="TResult">The return type</typeparam>
	/// <param name="name">The operation name</param>
	/// <param name="description">Human-readable description</param>
	/// <param name="handler">The handler function</param>
	protected void RegisterOperation<TResult>(
		string name,
		string description,
		Func<TResult> handler)
	{
		var metadata = new ServiceOperation(name, description, Array.Empty<ServiceParameter>(), typeof(TResult));
		_operations[name] = (_ => handler(), metadata);
	}

	/// <summary>
	/// Registers an operation with parameters and an optional return value.
	/// The handler receives a Dictionary of parameters and is responsible for
	/// extracting and validating them.
	/// </summary>
	/// <param name="name">The operation name</param>
	/// <param name="description">Human-readable description</param>
	/// <param name="parameters">Parameter metadata</param>
	/// <param name="handler">The handler function that receives parameters</param>
	/// <param name="returnType">The return type, or null for void operations</param>
	protected void RegisterOperation(
		string name,
		string description,
		IReadOnlyList<ServiceParameter> parameters,
		Func<Dictionary<string, object>?, object?> handler,
		Type? returnType = null)
	{
		var metadata = new ServiceOperation(name, description, parameters, returnType);
		_operations[name] = (handler, metadata);
	}

	/// <summary>
	/// Helper method to extract a required parameter from the parameters dictionary.
	/// Throws an exception if the parameter is missing or has the wrong type.
	/// </summary>
	/// <typeparam name="T">The expected parameter type</typeparam>
	/// <param name="parameters">The parameters dictionary</param>
	/// <param name="name">The parameter name</param>
	/// <returns>The parameter value</returns>
	/// <exception cref="InvalidOperationException">Thrown if the parameter is missing or invalid</exception>
	protected static T GetParameter<T>(Dictionary<string, object>? parameters, string name)
	{
		if (parameters == null || !parameters.TryGetValue(name, out var value))
		{
			throw new InvalidOperationException($"Required parameter '{name}' is missing");
		}

		if (value is T typedValue)
		{
			return typedValue;
		}

		// Try to convert common cases
		try
		{
			return (T)Convert.ChangeType(value, typeof(T));
		}
		catch
		{
			throw new InvalidOperationException($"Parameter '{name}' has invalid type. Expected {typeof(T).Name}, got {value?.GetType().Name ?? "null"}");
		}
	}

	/// <summary>
	/// Helper method to extract an optional parameter from the parameters dictionary.
	/// Returns the default value if the parameter is missing.
	/// </summary>
	/// <typeparam name="T">The expected parameter type</typeparam>
	/// <param name="parameters">The parameters dictionary</param>
	/// <param name="name">The parameter name</param>
	/// <param name="defaultValue">The default value to return if the parameter is missing</param>
	/// <returns>The parameter value or the default value</returns>
	protected static T GetParameter<T>(Dictionary<string, object>? parameters, string name, T defaultValue)
	{
		if (parameters == null || !parameters.TryGetValue(name, out var value))
		{
			return defaultValue;
		}

		if (value is T typedValue)
		{
			return typedValue;
		}

		// Try to convert common cases
		try
		{
			return (T)Convert.ChangeType(value, typeof(T));
		}
		catch
		{
			return defaultValue;
		}
	}
}
