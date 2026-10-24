// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections;
using SharpConsoleUI.Controls;

namespace SharpConsoleUI.Layout;

/// <summary>
/// A live view of one edge of a control host. Holds no controls of its own: every read filters
/// the host's children by edge, and every write goes through the host's own add and remove.
/// </summary>
/// <remarks>
/// A view rather than a collection, deliberately. A surface that kept its own list would be a
/// second source of truth, and the two would drift the moment someone set
/// <see cref="IWindowControl.Dock"/> on a child directly or removed it through the host. Here
/// there is nothing to drift: the surface is a question asked of the child list.
/// </remarks>
internal sealed class DockSurface : IDockSurface
{
	private readonly IControlHost _host;

	internal DockSurface(IControlHost host, Dock edge)
	{
		_host = host;
		Edge = edge;
	}

	public Dock Edge { get; }

	public IReadOnlyList<IWindowControl> Controls
	{
		get
		{
			var list = new List<IWindowControl>();
			foreach (var c in _host.Children)
			{
				if (c.Dock == Edge)
					list.Add(c);
			}
			return list;
		}
	}

	public int Count
	{
		get
		{
			int n = 0;
			foreach (var c in _host.Children)
			{
				if (c.Dock == Edge)
					n++;
			}
			return n;
		}
	}

	public IWindowControl this[int index] => Controls[index];

	public void Add(IWindowControl control)
	{
		ArgumentNullException.ThrowIfNull(control);

		// Set the edge BEFORE adding: the host may lay out on add, and a control that arrives
		// undocked and is re-docked a moment later would be arranged twice, in two places.
		control.Dock = Edge;

		if (!_host.Children.Contains(control))
			_host.AddControl(control);
	}

	public void Insert(int index, IWindowControl control)
	{
		ArgumentNullException.ThrowIfNull(control);

		var onEdge = Controls;
		if (index < 0 || index > onEdge.Count)
			throw new ArgumentOutOfRangeException(nameof(index),
				$"Index {index} is outside this edge, which holds {onEdge.Count} controls.");

		control.Dock = Edge;

		// Appending is the common case and needs no reordering.
		if (index == onEdge.Count)
		{
			if (!_host.Children.Contains(control))
				_host.AddControl(control);
			return;
		}

		// Otherwise rebuild the host's list with the control in position. Order across the whole
		// list is semantic for docking - a top-docked child declared before a left-docked one
		// spans the full width - so the other children must keep their relative order exactly.
		var target = onEdge[index];
		var rebuilt = new List<IWindowControl>();
		foreach (var c in _host.Children)
		{
			if (ReferenceEquals(c, control))
				continue;   // moving an existing child rather than adding a new one
			if (ReferenceEquals(c, target))
				rebuilt.Add(control);
			rebuilt.Add(c);
		}

		_host.ClearControls();
		foreach (var c in rebuilt)
			_host.AddControl(c);
	}

	public bool Remove(IWindowControl control)
	{
		if (control == null || control.Dock != Edge || !_host.Children.Contains(control))
			return false;

		_host.RemoveControl(control);
		return true;
	}

	public void Clear()
	{
		foreach (var c in Controls)
			_host.RemoveControl(c);
	}

	public bool Contains(IWindowControl control)
		=> control != null && control.Dock == Edge && _host.Children.Contains(control);

	public IEnumerator<IWindowControl> GetEnumerator() => Controls.GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
