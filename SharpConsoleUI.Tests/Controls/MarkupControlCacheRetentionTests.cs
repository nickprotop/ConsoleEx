// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Runtime.CompilerServices;
using System.Text;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

[Collection("ParseCounter")]
public class MarkupControlCacheRetentionTests
{
	private static readonly int[] Widths = [40, 38, int.MaxValue];

	[Theory]
	[InlineData(30)]
	[InlineData(200)]
	public void StreamingMarkdown_ReleasesIntermediateDocuments(int updates)
	{
		var control = new MarkupControl([]) { Wrap = true };
		var text = new StringBuilder("# Response\n\n");
		var obsolete = new List<WeakReference>();
		for (int i = 0; i < updates; i++)
		{
			text.Append("- fragment ").Append(i).Append(" with some wrapped text\n");
			obsolete.AddRange(SetMarkdownAndCapture(control, text.ToString(), Widths));
		}

		control.SetMarkdown(text.Append("\nCompleted.").ToString());
		foreach (int width in Widths) control.EnsureParsedForTest(width);

		AssertCollected(obsolete);
		Assert.Contains("Completed.", RenderedText(control.EnsureParsedForTest(40)));
		GC.KeepAlive(control);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(40)]
	[InlineData(int.MaxValue)]
	public void ReplacingSingleGroup_ReleasesOldRowsAndSource(int width)
	{
		var control = new MarkupControl([]);
		var obsolete = SetMarkdownAndCapture(control, "# Previous\n\nA previous document.", [width]);
		control.SetMarkdown("# Current\n\nA current document.");
		control.EnsureParsedForTest(width);

		AssertCollected(obsolete);
		GC.KeepAlive(control);
	}

