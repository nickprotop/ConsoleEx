using SharpConsoleUI.Helpers;
using Xunit;

namespace SharpConsoleUI.Tests.Helpers;

[Collection("EnvSerial")]
public class ClipboardOsc52Tests : IDisposable
{
	private readonly List<string> _emitted = new();

	public ClipboardOsc52Tests()
	{
		ClipboardHelper.ForceBackendForTests(ClipboardBackend.InternalFallback);
		ClipboardHelper.RegisterOsc52Emitter(s => _emitted.Add(s));
		ClipboardHelper.Osc52Mode = Osc52Mode.Auto;
		TerminalCapabilities.SetOsc52Override(null);
	}

	/// <summary>
	/// Pins TMUX off for the duration, and restores whatever was there.
	/// </summary>
	/// <remarks>
	/// How many sequences the helper emits depends on tmux, so a test about anything else has to
	/// say which world it is in rather than inherit the developer's terminal.
	/// </remarks>
	private static IDisposable OutsideTmux() => new TmuxScope(null);

	private sealed class TmuxScope : IDisposable
	{
		private readonly string? _saved;

		public TmuxScope(string? value)
		{
			_saved = Environment.GetEnvironmentVariable("TMUX");
			Environment.SetEnvironmentVariable("TMUX", value);
			TerminalCapabilities.DetectClipboardEnvironmentForTests();
		}

		public void Dispose()
		{
			Environment.SetEnvironmentVariable("TMUX", _saved);
			TerminalCapabilities.DetectClipboardEnvironmentForTests();
		}
	}

	public void Dispose()
	{
		ClipboardHelper.RegisterOsc52Emitter(null);
		ClipboardHelper.Osc52Mode = Osc52Mode.Auto;
		TerminalCapabilities.SetOsc52Override(null);
		ClipboardHelper.MaxOsc52Bytes = Osc52.DefaultMaxBytes;
	}

	[Fact]
	public void Auto_WithOsc52Supported_EmitsAndSetsLocalBuffer()
	{
		// TMUX pinned off: under tmux the helper emits the plain sequence AND a passthrough-wrapped
		// one, by design, so a test that inherits the ambient environment asserts one count in CI
		// and a different one for anyone running the suite from a tmux pane.
		using var _ = OutsideTmux();

		TerminalCapabilities.SetOsc52Override(true);
		ClipboardHelper.SetText("hello");
		Assert.Single(_emitted);
		Assert.Contains("\x1b]52;c;", _emitted[0]);
		Assert.Equal("hello", ClipboardHelper.GetText());
	}

	[Fact]
	public void Disabled_NeverEmits_ButLocalStillSet()
	{
		ClipboardHelper.Osc52Mode = Osc52Mode.Disabled;
		ClipboardHelper.SetText("hello");
		Assert.Empty(_emitted);
		Assert.Equal("hello", ClipboardHelper.GetText());
	}

	[Fact]
	public void Enabled_OverridesUnsupported()
	{
		using var _ = OutsideTmux();

		ClipboardHelper.Osc52Mode = Osc52Mode.Enabled;
		TerminalCapabilities.SetOsc52Override(false);
		ClipboardHelper.SetText("hi");
		Assert.Single(_emitted);
	}

	[Fact]
	public void Auto_WhenOsc52Unsupported_DoesNotEmit_ButLocalStillSet()
	{
		TerminalCapabilities.SetOsc52Override(false);
		ClipboardHelper.SetText("hi");
		Assert.Empty(_emitted);
		Assert.Equal("hi", ClipboardHelper.GetText());
	}

	[Fact]
	public void OverCap_DoesNotEmit_ButLocalStillSet()
	{
		TerminalCapabilities.SetOsc52Override(true);
		ClipboardHelper.MaxOsc52Bytes = 8;
		ClipboardHelper.SetText(new string('x', 1000));
		Assert.Empty(_emitted);
		Assert.Equal(new string('x', 1000), ClipboardHelper.GetText());
	}

	/// <summary>
	/// UNDER TMUX, BOTH FORMS: plain, which tmux forwards to the outer terminal with `set-clipboard on`, then
	/// passthrough-wrapped, which it forwards with `allow-passthrough on`. Neither is tmux's default, so either
	/// setting is now enough where only the second used to be.
	/// </summary>
	[Fact]
	public void UnderTmux_EmitsPlainThenWrapped()
	{
		var saved = Environment.GetEnvironmentVariable("TMUX");
		try
		{
			Environment.SetEnvironmentVariable("TMUX", "/tmp/tmux-1000/default,1,0");
			TerminalCapabilities.DetectClipboardEnvironmentForTests();
			TerminalCapabilities.SetOsc52Override(true);

			ClipboardHelper.SetText("hi");

			Assert.Equal(2, _emitted.Count);
			Assert.Equal(Osc52.BuildSequence("hi", tmuxWrap: false, Osc52.DefaultMaxBytes), _emitted[0]);
			Assert.Equal(Osc52.BuildSequence("hi", tmuxWrap: true, Osc52.DefaultMaxBytes), _emitted[1]);
		}
		finally
		{
			Environment.SetEnvironmentVariable("TMUX", saved);
			TerminalCapabilities.DetectClipboardEnvironmentForTests();
		}
	}

	[Fact]
	public void OutsideTmux_EmitsThePlainSequenceOnce()
	{
		var saved = Environment.GetEnvironmentVariable("TMUX");
		try
		{
			Environment.SetEnvironmentVariable("TMUX", null);
			TerminalCapabilities.DetectClipboardEnvironmentForTests();
			TerminalCapabilities.SetOsc52Override(true);

			ClipboardHelper.SetText("hi");

			Assert.Equal(Osc52.BuildSequence("hi", tmuxWrap: false, Osc52.DefaultMaxBytes), Assert.Single(_emitted));
		}
		finally
		{
			Environment.SetEnvironmentVariable("TMUX", saved);
			TerminalCapabilities.DetectClipboardEnvironmentForTests();
		}
	}

	[Fact]
	public void EmitterThrows_SetTextStillSucceeds()
	{
		TerminalCapabilities.SetOsc52Override(true);
		ClipboardHelper.RegisterOsc52Emitter(_ => throw new InvalidOperationException("boom"));
		ClipboardHelper.SetText("safe");
		Assert.Equal("safe", ClipboardHelper.GetText());
	}
}
