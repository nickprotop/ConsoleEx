// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;

namespace SharpConsoleUI.Controls;

/// <summary>
/// The shared names controls use to describe the state they have in common.
/// </summary>
/// <remarks>
/// <para>
/// Every control that reports a selection, a scroll position or a value is
/// reporting the same CONCEPT, and before these helpers each one spelled it
/// differently: <c>selectedIndex</c> here, <c>activeTab</c> there,
/// <c>label</c> against <c>text</c> against <c>input</c> for the same idea. A
/// reader could still make sense of one control at a time, but a tool could
/// not: an inspector cannot offer "scroll this to the end" if it has no idea
/// which controls scroll.
/// </para>
/// <para>
/// So the concepts get names once, here, and controls call these rather than
/// writing keys by hand. A control with something genuinely its own still
/// writes it directly - these cover what is shared, not everything.
/// </para>
/// </remarks>
public static class StateVocabulary
{
	/// <summary>Reports which item of how many is selected.</summary>
	/// <param name="w">The writer.</param>
	/// <param name="selectedIndex">Zero-based index, or -1 for no selection.</param>
	/// <param name="itemCount">How many items there are to choose from.</param>
	/// <remarks>
	/// The count travels with the index because an index alone cannot be read:
	/// "3" says nothing without "of 4", and "of 0" explains a selection that
	/// looks stuck.
	/// </remarks>
	public static void Selection(this IStateWriter w, int selectedIndex, int itemCount)
	{
		ArgumentNullException.ThrowIfNull(w);
		w.Write("selectedIndex", selectedIndex);
		w.Write("itemCount", itemCount);
	}

	/// <summary>Reports where a scrollable control is scrolled to.</summary>
	/// <param name="w">The writer.</param>
	/// <param name="offset">First visible row, column or line.</param>
	/// <param name="viewportExtent">How much is visible at once.</param>
	/// <param name="contentExtent">How much there is in total.</param>
	/// <remarks>
	/// All three, because an offset on its own cannot answer the question that is
	/// actually being asked. "Scrolled to 40" is meaningless; "40 of 120, showing
	/// 20" says at a glance that two thirds remain below. The pair of extents is
	/// also where scroll bugs show first: a content extent that disagrees with
	/// what was painted is the disagreement itself.
	/// </remarks>
	public static void Scroll(this IStateWriter w, int offset, int viewportExtent, int contentExtent)
	{
		ArgumentNullException.ThrowIfNull(w);
		w.Write("scrollOffset", offset);
		w.Write("viewportExtent", viewportExtent);
		w.Write("contentExtent", contentExtent);
	}

	/// <summary>Reports the text a control is showing or holding.</summary>
	/// <param name="w">The writer.</param>
	/// <param name="text">The text, truncated by the caller if it is long.</param>
	/// <remarks>
	/// One name for a button's caption, a checkbox's label and a prompt's input,
	/// because to a reader they are the same question: what does it say?
	/// </remarks>
	public static void Text(this IStateWriter w, string? text)
	{
		ArgumentNullException.ThrowIfNull(w);
		w.Write("text", text);
	}

	/// <summary>Reports a value and the range it sits in.</summary>
	/// <param name="w">The writer.</param>
	/// <param name="value">The current value.</param>
	/// <param name="min">Low end of the range.</param>
	/// <param name="max">High end of the range.</param>
	/// <remarks>
	/// A value without its range cannot be judged: 70 is nearly full on a
	/// percentage and nearly empty on a thousand.
	/// </remarks>
	public static void Value(this IStateWriter w, double value, double min, double max)
	{
		ArgumentNullException.ThrowIfNull(w);
		w.Write("value", value);
		w.Write("min", min);
		w.Write("max", max);
	}

	/// <summary>Reports whether a collapsible control is open.</summary>
	/// <param name="w">The writer.</param>
	/// <param name="expanded">True when its content is showing.</param>
	public static void Expansion(this IStateWriter w, bool expanded)
	{
		ArgumentNullException.ThrowIfNull(w);
		w.Write("expanded", expanded);
	}

	/// <summary>Reports whether a control is being edited, and how much it holds.</summary>
	/// <param name="w">The writer.</param>
	/// <param name="editing">True while the control has an edit in progress.</param>
	/// <param name="length">Characters held, which a caller may prefer to the text itself.</param>
	/// <remarks>
	/// Length rather than content for anything that can grow: an editor holding a
	/// megabyte should not put a megabyte on the wire to say so.
	/// </remarks>
	public static void Editing(this IStateWriter w, bool editing, int length)
	{
		ArgumentNullException.ThrowIfNull(w);
		w.Write("editing", editing);
		w.Write("length", length);
	}

	/// <summary>
	/// Shortens text for a description, so one long value cannot drown the rest.
	/// </summary>
	/// <param name="text">The text to shorten.</param>
	/// <param name="max">Longest result, including the ellipsis.</param>
	/// <returns>The text, or its first characters followed by a single ellipsis.</returns>
	public static string? Shorten(string? text, int max = 64)
	{
		if (text == null || text.Length <= max) return text;
		return string.Concat(text.AsSpan(0, Math.Max(1, max - 1)), "…");
	}
}
