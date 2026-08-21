// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Helpers;
using Xunit;

namespace SharpConsoleUI.Tests.Helpers
{
	/// <summary>
	/// The continuous-press loop repeats a held mouse button. It used to be an unawaited
	/// <c>while (_isButtonPressed)</c> poll with no owner, so a press that never saw its release —
	/// a driver shutting down mid-click, a dropped sequence — left it spinning against a dead
	/// handler, and the next press stacked another loop on top of it.
	/// </summary>
	public class SequenceHelperContinuousPressTests : IDisposable
	{
		public SequenceHelperContinuousPressTests() => SequenceHelper.Reset();

		public void Dispose() => SequenceHelper.Reset();

		private static ConsoleKeyInfo[] ToCki(string s)
		{
			var list = new List<ConsoleKeyInfo>();
			foreach (char c in s)
				list.Add(new ConsoleKeyInfo(c, ConsoleKey.None, false, false, false));
			return list.ToArray();
		}

		/// <summary>Feeds an SGR press for button 1 at (10,5). No release follows.</summary>
		private static void Press(Action<MouseFlags, System.Drawing.Point> handler)
		{
			SequenceHelper.GetMouse(ToCki("[<0;10;5M"), out _, out _, handler);
		}

		[Fact]
		public void PressWithoutRelease_ThenReset_StopsTheLoop()
		{
			int calls = 0;
			Press((_, __) => Interlocked.Increment(ref calls));

			Thread.Sleep(250);            // let the loop tick at least once
			SequenceHelper.Reset();

			int atReset = Volatile.Read(ref calls);
			Thread.Sleep(300);

			// Before the fix the count kept climbing forever: nothing cleared _isButtonPressed
			// when the release never arrived, so the loop outlived its owner.
			Assert.Equal(atReset, Volatile.Read(ref calls));
		}

		[Fact]
		public void ReleaseAfterPress_StopsTheLoop()
		{
			int calls = 0;
			Press((_, __) => Interlocked.Increment(ref calls));
			Thread.Sleep(150);

			// Release: the loop is cancelled here rather than noticing the flag on its next tick.
			SequenceHelper.GetMouse(ToCki("[<0;10;5m"), out _, out _, (_, __) => { });

			int atRelease = Volatile.Read(ref calls);
			Thread.Sleep(300);

			Assert.Equal(atRelease, Volatile.Read(ref calls));
		}

		[Fact]
		public void Reset_ClearsClickState()
		{
			Press((_, __) => { });
			SequenceHelper.Reset();

			// A press after Reset must read as a first click, not a continuation of the previous
			// sequence — this is the state leak that made tests order-dependent.
			SequenceHelper.GetMouse(ToCki("[<0;10;5M"), out var flags, out _, (_, __) => { });
			Assert.DoesNotContain(MouseFlags.Button1DoubleClicked, flags);
			Assert.DoesNotContain(MouseFlags.Button1TripleClicked, flags);
		}

		[Fact]
		public void Reset_IsSafeWhenNothingIsRunning()
		{
			SequenceHelper.Reset();
			SequenceHelper.Reset();
		}

		[Fact]
		public void HandlerThrowing_DoesNotFaultAnUnobservedTask()
		{
			// The loop is fire-and-forget; an escaping exception would be unobserved.
			Press((_, __) => throw new InvalidOperationException("boom"));
			Thread.Sleep(250);
			SequenceHelper.Reset();
			// Reaching here without the process dying is the assertion.
			Assert.True(true);
		}
	}
}
