// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Regression tests for gutter self-invalidation: an <see cref="IGutterRenderer"/> attached to a
/// MultilineEditControl that raises <see cref="IGutterRenderer.Invalidated"/> must re-invalidate the host
/// editor's window automatically — WITHOUT the consumer calling editor.Container?.Invalidate(...) manually.
/// A renderer that never raises the event behaves exactly as before.
/// </summary>
public class GutterRendererSelfInvalidationTests
{
	/// <summary>
	/// A test renderer that signals state changes levellessly. Its width is data-derived (number of
	/// "marked" columns), so the HOST editor — not this renderer — decides Relayout (width changed) vs
	/// Repaint (same width) by re-querying <see cref="GetWidth"/>.
	/// </summary>
	private sealed class StatefulGutter : IGutterRenderer
	{
		private int _columns;     // drives GetWidth
		private int _appearance;  // bumps without changing width
		public event EventHandler? Invalidated;

		/// <summary>Sets the column count (a width change when it differs) and signals.</summary>
		public void SetColumns(int columns)
		{
			_columns = columns;
			Invalidated?.Invoke(this, EventArgs.Empty);
		}

		/// <summary>Changes appearance only (width unchanged) and signals.</summary>
		public void BumpAppearance()
		{
			_appearance++;
			Invalidated?.Invoke(this, EventArgs.Empty);
		}

		public int GetWidth(int totalLineCount) => _columns;
		public void Render(in GutterRenderContext context, int width) { }
	}

	private static (ConsoleWindowSystem system, Window window, MultilineEditControl edit, StatefulGutter gutter) NewEditWindow()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(60, 24);
		var window = new Window(system) { Title = "Edit", Left = 0, Top = 0, Width = 50, Height = 18 };
		var edit = new MultilineEditControl();
		edit.SetContent("line one\nline two\nline three");
		var gutter = new StatefulGutter();
		edit.AddGutterRenderer(gutter);
		window.AddControl(edit);
		system.AddWindow(window);
		return (system, window, edit, gutter);
	}

	private static void Render(Window window)
	{
		var region = new List<Rectangle> { new Rectangle(0, 0, Math.Max(1, window.Width), Math.Max(1, window.Height)) };
		window.RenderAndGetVisibleContent(region);
	}

	[Fact]
	public void WidthChange_EditorDerivesRelayout_WithoutManualInvalidate()
	{
		var (_, window, _, gutter) = NewEditWindow();
		Render(window); // painted with gutter width 0
		Assert.Equal(FrameWork.None, window.PendingWork);

		// Renderer only signals "I changed". The EDITOR sees GetWidth went 0 → 2 and derives Relayout —
		// no editor.Invalidate() / Container.Invalidate() call anywhere.
		gutter.SetColumns(2);

		Assert.Equal(FrameWork.Relayout, window.PendingWork);
	}

	[Fact]
	public void AppearanceOnlyChange_EditorDerivesRepaint()
	{
		var (_, window, _, gutter) = NewEditWindow();
		gutter.SetColumns(2);
		Render(window); // now painted at gutter width 2
		Assert.Equal(FrameWork.None, window.PendingWork);

		// Width is unchanged (still 2). The editor re-queries GetWidth, sees no change → Repaint, not Relayout.
		gutter.BumpAppearance();

		Assert.Equal(FrameWork.Repaint, window.PendingWork);
	}

	[Fact]
	public void WidthChange_MeasureReflowsContent_EvenIfCacheWasWarm()
	{
		// The belt-and-braces in MeasureDOM: when a measure runs, a changed gutter width drops the
		// wrapped-lines cache so wrapping uses the new content width — the editor derives the layout
		// consequence from GetWidth, with no renderer/consumer level decision.
		var (_, window, edit, gutter) = NewEditWindow();
		Render(window);                  // measured + cached at gutter width 0
		int gutter0 = edit.GutterWidth;  // 0

		gutter.SetColumns(4);            // signals; editor requests Relayout
		Render(window);                  // measure runs → diff fires → rewrap at new width

		Assert.Equal(4, edit.GutterWidth);
		Assert.NotEqual(gutter0, edit.GutterWidth);
		Assert.Equal(FrameWork.None, window.PendingWork); // consumed cleanly
	}

	[Fact]
	public void RemovedGutter_IsUnsubscribed_DoesNotInvalidate()
	{
		var (_, window, edit, gutter) = NewEditWindow();
		edit.RemoveGutterRenderer(gutter);
		Render(window);
		Assert.Equal(FrameWork.None, window.PendingWork);

		// After removal the editor must no longer react to the detached renderer.
		gutter.SetColumns(2);

		Assert.Equal(FrameWork.None, window.PendingWork);
	}

	[Fact]
	public void ClearedGutters_AreUnsubscribed_DoNotInvalidate()
	{
		var (_, window, edit, gutter) = NewEditWindow();
		edit.ClearGutterRenderers();
		Render(window);
		Assert.Equal(FrameWork.None, window.PendingWork);

		gutter.SetColumns(2);

		Assert.Equal(FrameWork.None, window.PendingWork);
	}
}
