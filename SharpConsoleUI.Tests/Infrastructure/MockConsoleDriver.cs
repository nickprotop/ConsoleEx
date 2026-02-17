// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Drivers;

namespace SharpConsoleUI.Tests.Infrastructure;

/// <summary>
/// Mock console driver for testing. Thin wrapper around <see cref="HeadlessConsoleDriver"/>.
/// </summary>
public class MockConsoleDriver : HeadlessConsoleDriver
{
	/// <summary>
	/// Creates a new mock console driver with default size 200x50.
	/// </summary>
	public MockConsoleDriver() : base()
	{
	}

	/// <summary>
	/// Creates a new mock console driver with specified size.
	/// </summary>
	public MockConsoleDriver(int width, int height) : base(width, height)
	{
	}
}
