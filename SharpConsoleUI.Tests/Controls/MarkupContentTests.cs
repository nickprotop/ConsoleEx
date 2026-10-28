// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using SharpConsoleUI.Controls;
using Xunit;

namespace SharpConsoleUI.Tests.Controls
{
	/// <summary>
	/// Setting content a control already has.
	/// </summary>
	/// <remarks>
	/// A live view refills its panes on every poll, several times a second,
	/// almost always with what they already say. Treating that as a change
	/// relayouts the whole control for nothing, which is what a reader sees
	/// as flicker.
	/// </remarks>
	public class MarkupContentTests
	{
		/// <summary>How many times the control said it had changed.</summary>
		/// <remarks>
		/// INotifyPropertyChanged on Text is what a repaint hangs off, so it
		/// is the observable the caller shares with the layout - firmer than
		/// an internal counter and the same question.
		/// </remarks>
		private static int Changes(MarkupControl control, Action act)
		{
			int count = 0;

			void OnChanged(object? _, System.ComponentModel.PropertyChangedEventArgs e)
			{
				if (e.PropertyName == nameof(MarkupControl.Text)) count++;
			}

			control.PropertyChanged += OnChanged;

			try { act(); }
			finally { control.PropertyChanged -= OnChanged; }

			return count;
		}

		[Fact]
		public void SettingTheSameContentChangesNothing()
		{
			var control = new MarkupControl(new List<string> { "one", "two" });

			Assert.Equal(0, Changes(control,
				() => control.SetContent(new List<string> { "one", "two" })));
		}

		[Fact]
		public void SettingDifferentContentStillChanges()
		{
			var control = new MarkupControl(new List<string> { "one" });

			Assert.Equal(1, Changes(control,
				() => control.SetContent(new List<string> { "two" })));
		}

		[Fact]
		public void AddingALineIsAChange()
		{
			// Same first line, one more after it: a comparison that stopped at
			// the shorter of the two would miss this.
			var control = new MarkupControl(new List<string> { "one" });

			Assert.Equal(1, Changes(control,
				() => control.SetContent(new List<string> { "one", "two" })));
		}
	}
}
