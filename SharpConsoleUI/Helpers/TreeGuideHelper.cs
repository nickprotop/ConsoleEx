// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;

namespace SharpConsoleUI.Helpers;

/// <summary>
/// Draws what a tree shows in front of a node: the guide lines connecting it to its ancestors, and
/// the indicator that says whether it is expanded.
/// </summary>
/// <remarks>
/// <para>
/// Shared so every tree-shaped control draws its hierarchy the same way. <see cref="TreeControl"/>
/// draws through it, and so can any control or derived table showing nested rows — including one
/// that overrides how its guides look, which can start from these pieces.
/// </para>
/// <para>
/// The prefix is plain text with no markup, so a caller can measure it with
/// <see cref="UnicodeWidth.GetStringWidth(string)"/> and must escape it before putting it into
/// markup. The guide glyphs are box-drawing or ASCII characters, all one cell wide.
/// </para>
/// </remarks>
public static class TreeGuideHelper
{
	/// <summary>The glyphs a guide style draws with.</summary>
	/// <param name="guide">The guide style.</param>
	public static TreeGuideChars GetGuideChars(TreeGuide guide) => guide switch
	{
		TreeGuide.Ascii => new TreeGuideChars("\\", "+", "|", "-"),
		TreeGuide.DoubleLine => new TreeGuideChars("╚", "╠", "║", "═"),
		TreeGuide.BoldLine => new TreeGuideChars("┗", "┣", "┃", "━"),
		_ => new TreeGuideChars("└", "├", "│", "─"),
	};

	/// <summary>
	/// Appends the guide prefix of a node: a continuation line or a blank for each ancestor level,
	/// then the connector to the node itself.
	/// </summary>
	/// <param name="builder">Where the prefix is appended.</param>
	/// <param name="isLastAtLevel">
	/// One entry per level below the roots, outermost first: whether the node's ancestor at that level
	/// — and, at the last entry, the node itself — is the last of its siblings. Empty for a root,
	/// which gets no prefix.
	/// </param>
	/// <param name="chars">The guide glyphs.</param>
	/// <param name="indent">What follows each ancestor level's continuation line.</param>
	/// <remarks>
	/// An ancestor that is the last of its siblings has nothing below it to connect to, so its level
	/// is a blank rather than a vertical line.
	/// </remarks>
	public static void AppendPrefix(StringBuilder builder, IReadOnlyList<bool> isLastAtLevel, TreeGuideChars chars, string indent)
	{
		int depth = isLastAtLevel.Count;
		if (depth == 0) return;

		for (int i = 0; i < depth - 1; i++)
		{
			builder.Append(isLastAtLevel[i] ? " " : chars.Vertical);
			builder.Append(indent);
		}

		builder.Append(isLastAtLevel[depth - 1] ? chars.Corner : chars.Tee);
		builder.Append(chars.Horizontal);
		builder.Append(' ');
	}

	/// <summary>The indicator drawn in front of a node that has children, as plain text.</summary>
	/// <param name="isExpanded">Whether the node is expanded.</param>
	/// <returns><see cref="ControlDefaults.TreeExpandedIndicator"/> or <see cref="ControlDefaults.TreeCollapsedIndicator"/>.</returns>
	public static string GetExpanderText(bool isExpanded)
		=> isExpanded ? ControlDefaults.TreeExpandedIndicator : ControlDefaults.TreeCollapsedIndicator;

	/// <summary>
	/// The cells the indicator takes, which is also the span a click toggles the node on.
	/// </summary>
	public static int ExpanderWidth => UnicodeWidth.GetStringWidth(ControlDefaults.TreeExpandedIndicator);
}

/// <summary>
/// The glyphs a tree guide style draws with.
/// </summary>
/// <param name="Corner">The connector to a node that is the last of its siblings.</param>
/// <param name="Tee">The connector to a node with siblings below it.</param>
/// <param name="Vertical">The continuation line past an ancestor with siblings below it.</param>
/// <param name="Horizontal">The line from a connector to the node.</param>
public readonly record struct TreeGuideChars(string Corner, string Tee, string Vertical, string Horizontal);
