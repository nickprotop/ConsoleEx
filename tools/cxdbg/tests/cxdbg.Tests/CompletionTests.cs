// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Cxdbg.Commands;
using Xunit;

namespace Cxdbg.Tests;

/// <summary>
/// What Tab offers at the prompt.
/// </summary>
/// <remarks>
/// Tab is a convenience, never a gate: a field cxdbg has never heard of still
/// reaches the application, it is simply not offered.
/// </remarks>
public class CompletionTests
{
	private static IReadOnlyList<string> At(string line) =>
		Completion.For(line, line.Length);

	[Fact]
	public void TheFirstWordIsAVerb()
	{
		var found = At("rea");

		Assert.Equal(["reachable"], found);
	}

	[Fact]
	public void SeveralCandidatesAllCome()
	{
		// The editor lists them and extends to what they share, so it needs
		// all of them, not the first.
		var found = At("fo");

		Assert.Contains("focus", found);
		Assert.Contains("focusctl", found);
	}

	[Fact]
	public void CxdbgsOwnCommandsAreOffered()
	{
		var found = At(".");

		Assert.Contains(".help", found);
		Assert.Contains(".last", found);
	}

	[Fact]
	public void AfterAVerbComeItsFields()
	{
		// From the verb's own example, which is the only list cxdbg keeps of
		// what a verb takes.
		var found = At("tree --");

		Assert.Contains("--depth", found);
		Assert.DoesNotContain("--role", found);
	}

	[Fact]
	public void AVerbCxdbgDoesNotKnowOffersNothingAndRefusesNothing()
	{
		// The protocol decides what exists. An unknown verb still reaches the
		// application; Tab simply has nothing to say about it.
		Assert.Empty(At("nosuchverb --"));
	}

	[Fact]
	public void AfterARedirectComesAPath()
	{
		// Completed against the filesystem, not against anything cxdbg knows.
		string directory = Path.GetDirectoryName(Path.GetTempPath().TrimEnd('/'))!;

		var found = Completion.For($"tree > {directory}/", $"tree > {directory}/".Length);

		Assert.NotEmpty(found);
	}

	[Fact]
	public void NothingTypedOffersEverything()
	{
		var found = At("");

		Assert.Contains("tree", found);
		Assert.Contains("exit", found);
	}
	[Fact]
	public void ANewVerbIsOfferedWithoutBeingTaughtTwice()
	{
		// Verbs and their fields come from ProtocolVerbs.All, which is the one
		// list cxdbg keeps. Adding `control` there made it complete, help and
		// render without a second edit anywhere.
		Assert.Contains("control", At("cont"));
		Assert.Contains("--control", At("control --"));
	}

	[Fact]
	public void EveryVerbCompletesItsOwnFields()
	{
		// Completion used to scrape field names out of each verb's EXAMPLE, so
		// a verb whose example showed no field offered none - `theme` had no
		// --name, which is the one thing you need to switch a theme. They come
		// from the declared fields now, and this checks every verb rather than
		// the one that was noticed.
		foreach (var (verb, _, fields, _) in ProtocolVerbs.All)
		{
			if (fields.Length == 0) continue;

			var offered = Completion.For($"{verb} --", $"{verb} --".Length);

			foreach (var field in fields)
			{
				Assert.Contains("--" + field.Name, offered);
			}
		}
	}

	[Fact]
	public void EveryVerbIsOfferedAsAFirstWord()
	{
		var all = At("");

		foreach (var (verb, _, _, _) in ProtocolVerbs.All) Assert.Contains(verb, all);
	}

	[Fact]
	public void CompletingAHalfTypedFieldGivesTheWholeField()
	{
		var found = Completion.For("logs --le", "logs --le".Length);

		Assert.Equal(["--level"], found);
	}

	[Theory]
	[InlineData("logs ", "")]
	[InlineData("logs --", "--")]
	[InlineData("logs --le", "--level ")]
	public void TabNeverWritesWhatTheUserIsAboutToType(string typed, string expected)
	{
		// Reported: `logs `, Tab, then typing "--" gave "logs ----". Tab had
		// completed to the shared prefix of every field - which is "--" and
		// nothing else - so the two dashes typed next landed after it.
		//
		// A prefix of pure punctuation is not a completion. The list is still
		// offered; the line is left alone.
		var editor = new LineEditor(Completion.For);

		Assert.Equal(expected, editor.CompleteForTest(typed));
	}

	[Fact]
	public void WatchIsOfferedAsAFirstWord()
	{
		// It is cxdbg's own loop rather than a protocol verb, so nothing in the
		// verb list would put it here.
		Assert.Contains("watch", At("wat"));
	}

	[Fact]
	public void AfterWatchComesAVerbToWatch()
	{
		var found = At("watch per");

		Assert.Equal(["performance"], found);
	}

	[Fact]
	public void AfterWatchAndItsVerbComeThatVerbsFields()
	{
		// `watch performance --` must offer performance's own fields, not
		// watch's alone: completion has to look one word further along than
		// usual, because the verb is in the second position here.
		var found = At("watch performance --");

		Assert.Contains("--reset", found);
	}

	[Fact]
	public void WatchsOwnFieldsComeTooWhateverIsWatched()
	{
		var found = At("watch frame --");

		Assert.Contains("--interval", found);
		Assert.Contains("--count", found);
	}

	[Fact]
	public void AVerbWithNoFieldsStillOffersWatchsOwn()
	{
		// `metrics` declares none. Offering nothing at all there would read as
		// "watch takes no options", which is wrong.
		var found = At("watch metrics --");

		Assert.Contains("--interval", found);
	}
}
