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
	}
}
