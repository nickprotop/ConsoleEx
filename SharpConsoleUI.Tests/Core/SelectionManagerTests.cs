// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Core;

public class SelectionManagerTests
{
	/// <summary>Minimal selectable control used to exercise SelectionManager coordination in isolation.</summary>
	private sealed class FakeSelectable : ISelectableControl
	{
		private string _text;
		public FakeSelectable(string text) { _text = text; Selected = true; }

		public bool Selected { get; private set; }
		public bool HasSelection => Selected;
		public string GetSelectedText() => Selected ? _text : string.Empty;
		public void ClearSelection() { if (Selected) { Selected = false; SelectionChanged?.Invoke(this, string.Empty); } }
		public event EventHandler<string>? SelectionChanged;

		// IWindowControl (minimal stub)
		public int? ContentWidth => 0;
		public HorizontalAlignment HorizontalAlignment { get; set; }
		public VerticalAlignment VerticalAlignment { get; set; }
		public IContainer? Container { get; set; }
		public Margin Margin { get; set; }
		public StickyPosition StickyPosition { get; set; }
		public string? Name { get; set; }
		public object? Tag { get; set; }
		public bool Visible { get; set; } = true;
		public int? Width { get; set; }
		public int? Height { get; set; }
		public int ActualX => 0;
		public int ActualY => 0;
		public int ActualWidth => 0;
		public int ActualHeight => 0;
		public Size GetLogicalContentSize() => new(0, 0);
		public void Invalidate() { }
		public void Dispose() { }
	}

	[Fact]
	public void Window_ExposesSelectionManager()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 25 };
		Assert.NotNull(window.SelectionManager);
		Assert.False(window.SelectionManager.HasSelection);
	}

	[Fact]
	public void SetActiveSelection_ClearsPreviousOwner()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 25 };
		var a = new FakeSelectable("AAA");
		var b = new FakeSelectable("BBB");

		window.SelectionManager.SetActiveSelection(a);
		Assert.Same(a, window.SelectionManager.ActiveSelection);
		Assert.True(a.HasSelection);

		window.SelectionManager.SetActiveSelection(b);
		Assert.Same(b, window.SelectionManager.ActiveSelection);
		Assert.False(a.HasSelection); // previous owner cleared (single-selection invariant)
		Assert.True(b.HasSelection);
	}

	[Fact]
	public void GetSelectedText_ReflectsActiveControl()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 25 };
		var a = new FakeSelectable("hello");

		window.SelectionManager.SetActiveSelection(a);
		Assert.True(window.SelectionManager.HasSelection);
		Assert.Equal("hello", window.SelectionManager.GetSelectedText());
	}

	[Fact]
	public void ClearSelection_ResetsAndRaisesEvent()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 25 };
		var a = new FakeSelectable("x");
		SelectionChangedEventArgs? lastEvent = null;
		window.SelectionManager.SelectionChanged += (_, e) => lastEvent = e;

		window.SelectionManager.SetActiveSelection(a);
		window.SelectionManager.ClearSelection();

		Assert.Null(window.SelectionManager.ActiveSelection);
		Assert.False(window.SelectionManager.HasSelection);
		Assert.NotNull(lastEvent);
		Assert.Null(lastEvent!.Active);
	}
}
