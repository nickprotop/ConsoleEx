using SharpConsoleUI.Helpers;
using Xunit;

namespace SharpConsoleUI.Tests.Helpers;

[CollectionDefinition("EnvSerial", DisableParallelization = true)]
public class EnvSerialCollection { }

[Collection("EnvSerial")]
public class TerminalCapabilitiesSessionTests
{
	private static void WithEnv(Action body, params (string Key, string? Val)[] vars)
	{
		var saved = vars.Select(v => (v.Key, Old: Environment.GetEnvironmentVariable(v.Key))).ToList();
		try
		{
			foreach (var v in vars) Environment.SetEnvironmentVariable(v.Key, v.Val);
			body();
		}
		finally
		{
			foreach (var s in saved) Environment.SetEnvironmentVariable(s.Key, s.Old);
		}
	}

	[Fact]
	public void Detects_Ssh_Tmux_Screen_AndOsc52()
	{
		WithEnv(() =>
		{
			TerminalCapabilities.DetectClipboardEnvironmentForTests();
			Assert.True(TerminalCapabilities.IsRemoteSession);
			Assert.True(TerminalCapabilities.IsTmux);
			Assert.False(TerminalCapabilities.IsScreen);
			Assert.True(TerminalCapabilities.SupportsOsc52); // tmux is fine (we wrap)
		},
		("SSH_TTY", "/dev/pts/3"), ("TMUX", "/tmp/tmux-1000/default,1,0"), ("STY", null));
	}

	[Fact]
	public void Screen_DisablesOsc52()
	{
		WithEnv(() =>
		{
			TerminalCapabilities.DetectClipboardEnvironmentForTests();
			Assert.True(TerminalCapabilities.IsScreen);
			Assert.False(TerminalCapabilities.SupportsOsc52);
		},
		("STY", "12345.pts-0.host"), ("SSH_TTY", null), ("TMUX", null));
	}

	[Fact]
	public void Local_NoSshVars_IsNotRemote()
	{
		WithEnv(() =>
		{
			TerminalCapabilities.DetectClipboardEnvironmentForTests();
			Assert.False(TerminalCapabilities.IsRemoteSession);
			Assert.True(TerminalCapabilities.SupportsOsc52);
		},
		("SSH_TTY", null), ("SSH_CONNECTION", null), ("TMUX", null), ("STY", null));
	}

	[Fact]
	public void SshConnection_AloneMarksRemote()
	{
		WithEnv(() =>
		{
			TerminalCapabilities.DetectClipboardEnvironmentForTests();
			Assert.True(TerminalCapabilities.IsRemoteSession);
		},
		("SSH_TTY", null), ("SSH_CONNECTION", "1.2.3.4 5 6.7.8.9 22"), ("TMUX", null), ("STY", null));
	}
}
