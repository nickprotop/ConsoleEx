// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Controls;

/// <summary>
/// Collects the state a control reports about itself.
/// </summary>
/// <remarks>
/// <para>
/// Passed to <see cref="BaseControl.DescribeState"/>. A control writes the handful
/// of values that define what it currently IS — checked, open, the selected index,
/// the text — as opposed to how it is laid out, which the layout tree already
/// reports.
/// </para>
/// <para>
/// Deliberately not reflection. The library is <c>IsAotCompatible</c>, and walking
/// public properties would be the first self-inflicted trim warning in it. Writing
/// the values explicitly is also better on the merits: the output stays stable when
/// a control gains a field, and it carries the three or four values that matter
/// rather than the thirty that exist.
/// </para>
/// <para>
/// Only primitives, because this crosses a wire. Anything else is the caller's job
/// to render into one — a count rather than a collection, a name rather than an
/// object.
/// </para>
/// </remarks>
public interface IStateWriter
{
	/// <summary>Writes a text value. Null is written as null, not omitted.</summary>
	void Write(string name, string? value);

	/// <summary>Writes a whole-number value.</summary>
	void Write(string name, long value);

	/// <summary>Writes a fractional value.</summary>
	void Write(string name, double value);

	/// <summary>Writes a true/false value.</summary>
	void Write(string name, bool value);

	/// <summary>
	/// Writes a value only when it is worth reporting.
	/// </summary>
	/// <remarks>
	/// For the common case of a property that is usually at its default: a control
	/// with nothing unusual to say should produce a short description rather than a
	/// screenful of zeroes and empty strings.
	/// </remarks>
	void WriteIf(bool condition, string name, string? value);
}
