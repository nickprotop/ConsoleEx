using SharpConsoleUI.Configuration;
using SharpConsoleUI.Core;
using SharpConsoleUI.Tests.Infrastructure;
using Rectangle = System.Drawing.Rectangle;
using Xunit;

namespace SharpConsoleUI.Tests.Core;

/// <summary>
/// The real path: toasts shown through a live <see cref="ToastService"/> on a real window system,
/// asserting the portal bounds the service actually hands the renderer. The static measure tests
/// cover the arithmetic; these cover the wiring — per-toast options reaching the measurement, the
/// service defaults applying, and real stacking not colliding.
/// </summary>
public class ToastLiveSizingTests
{
	private sealed class NoopScheduler : IToastScheduler
	{
		public void Schedule(int delayMs, Action callback) { }
	}

	private static ToastService NewService(out ConsoleWindowSystem sys)
	{
		sys = TestWindowSystemBuilder.CreateTestSystem();
		return new ToastService(sys, scheduler: new NoopScheduler());
	}

	/// <summary>
	/// The bounds the service actually gave the renderer: read off the live desktop portal, which is
	/// what the toast is painted into. Portals are listed bottom-to-top in creation order.
	/// </summary>
	private static Rectangle BoundsAt(ConsoleWindowSystem sys, int index)
	{
		var portals = sys.DesktopPortalService.Portals;
		Assert.True(index < portals.Count, $"no portal at index {index} (have {portals.Count})");
		return portals[index].Bounds;
	}

	[Fact]
	public void ALongMessage_GetsATallerPortalThanAShortOne()
	{
		var svc = NewService(out var sys);

		svc.Show("Copied", NotificationSeverity.Info);
		svc.Show(string.Join(' ', Enumerable.Repeat("word", 60)), NotificationSeverity.Info);

		Assert.True(BoundsAt(sys, 1).Height > BoundsAt(sys, 0).Height);
	}

	[Fact]
	public void PerToastMaxLines_ReachesTheMeasurement()
	{
		var svc = NewService(out var sys);
		string text = string.Join(' ', Enumerable.Repeat("word", 60));

		svc.Show(text, NotificationSeverity.Info, new ToastOptions(MaxLines: 1));
		svc.Show(text, NotificationSeverity.Info, new ToastOptions(MaxLines: 4));

		Assert.Equal(1 + ControlDefaults.ToastChromeHeight, BoundsAt(sys, 0).Height);
		Assert.Equal(4 + ControlDefaults.ToastChromeHeight, BoundsAt(sys, 1).Height);
	}

	[Fact]
	public void TheServiceDefault_AppliesWhenAToastDoesNotSayOtherwise()
	{
		var svc = NewService(out var sys);
		svc.DefaultMaxLines = 3;

		svc.Show(string.Join(' ', Enumerable.Repeat("word", 60)), NotificationSeverity.Info);

		Assert.Equal(3 + ControlDefaults.ToastChromeHeight, BoundsAt(sys, 0).Height);
	}

	[Fact]
	public void APerToastValue_BeatsTheServiceDefault()
	{
		var svc = NewService(out var sys);
		svc.DefaultMaxLines = 3;

		svc.Show(string.Join(' ', Enumerable.Repeat("word", 60)), NotificationSeverity.Info,
			new ToastOptions(MaxLines: 1));

		Assert.Equal(1 + ControlDefaults.ToastChromeHeight, BoundsAt(sys, 0).Height);
	}

	[Fact]
	public void PerToastMinWidth_WidensTheBox()
	{
		var svc = NewService(out var sys);

		svc.Show("Hi", NotificationSeverity.Info);
		svc.Show("Hi", NotificationSeverity.Info, new ToastOptions(MinWidth: 40));

		Assert.True(BoundsAt(sys, 0).Width < 40);
		Assert.Equal(40, BoundsAt(sys, 1).Width);
	}

	[Fact]
	public void TheServiceMinWidthDefault_AppliesToPlainToasts()
	{
		var svc = NewService(out var sys);
		svc.DefaultMinWidth = 36;

		svc.Show("Hi", NotificationSeverity.Info);
		Assert.Equal(36, BoundsAt(sys, 0).Width);
	}

	[Fact]
	public void StackedToastsOfDifferentHeights_DoNotOverlap()
	{
		var svc = NewService(out var sys);

		svc.Show("Short", NotificationSeverity.Info);
		svc.Show(string.Join(' ', Enumerable.Repeat("word", 60)), NotificationSeverity.Info,
			new ToastOptions(MaxLines: 4));
		svc.Show("Another", NotificationSeverity.Info);

		var boxes = new[] { BoundsAt(sys, 0), BoundsAt(sys, 1), BoundsAt(sys, 2) };

		// BottomRight stacks upward: each later toast sits fully above the previous one.
		for (int i = 1; i < boxes.Length; i++)
			Assert.True(boxes[i].Y + boxes[i].Height <= boxes[i - 1].Y,
				$"toast {i} at Y={boxes[i].Y} h={boxes[i].Height} overlaps toast {i - 1} at Y={boxes[i - 1].Y}");
	}

	[Fact]
	public void ToastsAtDifferentCorners_BothSitAtTheirOwnEdge()
	{
		var svc = NewService(out var sys);

		svc.Show("Right", NotificationSeverity.Info,
			new ToastOptions(Position: ToastPosition.BottomRight));
		svc.Show("Left", NotificationSeverity.Info,
			new ToastOptions(Position: ToastPosition.BottomLeft));

		var rb = BoundsAt(sys, 0);
		var lb = BoundsAt(sys, 1);

		// Neither is pushed up by the other — they occupy different corners of the same row.
		Assert.Equal(rb.Y, lb.Y);
	}

	[Fact]
	public void EveryToastStaysOnScreen()
	{
		var svc = NewService(out var sys);
		int i = 0;

		foreach (string text in new[] { "Hi", new string('x', 400), string.Join(' ', Enumerable.Repeat("word", 80)) })
		{
			svc.Show(text, NotificationSeverity.Info);
			var b = BoundsAt(sys, i++);

			Assert.True(b.X >= 0, $"X {b.X} off screen");
			Assert.True(b.Y >= 0, $"Y {b.Y} off screen");
			Assert.True(b.Width >= 1 && b.Height >= 1);
		}
	}
}