	[Fact]
	public void UnchangedGroups_ReuseAllWidthsButReleaseOldWholeDocumentResults()
	{
		var control = new MarkupControl(["unchanged group", "old group"]);
		var rows = new Dictionary<int, List<Cell>>();
		var obsolete = new List<WeakReference>();
		foreach (int width in Widths)
		{
			var captured = CaptureUnchangedRow(control, width);
			rows.Add(width, captured.row);
			obsolete.Add(captured.document);
		}

		// Equal text with a different string reference must still reuse the group's parse.
		control.SetContent([new string("unchanged group".ToCharArray()), "new group"]);
		long before = MarkupControl.TotalParseCount;
		foreach (int width in Widths)
		{
			var parsed = control.EnsureParsedForTest(width);
			Assert.Same(rows[width], parsed.Rows[0]);
			Assert.Contains("new group", RenderedText(parsed));
		}
		Assert.Equal(Widths.Length, MarkupControl.TotalParseCount - before);

		AssertCollected(obsolete);
		GC.KeepAlive(control);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void RemovingGroups_ReleasesRowsAtWidthsNotRequestedAgain(bool empty)
	{
		var control = new MarkupControl(["keep", "remove"]);
		var obsolete = CaptureGroupRows(control, sourceIndex: 1, Widths);
		control.SetContent(empty ? [] : ["keep"]);
		control.EnsureParsedForTest(40);

		AssertCollected(obsolete);
		GC.KeepAlive(control);
	}

	[Fact]
	public void AppendingToOpenMarkdown_ReleasesPreviousCoalescedGroup()
	{
		var control = new MarkupControl(["[markdown]# Stream", "- first"]);
		var obsolete = CaptureGroupRows(control, sourceIndex: 0, Widths);
		control.AppendLine("- second[/]");
		foreach (int width in Widths)
		{
			var parsed = control.EnsureParsedForTest(width);
			Assert.All(parsed.RowSourceLine, source => Assert.Equal(0, source));
			Assert.Contains("second", RenderedText(parsed));
		}

		AssertCollected(obsolete);
		GC.KeepAlive(control);
	}

	[Fact]
	public void RepeatedContentVersions_KeepStaticWidthVariantsBesideDynamicMarkup()
	{
		var control = new MarkupControl(["static", "[gradient=red→blue]dynamic[/]"]);
		var rows = Widths.ToDictionary(width => width, width => control.EnsureParsedForTest(width).Rows[0]);
		for (int update = 0; update < 10; update++)
		{
			control.SetContent(["static", $"[gradient=red→blue]dynamic {update}[/]"]);
			long before = MarkupControl.TotalParseCount;
			foreach (int width in Widths)
				Assert.Same(rows[width], control.EnsureParsedForTest(width).Rows[0]);
			Assert.Equal(Widths.Length, MarkupControl.TotalParseCount - before);
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void NestedStreamingControl_ReleasesOldRowsAndSurvivesCollapse(bool collapseBeforeFinalRender)
	{
		var system = TestWindowSystemBuilder.CreateTestSystemWithoutDiagnostics();
		var window = new Window(system) { Width = 50, Height = 12 };
		var control = new MarkupControl([]) { Wrap = true, HorizontalAlignment = HorizontalAlignment.Stretch, EnableSelection = true };
		var scroll = SharpConsoleUI.Builders.Controls.ScrollablePanel().WithScrollbar(true).Build();
		scroll.AddControl(control);
		var panel = new CollapsiblePanel { Title = "Reasoning" };
		panel.AddControl(scroll);
		window.AddControl(panel);
		system.AddWindow(window);

		var text = new StringBuilder("# Stream\n\n");
		var obsolete = new List<WeakReference>();
		for (int update = 0; update < 20; update++)
		{
			text.Append("- fragment ").Append(update).Append('\n');
			obsolete.AddRange(RenderAndCapture(control, window, text.ToString()));
		}
		control.SetMarkdown(text.Append("\nFinal [link](https://example.com).\n").ToString());
		if (!collapseBeforeFinalRender) window.RenderAndGetVisibleContent();
		panel.Collapse();
		window.RenderAndGetVisibleContent();
		Assert.False(panel.IsExpanded);

		if (!collapseBeforeFinalRender) AssertCollected(obsolete);
		panel.Expand();
		window.RenderAndGetVisibleContent();
		window.Invalidate(Invalidation.Relayout);
		window.RenderAndGetVisibleContent();
		AssertCollected(obsolete);
		string copied = control.CopyToClipboardText();
		Assert.Contains("fragment 19", copied);
		Assert.Contains("Final link.", copied);
		Assert.True(scroll.CanScrollDown);
		GC.KeepAlive(control);
		GC.KeepAlive(window);
	}

	[Fact]
	public void FailedParse_DoesNotPreventCleanupOnRetry()
	{
		var control = new MarkupControl([]);
		var obsolete = SetMarkdownAndCapture(control, "previous document", Widths);
		var highlighter = new ThrowOnceHighlighter();
		control.MarkdownStyle = new MarkdownStyle { CodeHighlighters = new Dictionary<string, ISyntaxHighlighter> { ["test"] = highlighter } };
		control.SetMarkdown("```test\ncurrent\n```");
		Assert.Throws<InvalidOperationException>(() => control.EnsureParsedForTest(40));
		Assert.Contains("current", RenderedText(control.EnsureParsedForTest(40)));

		AssertCollected(obsolete);
		GC.KeepAlive(control);
	}

	// Keep strong references out of the caller's JIT frame during collection, including in Debug builds.
	[MethodImpl(MethodImplOptions.NoInlining)]
	private static List<WeakReference> SetMarkdownAndCapture(MarkupControl control, string text, int[] widths)
	{
		control.SetMarkdown(text);
		var references = CaptureGroupRows(control, 0, widths);
		references.Add(new WeakReference(control.GetContentLinesForTest()[0]));
		return references;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static List<WeakReference> CaptureGroupRows(MarkupControl control, int sourceIndex, int[] widths)
	{
		var references = new List<WeakReference>();
		foreach (int width in widths)
		{
			var parsed = control.EnsureParsedForTest(width);
			references.Add(new WeakReference(parsed));
			for (int row = 0; row < parsed.Rows.Count; row++)
			{
				if (parsed.RowSourceLine[row] != sourceIndex) continue;
				references.Add(new WeakReference(parsed.Rows[row]));
				references.Add(new WeakReference(parsed.RowLinks[row]));
			}
		}
		return references;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static (List<Cell> row, WeakReference document) CaptureUnchangedRow(MarkupControl control, int width)
	{
		var parsed = control.EnsureParsedForTest(width);
		return (parsed.Rows[0], new WeakReference(parsed));
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static List<WeakReference> RenderAndCapture(MarkupControl control, Window window, string text)
	{
		control.SetMarkdown(text);
		window.RenderAndGetVisibleContent();
		// These are the two widths requested by the real scrollbar overflow probes.
		return CaptureGroupRows(control, 0, [window.Width - 2, window.Width - 4]);
	}

	private static void AssertCollected(IEnumerable<WeakReference> references)
	{
		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();
		Assert.Equal(0, references.Count(reference => reference.IsAlive));
	}

	private static string RenderedText(MarkupControl.ParsedContent parsed)
		=> string.Join("\n", parsed.Rows.Select(row => string.Concat(row.Select(cell => cell.Character.ToString()))));

	private sealed class ThrowOnceHighlighter : ISyntaxHighlighter
	{
		private bool _throw = true;

		public (IReadOnlyList<SyntaxToken> Tokens, SyntaxLineState EndState) Tokenize(string line, int lineIndex, SyntaxLineState startState)
		{
			if (_throw)
			{
				_throw = false;
				throw new InvalidOperationException("Test parse failure");
			}
			return (Array.Empty<SyntaxToken>(), startState);
		}
	}
}
