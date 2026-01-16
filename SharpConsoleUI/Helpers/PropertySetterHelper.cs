using SharpConsoleUI.Controls;
using Spectre.Console;

namespace SharpConsoleUI.Helpers;

/// <summary>
/// Helper class for property setters with validation and invalidation.
/// Eliminates 200-250 lines of duplicated Width/Height/Color property patterns across 14+ controls.
/// </summary>
public static class PropertySetterHelper
{
	/// <summary>
	/// Sets a property value with optional validation and automatic container invalidation.
	/// </summary>
	/// <typeparam name="T">Property type</typeparam>
	/// <param name="field">Reference to the backing field</param>
	/// <param name="value">New value to set</param>
	/// <param name="container">Container to invalidate (if value changed)</param>
	/// <param name="validate">Optional validation function to transform/validate the value</param>
	/// <param name="invalidate">Whether to invalidate container on change (default true)</param>
	/// <returns>True if value changed, false otherwise</returns>
	public static bool SetProperty<T>(
		ref T field,
		T value,
		IContainer? container,
		Func<T, T>? validate = null,
		bool invalidate = true)
	{
		// Apply validation if provided
		T validatedValue = validate != null ? validate(value) : value;

		// Check if value actually changed
		if (EqualityComparer<T>.Default.Equals(field, validatedValue))
			return false;

		// Update field
		field = validatedValue;

		// Invalidate container if requested
		if (invalidate && container != null)
		{
			container.Invalidate(true);
		}

		return true;
	}

	/// <summary>
	/// Sets a nullable integer dimension property (Width or Height) with validation.
	/// Ensures value is non-negative if provided.
	/// </summary>
	/// <param name="field">Reference to the Width/Height backing field</param>
	/// <param name="value">New dimension value (null = auto-size)</param>
	/// <param name="container">Container to invalidate if changed</param>
	/// <returns>True if value changed, false otherwise</returns>
	public static bool SetDimensionProperty(
		ref int? field,
		int? value,
		IContainer? container)
	{
		return SetProperty(
			ref field,
			value,
			container,
			validate: v => v.HasValue ? Math.Max(0, v.Value) : v);
	}

	/// <summary>
	/// Sets a nullable Color property with automatic invalidation.
	/// </summary>
	/// <param name="field">Reference to the Color backing field</param>
	/// <param name="value">New color value (null = use default/parent color)</param>
	/// <param name="container">Container to invalidate if changed</param>
	/// <returns>True if value changed, false otherwise</returns>
	public static bool SetColorProperty(
		ref Color? field,
		Color? value,
		IContainer? container)
	{
		return SetProperty(ref field, value, container);
	}

	/// <summary>
	/// Sets a boolean property with automatic invalidation.
	/// </summary>
	/// <param name="field">Reference to the boolean backing field</param>
	/// <param name="value">New boolean value</param>
	/// <param name="container">Container to invalidate if changed</param>
	/// <returns>True if value changed, false otherwise</returns>
	public static bool SetBoolProperty(
		ref bool field,
		bool value,
		IContainer? container)
	{
		return SetProperty(ref field, value, container);
	}

	/// <summary>
	/// Sets a string property with automatic invalidation.
	/// </summary>
	/// <param name="field">Reference to the string backing field</param>
	/// <param name="value">New string value</param>
	/// <param name="container">Container to invalidate if changed</param>
	/// <param name="nullToEmpty">If true, converts null to empty string (default false)</param>
	/// <returns>True if value changed, false otherwise</returns>
	public static bool SetStringProperty(
		ref string field,
		string value,
		IContainer? container,
		bool nullToEmpty = false)
	{
		return SetProperty(
			ref field,
			value,
			container,
			validate: nullToEmpty ? (v => v ?? string.Empty) : null);
	}

	/// <summary>
	/// Sets an enum property with automatic invalidation.
	/// </summary>
	/// <typeparam name="TEnum">Enum type</typeparam>
	/// <param name="field">Reference to the enum backing field</param>
	/// <param name="value">New enum value</param>
	/// <param name="container">Container to invalidate if changed</param>
	/// <returns>True if value changed, false otherwise</returns>
	public static bool SetEnumProperty<TEnum>(
		ref TEnum field,
		TEnum value,
		IContainer? container) where TEnum : struct, Enum
	{
		return SetProperty(ref field, value, container);
	}
}
