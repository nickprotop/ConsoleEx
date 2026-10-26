// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SharpConsoleUI.Controls;
using Xunit;

namespace SharpConsoleUI.Tests.StateDescription
{
	/// <summary>
	/// Controls describe shared concepts under shared names.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A reader can make sense of one control spelling its selection
	/// <c>activeTab</c> and another spelling it <c>selectedIndex</c>. A tool
	/// cannot: an inspector can only offer "scroll to the end" for controls it
	/// can recognise as scrollable, and it recognises them by the names they
	/// report.
	/// </para>
	/// <para>
	/// Reflection lives here rather than in the library, which is
	/// <c>IsAotCompatible</c> - the test walks types to find what SHOULD be
	/// described, while each control still writes its own values explicitly.
	/// </para>
	/// </remarks>
	public class StateVocabularyTests
	{
		private sealed class Recorder : IStateWriter
		{
			public Dictionary<string, object?> Values { get; } = new();

			public void Write(string name, string? value) => Values[name] = value;
			public void Write(string name, long value) => Values[name] = value;
			public void Write(string name, double value) => Values[name] = value;
			public void Write(string name, bool value) => Values[name] = value;
			public void WriteIf(bool condition, string name, string? value)
			{
				if (condition) Values[name] = value;
			}
		}

		/// <summary>Every concrete control in the library.</summary>
		private static IEnumerable<Type> Controls() =>
			typeof(BaseControl).Assembly.GetTypes()
				.Where(t => t.IsClass && !t.IsAbstract && typeof(BaseControl).IsAssignableFrom(t))
				.Where(t => t.GetConstructor(Type.EmptyTypes) != null)
				.OrderBy(t => t.Name);

		private static Dictionary<string, object?>? TryDescribe(Type t)
		{
			try
			{
				var control = (BaseControl)Activator.CreateInstance(t)!;
				var r = new Recorder();
				control.DescribeState(r);
				return r.Values;
			}
			catch
			{
				// A control that cannot stand alone is not this test's business.
				return null;
			}
		}

		/// <summary>Whether a control declares a public property of its own.</summary>
		private static bool Declares(Type t, params string[] names) =>
			names.Any(n => t.GetProperty(n, BindingFlags.Public | BindingFlags.Instance) != null);

		[Fact]
		public void AControlWithASelectionReportsIt()
		{
			var missing = new List<string>();

			foreach (var t in Controls())
			{
				if (!Declares(t, "SelectedIndex")) continue;
				var state = TryDescribe(t);
				if (state == null) continue;

				if (!state.ContainsKey("selectedIndex") || !state.ContainsKey("itemCount"))
					missing.Add(t.Name);
			}

			Assert.True(missing.Count == 0,
				"these expose SelectedIndex but do not report selectedIndex + itemCount: "
					+ string.Join(", ", missing));
		}

		[Fact]
		public void NoControlSpellsASharedConceptItsOwnWay()
		{
			// The drift this exists to stop. Each of these meant the same thing as
			// a name in StateVocabulary, and cost a tool the ability to recognise
			// the concept at all.
			var banned = new Dictionary<string, string>
			{
				["label"] = "text",
				["caption"] = "text",
				["input"] = "text",
				["activeTab"] = "selectedIndex",
				["selectedItem"] = "selectedIndex",
				["offset"] = "scrollOffset",
				["scrollTop"] = "scrollOffset",
				["count"] = "itemCount",
			};

			var found = new List<string>();

			foreach (var t in Controls())
			{
				var state = TryDescribe(t);
				if (state == null) continue;

				foreach (var key in state.Keys)
				{
					if (banned.TryGetValue(key, out string? instead))
						found.Add($"{t.Name} writes '{key}' (use '{instead}')");
				}
			}

			Assert.True(found.Count == 0, string.Join("; ", found));
		}

		[Fact]
		public void EveryControlWithStateDescribesIt()
		{
			// The base writes enabled/interactive/focusable for everyone, so a
			// control "says nothing of its own" when it writes only those. Two are
			// allowed to: a layout sentinel that is not a real control, and a
			// separator, which is one character and genuinely has no state.
			var allowed = new HashSet<string> { "BodyMarker", "SeparatorControl" };

			var silent = new List<string>();

			foreach (var t in Controls())
			{
				var state = TryDescribe(t);
				if (state == null) continue;

				bool saysSomething = state.Keys
					.Any(k => k is not ("enabled" or "interactive" or "focusable"));

				if (!saysSomething && !allowed.Contains(t.Name)) silent.Add(t.Name);
			}

			Assert.True(silent.Count == 0,
				"these describe nothing of their own: " + string.Join(", ", silent));
		}

		[Fact]
		public void TheVocabularyWritesTheNamesItPromises()
		{
			var r = new Recorder();

			r.Selection(2, 7);
			r.Scroll(40, 20, 120);
			r.Text("hello");
			r.Value(70, 0, 100);
			r.Expansion(true);
			r.Editing(false, 12);

			Assert.Equal(2L, r.Values["selectedIndex"]);
			Assert.Equal(7L, r.Values["itemCount"]);
			Assert.Equal(40L, r.Values["scrollOffset"]);
			Assert.Equal(20L, r.Values["viewportExtent"]);
			Assert.Equal(120L, r.Values["contentExtent"]);
			Assert.Equal("hello", r.Values["text"]);
			Assert.Equal(70d, r.Values["value"]);
			Assert.Equal(0d, r.Values["min"]);
			Assert.Equal(100d, r.Values["max"]);
			Assert.Equal(true, r.Values["expanded"]);
			Assert.Equal(false, r.Values["editing"]);
			Assert.Equal(12L, r.Values["length"]);
		}

		[Fact]
		public void LongTextIsShortenedRatherThanDrowningTheDescription()
		{
			string long_ = new('x', 500);
			string? shortened = StateVocabulary.Shorten(long_);

			Assert.NotNull(shortened);
			Assert.True(shortened!.Length <= 64, $"shortened to {shortened.Length}");
			Assert.EndsWith("…", shortened);
			Assert.Equal("short", StateVocabulary.Shorten("short"));
			Assert.Null(StateVocabulary.Shorten(null));
		}
	}
}
