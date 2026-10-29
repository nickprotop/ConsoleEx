// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text.Json.Nodes;
using Cxdbg.Commands;
using Xunit;

namespace Cxdbg.Tests;

/// <summary>
/// Where the bytes a <c>seq</c> sends come from.
/// </summary>
/// <remarks>
/// A redirect is typed in two places that do not share a shell - the command
/// line, where the shell has already eaten it, and the prompt, where nothing
/// has. Both end in the same field, and these are the rules that keep them
/// agreeing.
/// </remarks>
public class SeqInputTests
{
	[Fact]
	public void ARedirectIsTakenOffTheLine()
	{
		var words = SeqInput.SplitRedirect(["seq", "<", "capture.txt"], out string? from);

		Assert.Equal("capture.txt", from);
		Assert.Equal(["seq"], words);
	}

	[Fact]
	public void WordsAfterTheRedirectSurvive()
	{
		// A shell puts a redirect wherever it likes. Taking the tail along with
		// it dropped the socket, and the command went looking for an
		// application nobody had named.
		var words = SeqInput.SplitRedirect(
			["seq", "<", "capture.txt", "-s", "/tmp/app.sock"], out string? from);

		Assert.Equal("capture.txt", from);
		Assert.Equal(["seq", "-s", "/tmp/app.sock"], words);
	}

	[Fact]
	public void ALineWithNoRedirectIsUntouched()
	{
		string[] typed = ["seq", "--text", "\\e[A"];

		var words = SeqInput.SplitRedirect(typed, out string? from);

		Assert.Null(from);
		Assert.Equal(typed, words);
	}

	[Fact]
	public void ATrailingArrowWithNoPathLeavesNothingToRead()
	{
		var words = SeqInput.SplitRedirect(["seq", "<"], out string? from);

		Assert.Null(from);
		Assert.Equal(["seq"], words);
	}

	[Fact]
	public void AFileBecomesTheTextField()
	{
		string path = Path.GetTempFileName();

		try
		{
			// The trailing newline an editor adds is dropped: sending it would
			// deliver an Enter the capture never contained.
			File.WriteAllText(path, "\\e[<0;5;5M\n");

			var fields = new Dictionary<string, JsonNode?>();

			Assert.Null(SeqInput.Load(path, fields));
			Assert.Equal("\\e[<0;5;5M", fields["text"]?.GetValue<string>());
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Fact]
	public void AMissingFileIsReportedRatherThanSent()
	{
		var fields = new Dictionary<string, JsonNode?>();

		string? error = SeqInput.Load("/no/such/capture.txt", fields);

		Assert.NotNull(error);
		Assert.Contains("no such file", error);
		Assert.Empty(fields);
	}

	[Fact]
	public void AFileTooBigToBeASequenceIsRefused()
	{
		string path = Path.GetTempFileName();

		try
		{
			File.WriteAllText(path, new string('x', SeqInput.MostBytes + 1));

			var fields = new Dictionary<string, JsonNode?>();

			string? error = SeqInput.Load(path, fields);

			Assert.NotNull(error);
			Assert.Contains("not that long", error);
			Assert.Empty(fields);
		}
		finally
		{
			File.Delete(path);
		}
	}
}
