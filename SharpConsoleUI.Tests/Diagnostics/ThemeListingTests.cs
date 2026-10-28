// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Linq;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Diagnostics.Socket;
using SharpConsoleUI.Drivers;
using Xunit;

namespace SharpConsoleUI.Tests.ThemeListing
{
	/// <summary>
	/// Finding out which themes an application has.
	/// </summary>
	/// <remarks>
	/// A verb that only switches is one a caller can use when it already knows
	/// the answer. Anything building a list - a debugger's theme picker, a
	/// script trying each in turn - needs to ask, and the socket is the only
	/// thing it can ask.
	/// </remarks>
	public class ThemeListingTests
	{
		private static ConsoleWindowSystem Build() =>
			new(new HeadlessConsoleDriver(40, 10),
				options: ConsoleWindowSystemOptions.Create() with
				{
					PipedInput = new PipedInputOptions(Enabled: false),
				});

		[Fact]
		public void AskingWithNoNameListsThem()
		{
			var system = Build();

			var reply = DiagnosticsSerializer.Themes(system);

			var names = reply["themes"]!.AsArray()
				.Select(t => t!["name"]!.GetValue<string>())
				.ToList();

			Assert.NotEmpty(names);
			Assert.Contains(reply["theme"]!.GetValue<string>(), names);
		}

		[Fact]
		public void EachThemeSaysWhatItIs()
		{
			// A list of names answers "which" but not "which one do I want".
			var system = Build();

			var first = DiagnosticsSerializer.Themes(system)["themes"]!.AsArray()[0]!;

			Assert.False(string.IsNullOrWhiteSpace(first["description"]?.GetValue<string>()));
		}

		[Fact]
		public void EveryVerbNamesTheThemeTheSameWay()
		{
			// The registry calls it "ModernGray" and the class is
			// ModernGrayTheme. Reporting the type in one verb and the registry
			// name in another leaves a caller unable to tell whether the theme
			// changed, and a picker unable to match the current one against the
			// list it was given.
			var system = Build();

			string listed = DiagnosticsSerializer.Themes(system)["theme"]!.GetValue<string>();
			string onTheDesktop = DiagnosticsSerializer.Desktop(system, 1, false)["desktop"]!["theme"]!
				.GetValue<string>();

			Assert.Equal(listed, onTheDesktop);

			// And it is the registry's name, which is the one a caller can
			// pass back to `theme` to switch to it.
			Assert.Contains(listed, DiagnosticsSerializer.Themes(system)["themes"]!.AsArray()
				.Select(t => t!["name"]!.GetValue<string>()));
		}

		[Fact]
		public void ASwitchedThemeIsCalledWhatItWasSwitchedTo()
		{
			// `theme --name Ocean` worked and then `theme` answered
			// "MutableTheme" - the type of the object the registry built, not
			// the theme anyone asked for. A caller cannot tell whether the
			// switch took, and a picker cannot match the current theme against
			// its own list.
			var system = Build();

			var available = system.ThemeRegistryService.GetAvailableThemes().ToList();
			if (available.Count == 0) return;

			string wanted = available[^1].Name;
			system.ThemeStateService.SwitchTheme(wanted);

			Assert.Equal(wanted, DiagnosticsSerializer.ThemeName(system));
		}

		[Fact]
		public void TheDesktopCanBeAskedAboutOnItsOwn()
		{
			// "Is a modal up? is a portal eating my clicks? where does the
			// desktop start?" are first-class questions. They lived inside the
			// tree reply, which nothing in the verb list or the help mentioned
			// - and the formatter dropped them, so even the workaround showed
			// nothing.
			var system = Build();

			var desktop = DiagnosticsSerializer.DesktopInfo(system);

			Assert.True(desktop["width"]!.GetValue<int>() > 0);
			Assert.NotNull(desktop["originY"]);
			Assert.NotNull(desktop["modals"]);
			Assert.NotNull(desktop["portals"]);
			Assert.NotNull(desktop["theme"]);
		}

		[Fact]
		public void TheDesktopSaysTheSameAsTheTreeDoes()
		{
			// One description of the desktop, not two: the verb and the tree's
			// own block are the same object, so they cannot drift.
			var system = Build();

			var alone = DiagnosticsSerializer.DesktopInfo(system);
			var inTree = DiagnosticsSerializer.Desktop(system, 0, false)["desktop"]!.AsObject();

			Assert.Equal(inTree["width"]!.GetValue<int>(), alone["width"]!.GetValue<int>());
			Assert.Equal(inTree["theme"]!.GetValue<string>(), alone["theme"]!.GetValue<string>());
		}
	}
}
