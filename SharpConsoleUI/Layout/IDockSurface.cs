// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;

namespace SharpConsoleUI.Layout;

/// <summary>
/// One edge of a docking container, as a collection you can add to, remove from and enumerate.
/// </summary>
/// <remarks>
/// <para>
/// Docking is otherwise expressed by setting <see cref="IWindowControl.Dock"/> on a child and
/// adding it to the container, which works but reads backwards for anything structural: to find
/// what is in the footer you would filter the whole child list by edge, and to replace the footer
/// you would remove those children by identity. A surface says what it is.
/// </para>
/// <para>
/// Both routes mutate the same child list - a control added through a surface IS an ordinary
/// child with its <see cref="IWindowControl.Dock"/> set, and one added directly with
/// <c>Dock = Dock.Top</c> appears in the top surface. There is no second collection to keep in
/// step.
/// </para>
/// </remarks>
public interface IDockSurface : IEnumerable<IWindowControl>
{
	/// <summary>The edge this surface represents.</summary>
	Dock Edge { get; }

	/// <summary>The controls on this edge, in declaration order.</summary>
	IReadOnlyList<IWindowControl> Controls { get; }

	/// <summary>How many controls are on this edge.</summary>
	int Count { get; }

	/// <summary>
	/// Adds a control to this edge, setting its <see cref="IWindowControl.Dock"/> to
	/// <see cref="Edge"/>.
	/// </summary>
	void Add(IWindowControl control);

	/// <summary>
	/// Inserts a control at <paramref name="index"/> within this edge. Order is semantic - it
	/// decides which control sits nearest the content.
	/// </summary>
	void Insert(int index, IWindowControl control);

	/// <summary>
	/// Removes a control from this edge. Returns false when the control is not on it, including
	/// when it belongs to the container but is docked elsewhere.
	/// </summary>
	bool Remove(IWindowControl control);

	/// <summary>Removes every control on this edge from the container.</summary>
	void Clear();

	/// <summary>Whether this control is on this edge.</summary>
	bool Contains(IWindowControl control);

	/// <summary>The control at <paramref name="index"/> within this edge.</summary>
	IWindowControl this[int index] { get; }
}

/// <summary>
/// A container that exposes its four edges as surfaces.
/// </summary>
public interface IDockSurfaceProvider
{
	/// <summary>Controls docked to the left, in declaration order.</summary>
	IDockSurface Left { get; }

	/// <summary>Controls docked to the top, in declaration order.</summary>
	IDockSurface Top { get; }

	/// <summary>Controls docked to the right, in declaration order.</summary>
	IDockSurface Right { get; }

	/// <summary>Controls docked to the bottom, in declaration order.</summary>
	IDockSurface Bottom { get; }

	/// <summary>Undocked controls, which share whatever space the edges leave.</summary>
	IDockSurface Body { get; }

	/// <summary>The surface for a given edge, with <see cref="Dock.None"/> giving the body.</summary>
	IDockSurface Surface(Dock edge);
}
