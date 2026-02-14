// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls
{
	/// <summary>
	/// Security tests for FigleControl to verify path traversal protection.
	/// </summary>
	public class FigleControlSecurityTests
	{
		private readonly ConsoleWindowSystem _windowSystem;
		private readonly Window _window;

		public FigleControlSecurityTests()
		{
			_windowSystem = TestWindowSystemBuilder.CreateTestSystem();
			_window = new Window(_windowSystem);
		}

		[Fact]
		public void FontPath_PathTraversalAttempt_ThrowsArgumentException()
		{
			// Arrange
			var control = new FigleControl
			{
				Container = _window,
				Text = "Test",
				FontPath = "../../../etc/passwd"
			};

			// Act & Assert
			var exception = Assert.Throws<ArgumentException>(() =>
			{
				// Force font loading by accessing the control's content
				var size = control.GetLogicalContentSize();
			});

			Assert.Contains("path traversal", exception.Message.ToLower());
		}

		[Fact]
		public void FontPath_AbsolutePathOutsideFonts_ThrowsArgumentException()
		{
			// Arrange
			var control = new FigleControl
			{
				Container = _window,
				Text = "Test",
				FontPath = "/etc/passwd"
			};

			// Act & Assert
			var exception = Assert.Throws<ArgumentException>(() =>
			{
				var size = control.GetLogicalContentSize();
			});

			Assert.Contains("path traversal", exception.Message.ToLower());
		}

		[Fact]
		public void FontPath_ValidFontName_NoException()
		{
			// Arrange
			var control = new FigleControl
			{
				Container = _window,
				Text = "Test",
				FontPath = "validfont"
			};

			// Act - Should not throw even if file doesn't exist (falls back to default)
			var exception = Record.Exception(() =>
			{
				var size = control.GetLogicalContentSize();
			});

			// Assert - Should not throw security exception
			Assert.Null(exception);
		}

		[Fact]
		public void FontPath_NullOrEmpty_NoException()
		{
			// Arrange
			var control = new FigleControl
			{
				Container = _window,
				Text = "Test",
				FontPath = null
			};

			// Act
			var exception = Record.Exception(() =>
			{
				var size = control.GetLogicalContentSize();
			});

			// Assert
			Assert.Null(exception);
		}

		[Theory]
		[InlineData("..\\..\\..\\windows\\system32\\config\\sam")]
		[InlineData("../../../../etc/shadow")]
		[InlineData("..\\system.ini")]
		public void FontPath_VariousTraversalAttempts_ThrowsArgumentException(string maliciousPath)
		{
			// Arrange
			var control = new FigleControl
			{
				Container = _window,
				Text = "Test",
				FontPath = maliciousPath
			};

			// Act & Assert
			var exception = Assert.Throws<ArgumentException>(() =>
			{
				var size = control.GetLogicalContentSize();
			});

			Assert.Contains("path traversal", exception.Message.ToLower());
		}
	}
}
