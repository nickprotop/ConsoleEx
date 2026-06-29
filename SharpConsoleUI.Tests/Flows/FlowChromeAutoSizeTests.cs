// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Flows;
using Xunit;

namespace SharpConsoleUI.Tests.Flows
{
	public class FlowChromeAutoSizeTests
	{
		[Fact]
		public void NewFlags_DefaultFalse()
		{
			var chrome = new FlowChrome("T");
			Assert.False(chrome.AutoSizeHeight);
			Assert.False(chrome.Resizable);
		}

		[Fact]
		public void Flags_RoundTripThroughPublicCtor()
		{
			var chrome = new FlowChrome("T", autoSizeHeight: true, resizable: true);
			Assert.True(chrome.AutoSizeHeight);
			Assert.True(chrome.Resizable);
		}
	}
}
