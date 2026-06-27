// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Layout
{
	/// <summary>
	/// Marks a control that, when placed with <see cref="VerticalAlignment.Fill"/> inside a scroll
	/// viewport (e.g. <see cref="Controls.ScrollablePanelControl"/>), has a hard minimum height it
	/// cannot shrink below and does <b>not</b> scroll its own content internally — so when the
	/// viewport is shorter than that minimum the <i>hosting</i> panel must scroll on its behalf.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A Fill child is normally measured against its allotted Fill slot (the viewport share), and a
	/// host with a short viewport therefore never learns the child needs more room. That is correct
	/// for the common cases: a content-shorter-than-slot control (a label) simply fills its slot, and
	/// a self-scrolling container (a nested <see cref="Controls.ScrollablePanelControl"/>,
	/// <see cref="Controls.TableControl"/>) deliberately caps itself to the viewport and scrolls its
	/// own content — it must NOT be measured for its full content, or the outer panel would try to
	/// scroll it instead of letting it scroll internally.
	/// </para>
	/// <para>
	/// A control like <see cref="Controls.GridControl"/> is the exception: its fixed (<c>Cells</c>)
	/// rows neither shrink nor scroll, so when the viewport is too short the rows would squash (clip)
	/// with no way to reveal them. Implementing this marker tells the scroll viewport to measure the
	/// child <b>unbounded</b> — discovering its true minimum height — and then take the larger of that
	/// minimum and the Fill slot. When there is room the Fill slot wins (the child fills, unchanged);
	/// when the viewport is shorter than the minimum, the minimum wins and the panel shows a scrollbar
	/// and scrolls to reveal the clipped rows.
	/// </para>
	/// <para>
	/// This is a pure behavioural marker — it carries no members. The minimum itself is whatever the
	/// control reports from its measure pass when given an unbounded height.
	/// </para>
	/// </remarks>
	public interface IFillReportsMinimumHeight
	{
	}
}
