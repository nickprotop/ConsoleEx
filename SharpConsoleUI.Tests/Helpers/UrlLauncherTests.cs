using SharpConsoleUI.Helpers;
using Xunit;

namespace SharpConsoleUI.Tests.Helpers;

/// <summary>
/// Tests that <see cref="UrlLauncher.Open"/> is best-effort and never throws — launching a browser is
/// a convenience with nothing to recover on failure, so a null/empty url is a safe no-op and a url with
/// no available launcher/handler must not surface an exception.
/// </summary>
public class UrlLauncherTests
{
	[Fact]
	public void Open_NullOrEmpty_IsNoOp()
	{
		var ex = Record.Exception(() =>
		{
			UrlLauncher.Open(null!);
			UrlLauncher.Open("");
			UrlLauncher.Open("   ");
		});
		Assert.Null(ex);
	}

	[Fact]
	public void Open_BadUrl_DoesNotThrow()
	{
		var ex = Record.Exception(() => UrlLauncher.Open("nonexistent-scheme://xyz"));
		Assert.Null(ex);
	}
}
