// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.IO;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using Xunit;

namespace SharpConsoleUI.Tests.Drivers;

/// <summary>
/// A headless system does not read stdin unless it is asked to.
/// </summary>
/// <remarks>
/// Capture is right for a terminal application started with <c>cat file | app</c>.
/// It is wrong for a headless one, where a redirected stdin is far more often a
/// test runner's doing than a deliberate pipe - and reading it is never what
/// anyone wanted. Piping into a headless app still works; it is opt-in.
/// </remarks>
public class HeadlessPipedInputTests
{
	[Fact]
	public void Headless_does_not_capture_stdin_by_default()
	{
		var system = new ConsoleWindowSystem(new HeadlessConsoleDriver(40, 10));

		Assert.False(system.PipedInputEnabled);
	}

	[Fact]
	public void Headless_ignores_a_redirected_stdin_by_default()
	{
		// The case that prompted this: a runner redirects stdin for its own
		// reasons and the system reads it as if it were piped input.
		var original = Console.In;

		try
		{
			Console.SetIn(new StringReader("not meant for the app\n"));

			var system = new ConsoleWindowSystem(new HeadlessConsoleDriver(40, 10));

			Assert.Null(system.PipedInput);
		}
		finally
		{
			Console.SetIn(original);
		}
	}

	[Fact]
	public void An_explicit_yes_still_captures()
	{
		var original = Console.In;

		try
		{
			Console.SetIn(new StringReader("alpha\nbeta\n"));

			var system = new ConsoleWindowSystem(new HeadlessConsoleDriver(40, 10),
				ConsoleWindowSystemOptions.Create() with
				{
					PipedInput = new PipedInputOptions(Enabled: true)
				});

			Assert.True(system.PipedInputEnabled);

			if (Console.IsInputRedirected)
				Assert.Equal("alpha\nbeta\n", system.PipedInput);
		}
		finally
		{
			Console.SetIn(original);
		}
	}
}
