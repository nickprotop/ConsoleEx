using System;
using System.Diagnostics;
using SharpConsoleUI.Helpers;
using Xunit;

namespace SharpConsoleUI.Tests.Helpers;

/// <summary>
/// Tests that <see cref="UrlLauncher.Open"/> is best-effort and never throws — launching a browser is
/// a convenience with nothing to recover on failure, so a null/empty url is a safe no-op and a url with
/// no available launcher/handler must not surface an exception.
///
/// <para>It must also never BLOCK. The launch reaches the desktop shell, and on Windows
/// <c>ShellExecuteEx</c> answers a scheme with no registered handler by showing a modal
/// "How do you want to open this file?" dialog and waiting on it. Callers are typically the UI thread
/// handling a click on a link, so a blocking launch freezes the application behind a dialog the user
/// may not even see. Found on a real Windows desktop, where it stalled a full test run ~180 tests from
/// the end; a stack dump named <c>UrlLauncher.Open</c> and an <c>OpenWith.exe</c> was sitting on the
/// desktop waiting to be dismissed.</para>
///
/// <para>These tests deliberately use urls that must never reach the shell, so running them cannot
/// open a browser or leave a dialog behind.</para>
/// </summary>
public class UrlLauncherTests : IDisposable
{
	// Well under any watchdog threshold; a synchronous ShellExecuteEx on an unhandled scheme blows
	// past this by orders of magnitude (it waits for a human).
	private const int PromptMs = 250;

	public void Dispose() => UrlLauncher.AllowedSchemes = null;

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

	/// <summary>
	/// A malformed, non-absolute url is not launchable by anything and must be dropped before the shell.
	/// </summary>
	/// <remarks>
	/// Regression gate. These used to be handed to <c>ShellExecuteEx</c>, which opened the Windows
	/// "Choose an application" dialog and blocked the calling thread on it.
	/// </remarks>
	[Fact]
	public void Open_MalformedUrl_DoesNotThrow_AndReturnsPromptly()
	{
		var sw = Stopwatch.StartNew();
		var ex = Record.Exception(() =>
		{
			UrlLauncher.Open("not a url at all");
			UrlLauncher.Open("://missing-scheme");
			UrlLauncher.Open("/just/a/path");
		});
		sw.Stop();

		Assert.Null(ex);
		Assert.True(sw.ElapsedMilliseconds < PromptMs,
			$"Open blocked the caller for {sw.ElapsedMilliseconds}ms on malformed input.");
	}

	/// <summary>
	/// An unknown scheme is dropped when the application has restricted the allowed set — the way an
	/// app that only opens web links rules out the shell dialog entirely.
	/// </summary>
	[Fact]
	public void Open_UnknownScheme_IsDropped_WhenSchemesAreRestricted()
	{
		UrlLauncher.AllowedSchemes = Array.Empty<string>();

		var sw = Stopwatch.StartNew();
		var ex = Record.Exception(() => UrlLauncher.Open("nonexistent-scheme://xyz"));
		sw.Stop();

		Assert.Null(ex);
		Assert.True(sw.ElapsedMilliseconds < PromptMs,
			$"Open blocked the caller for {sw.ElapsedMilliseconds}ms; a dropped scheme must not reach the shell.");
	}

	/// <summary>
	/// Restricting the set must not disable the web schemes this helper exists for.
	/// </summary>
	/// <remarks>
	/// Asserted through the filter, never by calling <see cref="UrlLauncher.Open"/> with a web url:
	/// that reaches the shell and really does open the developer's browser mid-run.
	/// </remarks>
	[Fact]
	public void WebSchemes_StayAllowed_EvenWhenSchemesAreRestricted()
	{
		UrlLauncher.AllowedSchemes = Array.Empty<string>();

		Assert.True(IsLaunchable("https://example.com"));
		Assert.True(IsLaunchable("http://example.com"));
		Assert.True(IsLaunchable("mailto:someone@example.com"));
	}

	/// <summary>
	/// Application schemes stay allowed by default: existing code launching <c>vscode://</c> or
	/// <c>slack://</c> must not lose that silently.
	/// </summary>
	/// <remarks>
	/// Asserted through the filter rather than by calling <see cref="UrlLauncher.Open"/>, because on a
	/// developer machine where the scheme IS registered, actually launching it would open the
	/// application mid-test-run.
	/// </remarks>
	[Fact]
	public void CustomSchemes_AreAllowedByDefault()
	{
		Assert.Null(UrlLauncher.AllowedSchemes); // the shipped default

		Assert.True(IsLaunchable("vscode://file/tmp/x"));
		Assert.True(IsLaunchable("slack://channel?id=x"));
	}

	[Fact]
	public void AllowedSchemes_RestrictToTheNamedSet_CaseInsensitively()
	{
		UrlLauncher.AllowedSchemes = new[] { "VSCode" };

		Assert.True(IsLaunchable("vscode://file/tmp/x"));   // named, different casing
		Assert.False(IsLaunchable("slack://channel?id=x")); // not named
		Assert.True(IsLaunchable("https://example.com"));   // web schemes are always allowed
	}

	/// <summary>
	/// Asserts scheme policy through the filter itself, so nothing is handed to the desktop shell.
	/// </summary>
	private static bool IsLaunchable(string url) => UrlLauncher.IsLaunchableScheme(url);
}
