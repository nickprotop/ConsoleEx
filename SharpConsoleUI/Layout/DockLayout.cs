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
/// Allocates edges: each docked child claims a strip from the remaining rectangle in declaration
/// order, and undocked children share whatever is left.
/// </summary>
/// <remarks>
/// <para>
/// The plain container form of the allocation <see cref="DockRootLayout"/> performs, without the
/// scrolling body a window root needs. A grid cell holds one control, so "docking inside a grid
/// cell" means putting a <see cref="Controls.DockPanelControl"/> in that cell - this is the
/// layout behind it.
/// </para>
/// <para>
/// Far edges (<see cref="Dock.Bottom"/>, <see cref="Dock.Right"/>) are allocated in reverse
/// within a run of the same edge, so the first child declared ends up nearest the content. See
/// <see cref="DockRootLayout"/> for why this departs from WPF.
/// </para>
/// </remarks>
public sealed class DockLayout : ILayoutContainer
{
	/// <summary>
	/// Whether the last undocked child expands to fill the remaining rectangle. When false it
	/// takes its desired size and the remainder stays empty.
	/// </summary>
	public bool LastChildFill { get; set; } = true;

	/// <summary>
	/// Orders children for allocation: same-edge runs on a far edge are reversed, everything else
	/// keeps declaration order.
	/// </summary>
	internal static List<LayoutNode> AllocationOrder(IReadOnlyList<LayoutNode> children)
	{
		var ordered = new List<LayoutNode>(children.Count);
		var run = new List<LayoutNode>();
		Dock runEdge = Dock.None;

		void Flush()
		{
			for (int i = run.Count - 1; i >= 0; i--)
				ordered.Add(run[i]);
			run.Clear();
			runEdge = Dock.None;
		}

		foreach (var child in children)
		{
			var d = child.Control?.Dock ?? Dock.None;
			bool isFar = d == Dock.Bottom || d == Dock.Right;

			if (isFar && d == runEdge)
			{
				run.Add(child);
				continue;
			}

			Flush();

			if (isFar)
			{
				runEdge = d;
				run.Add(child);
			}
			else
			{
				ordered.Add(child);
			}
		}

		Flush();
		return ordered;
	}

	/// <inheritdoc/>
	public LayoutSize MeasureChildren(LayoutNode node, LayoutConstraints constraints)
	{
		int remainingW = constraints.MaxWidth;
		int remainingH = constraints.MaxHeight;
		int usedW = 0, usedH = 0;

		foreach (var child in AllocationOrder(node.Children))
		{
			if (!child.IsVisible || child.Control == null)
				continue;

			var size = child.Measure(new LayoutConstraints(0, remainingW, 0, remainingH));

			switch (child.Control.Dock)
			{
				case Dock.Left:
				case Dock.Right:
					int w = Math.Min(size.Width, remainingW);
					remainingW -= w;
					usedW += w;
					usedH = Math.Max(usedH, size.Height);
					break;

				case Dock.Top:
				case Dock.Bottom:
					int h = Math.Min(size.Height, remainingH);
					remainingH -= h;
					usedH += h;
					usedW = Math.Max(usedW, size.Width);
					break;

				default:
					usedW = Math.Max(usedW, usedW + size.Width);
					usedH = Math.Max(usedH, usedH + size.Height);
					break;
			}
		}

		// A dock panel fills what it is given when anything fills, and otherwise reports the space
		// its children actually consumed.
		return LastChildFill
			? new LayoutSize(constraints.MaxWidth, constraints.MaxHeight)
			: new LayoutSize(Math.Min(usedW, constraints.MaxWidth), Math.Min(usedH, constraints.MaxHeight));
	}

	/// <inheritdoc/>
	public void ArrangeChildren(LayoutNode node, LayoutRect finalRect)
	{
		int x = 0, y = 0;
		int w = finalRect.Width, h = finalRect.Height;

		var ordered = AllocationOrder(node.Children);

		// The last undocked child gets the remainder; earlier undocked ones take their own size.
		LayoutNode? filler = null;
		if (LastChildFill)
		{
			for (int i = ordered.Count - 1; i >= 0; i--)
			{
				var c = ordered[i];
				if (c.IsVisible && c.Control != null && c.Control.Dock == Dock.None)
				{
					filler = c;
					break;
				}
			}
		}

		foreach (var child in ordered)
		{
			if (!child.IsVisible || child.Control == null)
			{
				child.Arrange(LayoutRect.Empty);
				continue;
			}

			if (ReferenceEquals(child, filler))
				continue;   // arranged last, into whatever is left

			int cw = child.DesiredSize.Width;
			int ch = child.DesiredSize.Height;

			switch (child.Control.Dock)
			{
				case Dock.Left:
					cw = Math.Min(cw, w);
					child.Arrange(new LayoutRect(x, y, cw, h));
					x += cw; w -= cw;
					break;

				case Dock.Right:
					cw = Math.Min(cw, w);
					child.Arrange(new LayoutRect(x + w - cw, y, cw, h));
					w -= cw;
					break;

				case Dock.Top:
					ch = Math.Min(ch, h);
					child.Arrange(new LayoutRect(x, y, w, ch));
					y += ch; h -= ch;
					break;

				case Dock.Bottom:
					ch = Math.Min(ch, h);
					child.Arrange(new LayoutRect(x, y + h - ch, w, ch));
					h -= ch;
					break;

				default:
					// An undocked child that is not the filler stacks from the top of what is left.
					ch = Math.Min(ch, h);
					child.Arrange(new LayoutRect(x, y, w, ch));
					y += ch; h -= ch;
					break;
			}
		}

		filler?.Arrange(new LayoutRect(x, y, w, h));
	}
}
