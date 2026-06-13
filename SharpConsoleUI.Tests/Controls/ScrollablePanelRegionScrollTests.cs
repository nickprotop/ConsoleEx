// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using SharpConsoleUI.Controls;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Coverage for the region-based scroll-into-view API on <see cref="IScrollableContainer"/> and its
/// <see cref="ScrollablePanelControl"/> override (<c>ScrollChildRegionIntoView</c>). Verifies that the
/// region clamp mirrors the existing whole-child clamp, and that the default interface method degrades
/// to <see cref="IScrollableContainer.ScrollChildIntoView"/> for implementers that do not override it.
/// </summary>
public class ScrollablePanelRegionScrollTests
{
	private readonly ITestOutputHelper _out;

	public ScrollablePanelRegionScrollTests(ITestOutputHelper outHelper)
	{
		_out = outHelper;
	}

	private static (ScrollablePanelControl panel, Window window) Render(ScrollablePanelControl panel)
	{
		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();
		return (panel, window);
	}

	private static MarkupControl Tall(int lines) =>
		new MarkupControl(Enumerable.Range(0, lines).Select(i => $"line{i}").ToList());

	[Fact]
	public void RegionBelowViewport_ScrollsDownToShowRegionBottom()
	{
		var panel = new ScrollablePanelControl { Height = 6 };
		var child = Tall(40); // far taller than the viewport
		panel.AddControl(child);
		var (_, _) = Render(panel);

		Assert.Equal(0, panel.VerticalScrollOffset); // precondition: top
		int vh = panel.ViewportHeight;
		_out.WriteLine($"viewportHeight={vh} contentHeight={panel.TotalContentHeight}");

		// Region of height 1 well below the viewport (row 20 within the child).
		int regionTop = 20;
		panel.ScrollChildRegionIntoView(child, childRelativeTop: regionTop, regionHeight: 1);
		_out.WriteLine($"after below-scroll offset={panel.VerticalScrollOffset}");

		// Mirror whole-child clamp: region bottom aligned to viewport bottom.
		int expected = regionTop + 1 - vh;
		Assert.Equal(expected, panel.VerticalScrollOffset);
	}

	[Fact]
	public void RegionAboveViewport_ScrollsUpToShowRegionTop()
	{
		var panel = new ScrollablePanelControl { Height = 6 };
		var child = Tall(40);
		panel.AddControl(child);
		var (_, _) = Render(panel);

		// Scroll down first so a low region is above the viewport.
		panel.ScrollToPosition(vertical: 25);
		Assert.Equal(25, panel.VerticalScrollOffset);

		int regionTop = 5; // above the current viewport
		panel.ScrollChildRegionIntoView(child, childRelativeTop: regionTop, regionHeight: 1);
		_out.WriteLine($"after above-scroll offset={panel.VerticalScrollOffset}");

		// Region top aligned to viewport top.
		Assert.Equal(regionTop, panel.VerticalScrollOffset);
	}

	[Fact]
	public void RegionAlreadyVisible_DoesNotScroll()
	{
		var panel = new ScrollablePanelControl { Height = 6 };
		var child = Tall(40);
		panel.AddControl(child);
		var (_, _) = Render(panel);

		panel.ScrollToPosition(vertical: 10);
		int before = panel.VerticalScrollOffset;
		Assert.Equal(10, before);

		// Region at row 12, height 1 — within [10, 10+viewportHeight).
		panel.ScrollChildRegionIntoView(child, childRelativeTop: 12, regionHeight: 1);
		_out.WriteLine($"visible-region offset before={before} after={panel.VerticalScrollOffset}");

		Assert.Equal(before, panel.VerticalScrollOffset); // no scroll
	}

	[Fact]
	public void RegionTallerThanViewport_AlignsToRegionTop()
	{
		var panel = new ScrollablePanelControl { Height = 6 };
		var child = Tall(40);
		panel.AddControl(child);
		var (_, _) = Render(panel);

		Assert.Equal(0, panel.VerticalScrollOffset);
		int vh = panel.ViewportHeight;

		// Region below the viewport AND taller than it → align to region top (not bottom).
		int regionTop = 10;
		int regionHeight = vh + 5;
		panel.ScrollChildRegionIntoView(child, childRelativeTop: regionTop, regionHeight: regionHeight);
		_out.WriteLine($"viewportHeight={vh} regionHeight={regionHeight} offset={panel.VerticalScrollOffset}");

		Assert.Equal(regionTop, panel.VerticalScrollOffset);
	}

	[Fact]
	public void ChildNotInPanel_NoOp_DoesNotThrow()
	{
		var panel = new ScrollablePanelControl { Height = 6 };
		panel.AddControl(Tall(40));
		var (_, _) = Render(panel);

		panel.ScrollToPosition(vertical: 8);
		int before = panel.VerticalScrollOffset;
		Assert.Equal(8, before);

		var stranger = Tall(3); // never added to the panel
		var ex = Record.Exception(() =>
			panel.ScrollChildRegionIntoView(stranger, childRelativeTop: 2, regionHeight: 1));
		_out.WriteLine($"stranger offset before={before} after={panel.VerticalScrollOffset}");

		Assert.Null(ex);
		Assert.Equal(before, panel.VerticalScrollOffset); // unchanged
	}

	/// <summary>
	/// Minimal implementer that does NOT override the region method, to exercise the default
	/// interface method degrade path (falls back to ScrollChildIntoView).
	/// </summary>
	private sealed class StubScrollContainer : IScrollableContainer
	{
		public bool ScrollChildIntoViewCalled { get; private set; }
		public IWindowControl? LastChild { get; private set; }

		public void ScrollChildIntoView(IWindowControl child)
		{
			ScrollChildIntoViewCalled = true;
			LastChild = child;
		}
	}

	[Fact]
	public void DefaultInterfaceMethod_DegradesToScrollChildIntoView()
	{
		IScrollableContainer container = new StubScrollContainer();
		var child = Tall(3);

		// Call via the interface — the stub does not override the region method, so the default
		// interface method should forward to ScrollChildIntoView.
		container.ScrollChildRegionIntoView(child, childRelativeTop: 1, regionHeight: 1);

		var stub = (StubScrollContainer)container;
		Assert.True(stub.ScrollChildIntoViewCalled, "Default interface method must degrade to ScrollChildIntoView.");
		Assert.Same(child, stub.LastChild);
	}
}
