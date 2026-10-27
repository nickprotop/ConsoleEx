// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace Cxdbg.Session;

/// <summary>
/// Two nodes are the same control when they have the same id.
/// </summary>
/// <remarks>
/// The binding uses this to decide whether a node in a fresh poll is one it
/// already has a row for. Without it the rows would be matched by reference and
/// every poll would look like a complete replacement, taking the selection and
/// the expansion with it.
/// </remarks>
internal sealed class ControlNodeComparer : IEqualityComparer<ControlNode>
{
	internal static ControlNodeComparer Instance { get; } = new();

	public bool Equals(ControlNode? a, ControlNode? b)
	{
		if (ReferenceEquals(a, b)) return true;
		if (a == null || b == null) return false;

		// Scaffolding has no id, so it falls back to reference identity - the
		// model gives it the same object across polls anyway, and nothing can
		// be asked about it.
		if (a.Id.Length == 0 || b.Id.Length == 0) return false;

		return string.Equals(a.Id, b.Id, StringComparison.Ordinal);
	}

	public int GetHashCode(ControlNode node) =>
		node.Id.Length == 0
			? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(node)
			: StringComparer.Ordinal.GetHashCode(node.Id);
}
