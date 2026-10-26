// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Drawing;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using Xunit;

namespace SharpConsoleUI.Tests.Controls
{
	/// <summary>
	/// Every activating control, against the gestures the recognizer reports.
	/// </summary>
	/// <remarks>
	/// The recognizer derives click, double and triple and ADDS them to the
	/// event rather than replacing the press. A control that also runs its own
	/// clock has two detectors for one gesture, and they agree only by accident:
	/// one double click can activate twice, and two in a row can activate three
	/// times. This fixes the expected counts for each control so a regression in
	/// the shared layer shows up per control rather than in one place.
	/// </remarks>
	public class MouseGestureAuditTests
	{
		private static readonly MouseFlags[][] DoubleClick =
		{
			new[] { MouseFlags.Button1Pressed },
			new[] { MouseFlags.Button1Released, MouseFlags.Button1Clicked },
			new[] { MouseFlags.Button1Pressed, MouseFlags.Button1DoubleClicked },
			new[] { MouseFlags.Button1Released, MouseFlags.Button1Clicked },
		};

		private static ConsoleWindowSystem NewSystem() =>
			new(new HeadlessConsoleDriver(80, 25),
				options: ConsoleWindowSystemOptions.Create() with
				{
					PipedInput = new PipedInputOptions(Enabled: false),
				});

		private static Window Host(ConsoleWindowSystem system, IWindowControl control)
		{
			var window = new Window(system)
			{
				Width = 40,
				Height = 10,
				BorderStyle = BorderStyle.Frameless,
			};
			window.AddControl(control);
			system.AddWindow(window);
			window.RenderAndGetVisibleContent();
			return window;
		}

		private static void Send(IMouseAwareControl control, Point at)
		{
			foreach (var flags in DoubleClick)
				control.ProcessMouseEvent(new MouseEventArgs(new List<MouseFlags>(flags), at, at, at));
		}

		[Fact]
		public void AListItemActivatesOncePerDoubleClick()
		{
			int activations = 0;
			var list = new ListControl();
			list.AddItem("Acorn brittle");
			list.AddItem("Crumpet crumbs");
			list.ItemActivated += (_, _) => activations++;

			var system = NewSystem();
			Host(system, list);

			Send(list, new Point(2, 1));
			Assert.Equal(1, activations);

			Send(list, new Point(2, 1));
			Assert.Equal(2, activations);
		}

		[Fact]
		public void ATreeNodeActivatesOncePerDoubleClick()
		{
			int activations = 0;
			var tree = new TreeControl();
			tree.AddRootNode(new TreeNode("Acorn brittle"));
			tree.AddRootNode(new TreeNode("Crumpet crumbs"));
			tree.NodeActivated += (_, _) => activations++;

			var system = NewSystem();
			Host(system, tree);

			Send(tree, new Point(4, 1));
			Assert.Equal(1, activations);

			Send(tree, new Point(4, 1));
			Assert.Equal(2, activations);
		}

		[Fact]
		public void AMarkupDoubleClickRaisesOncePerGesture()
		{
			int doubles = 0;
			var markup = new MarkupControl(new List<string> { "Acorn brittle" })
			{
				DoubleClickEnabled = true,
			};
			markup.MouseDoubleClick += (_, _) => doubles++;

			var system = NewSystem();
			Host(system, markup);

			Send(markup, new Point(2, 0));
			Assert.Equal(1, doubles);

			Send(markup, new Point(2, 0));
			Assert.Equal(2, doubles);
		}

		[Fact]
		public void ATableRowActivatesOncePerDoubleClick()
		{
			int activations = 0;

			var table = SharpConsoleUI.Builders.Controls.Table()
				.AddColumn("Snack").AddColumn("Hoarder")
				.AddRow("Acorn brittle", "Sid Squirrel")
				.AddRow("Crumpet crumbs", "Wanda Wombat")
				.OnRowActivated((_, _) => activations++)
				.Build();

			var system = NewSystem();
			Host(system, table);

			Send(table, new Point(3, 4));
			Assert.Equal(1, activations);

			Send(table, new Point(3, 4));
			Assert.Equal(2, activations);
		}
	}
}
