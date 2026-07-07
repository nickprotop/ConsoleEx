// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Drawing;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Helpers;
using Xunit;

namespace SharpConsoleUI.Tests.Helpers
{
	public class MouseGestureCaptureTests
	{
		private enum R
		{
			Content,
			VScroll,
			HScroll,
			Gutter
		}

		private static MouseEventArgs Make(params MouseFlags[] flags)
		{
			var pos = new Point(0, 0);
			return new MouseEventArgs(new List<MouseFlags>(flags), pos, pos, pos);
		}

		[Fact]
		public void FreshPress_CapturesHitTestedRegion_ReturnsDown()
		{
			var capture = new MouseGestureCapture<R>();
			int calls = 0;

			var route = capture.Route(Make(MouseFlags.Button1Pressed), _ =>
			{
				calls++;
				return R.VScroll;
			});

			Assert.Equal(GesturePhase.Down, route.Phase);
			Assert.Equal(R.VScroll, route.Region);
			Assert.True(capture.IsCapturing);
			Assert.Equal(R.VScroll, capture.CapturedRegion);
			Assert.Equal(1, calls);
		}

		[Fact]
		public void PressOrDrag_WhileCapturing_ReturnsMove_WithoutCallingHitTest()
		{
			var capture = new MouseGestureCapture<R>();
			int calls = 0;

			// Fresh press captures VScroll.
			capture.Route(Make(MouseFlags.Button1Pressed), _ =>
			{
				calls++;
				return R.VScroll;
			});
			Assert.Equal(1, calls);

			// A resent press+drag over what would hit-test to Content must route to VScroll.
			var route = capture.Route(Make(MouseFlags.Button1Pressed, MouseFlags.Button1Dragged), _ =>
			{
				calls++;
				return R.Content;
			});

			Assert.Equal(GesturePhase.Move, route.Phase);
			Assert.Equal(R.VScroll, route.Region);
			Assert.Equal(1, calls); // hitTest NOT called on this event
		}

		[Fact]
		public void Release_ReturnsUp_WithCapturedRegion_ThenClears()
		{
			var capture = new MouseGestureCapture<R>();

			capture.Route(Make(MouseFlags.Button1Pressed), _ => R.Gutter);

			var route = capture.Route(Make(MouseFlags.Button1Released), _ => R.Content);

			Assert.Equal(GesturePhase.Up, route.Phase);
			Assert.Equal(R.Gutter, route.Region);
			Assert.False(capture.IsCapturing);
		}

		[Fact]
		public void Button1Clicked_WhileCapturing_ReturnsUp_ThenClears()
		{
			var capture = new MouseGestureCapture<R>();

			capture.Route(Make(MouseFlags.Button1Pressed), _ => R.Gutter);

			var route = capture.Route(Make(MouseFlags.Button1Clicked), _ => R.Content);

			Assert.Equal(GesturePhase.Up, route.Phase);
			Assert.Equal(R.Gutter, route.Region);
			Assert.False(capture.IsCapturing);
		}

		[Fact]
		public void StrayDragWithNoCapture_ReturnsNone()
		{
			var capture = new MouseGestureCapture<R>();

			var route = capture.Route(Make(MouseFlags.Button1Dragged), _ => R.Content);

			Assert.Equal(GesturePhase.None, route.Phase);
			Assert.False(capture.IsCapturing);
		}

		[Fact]
		public void Reset_ClearsActiveCapture()
		{
			var capture = new MouseGestureCapture<R>();

			capture.Route(Make(MouseFlags.Button1Pressed), _ => R.VScroll);
			Assert.True(capture.IsCapturing);

			capture.Reset();
			Assert.False(capture.IsCapturing);

			var route = capture.Route(Make(MouseFlags.Button1Dragged), _ => R.Content);
			Assert.Equal(GesturePhase.None, route.Phase);
		}
	}
}
