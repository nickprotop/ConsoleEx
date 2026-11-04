// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Imaging;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// A control shown inside a message, below its body.
/// </summary>
public class ChatMessageAttachmentTests
{
	private static ChatTranscriptControl Build() => new();

	private static MarkupControl Thumbnail(string text = "[thumbnail]")
		=> new(new List<string> { text });

	#region Setting and clearing

	[Fact]
	public void SetAttachment_PutsTheControlInTheTranscript()
	{
		var chat = Build();
		var id = chat.AddMessage(ChatRole.User, "look at this");
		var thumb = Thumbnail();

		chat.SetMessageAttachment(id, thumb);

		Assert.Same(thumb, chat.GetMessageAttachment(id));
		Assert.Same(thumb, chat.ShownAttachmentForTest(id));
	}

	/// <summary>
	/// It is drawn after the message's own panel, so a message reads as body then attachment rather
	/// than the other way round.
	/// </summary>
	[Fact]
	public void TheAttachment_SitsAfterTheMessagePanel()
	{
		var chat = Build();
		var id = chat.AddMessage(ChatRole.User, "look at this");
		var thumb = Thumbnail();

		chat.SetMessageAttachment(id, thumb);

		var children = chat.Children.ToList();
		int panel = children.FindIndex(c => c is CollapsiblePanel);
		int attachment = children.IndexOf(thumb);

		Assert.True(panel >= 0, "the message panel should be a child");
		Assert.True(attachment > panel, "the attachment should follow the panel it belongs to");
	}

	[Fact]
	public void SettingNull_ClearsIt()
	{
		var chat = Build();
		var id = chat.AddMessage(ChatRole.User, "x");
		var thumb = Thumbnail();
		chat.SetMessageAttachment(id, thumb);

		chat.SetMessageAttachment(id, null);

		Assert.Null(chat.GetMessageAttachment(id));
		Assert.Null(chat.ShownAttachmentForTest(id));
		Assert.DoesNotContain(thumb, chat.Children);
	}

	/// <summary>A second attachment replaces the first, which must not be left behind.</summary>
	[Fact]
	public void SettingASecond_RemovesTheFirst()
	{
		var chat = Build();
		var id = chat.AddMessage(ChatRole.User, "x");
		var first = Thumbnail("[first]");
		var second = Thumbnail("[second]");
		chat.SetMessageAttachment(id, first);

		chat.SetMessageAttachment(id, second);

		Assert.Same(second, chat.GetMessageAttachment(id));
		Assert.DoesNotContain(first, chat.Children);
		Assert.Contains(second, chat.Children);
	}

	/// <summary>Setting the same control twice is a no-op, not a remove and re-add.</summary>
	[Fact]
	public void SettingTheSameControlAgain_ChangesNothing()
	{
		var chat = Build();
		var id = chat.AddMessage(ChatRole.User, "x");
		var thumb = Thumbnail();
		chat.SetMessageAttachment(id, thumb);
		int before = chat.Children.Count;

		chat.SetMessageAttachment(id, thumb);

		Assert.Equal(before, chat.Children.Count);
		Assert.Same(thumb, chat.ShownAttachmentForTest(id));
	}

	[Fact]
	public void AnUnknownMessage_Throws()
	{
		var chat = Build();

		Assert.ThrowsAny<Exception>(() => chat.SetMessageAttachment(new ChatMessageId(9999), Thumbnail()));
		Assert.ThrowsAny<Exception>(() => chat.GetMessageAttachment(new ChatMessageId(9999)));
	}

	#endregion

	#region Lifetime

	[Fact]
	public void RemovingTheMessage_TakesTheAttachmentWithIt()
	{
		var chat = Build();
		var id = chat.AddMessage(ChatRole.User, "x");
		var thumb = Thumbnail();
		chat.SetMessageAttachment(id, thumb);

		chat.RemoveMessage(id);

		Assert.DoesNotContain(thumb, chat.Children);
	}

	[Fact]
	public void ClearingTheTranscript_TakesEveryAttachmentWithIt()
	{
		var chat = Build();
		var first = chat.AddMessage(ChatRole.User, "one");
		var second = chat.AddMessage(ChatRole.Assistant, "two");
		var a = Thumbnail("[a]");
		var b = Thumbnail("[b]");
		chat.SetMessageAttachment(first, a);
		chat.SetMessageAttachment(second, b);

		chat.Clear();

		Assert.DoesNotContain(a, chat.Children);
		Assert.DoesNotContain(b, chat.Children);
	}

	/// <summary>One message's attachment is not another's: removing one leaves the other alone.</summary>
	[Fact]
	public void RemovingOneMessage_LeavesAnotherAttachmentAlone()
	{
		var chat = Build();
		var first = chat.AddMessage(ChatRole.User, "one");
		var second = chat.AddMessage(ChatRole.Assistant, "two");
		var a = Thumbnail("[a]");
		var b = Thumbnail("[b]");
		chat.SetMessageAttachment(first, a);
		chat.SetMessageAttachment(second, b);

		chat.RemoveMessage(first);

		Assert.DoesNotContain(a, chat.Children);
		Assert.Contains(b, chat.Children);
	}

	#endregion

	#region Collapse and expand

	/// <summary>
	/// A collapsed message is a single summary line. An image hanging underneath it would
	/// contradict the collapse, so the attachment comes out.
	/// </summary>
	[Fact]
	public void CollapsingTheMessage_TakesTheAttachmentOutOfView()
	{
		// A Tool message, because User panels are not collapsible: collapsing one is a no-op and
		// the test would be asserting against an expansion change that never happened.
		var chat = Build();
		var id = chat.AddMessage(ChatRole.Tool, "x");
		var thumb = Thumbnail();
		chat.SetMessageAttachment(id, thumb);

		chat.SetExpanded(id, false);

		Assert.DoesNotContain(thumb, chat.Children);
		Assert.Null(chat.ShownAttachmentForTest(id));
	}

	[Fact]
	public void ExpandingAgain_PutsItBack()
	{
		var chat = Build();
		var id = chat.AddMessage(ChatRole.Tool, "x");
		var thumb = Thumbnail();
		chat.SetMessageAttachment(id, thumb);
		chat.SetExpanded(id, false);

		chat.SetExpanded(id, true);

		Assert.Contains(thumb, chat.Children);
		Assert.Same(thumb, chat.ShownAttachmentForTest(id));
	}

	/// <summary>
	/// The SAME instance comes back. A host holding a reference keeps it, and an ImageControl is
	/// not asked to decode its image again every time the message is reopened.
	/// </summary>
	[Fact]
	public void TheSameInstanceSurvivesACollapseAndExpand()
	{
		var chat = Build();
		var id = chat.AddMessage(ChatRole.Tool, "x");
		var thumb = Thumbnail();
		chat.SetMessageAttachment(id, thumb);

		chat.SetExpanded(id, false);
		chat.SetExpanded(id, true);

		Assert.Same(thumb, chat.GetMessageAttachment(id));
		Assert.Same(thumb, chat.ShownAttachmentForTest(id));
	}

	/// <summary>
	/// Attaching to a message that is already collapsed records it without showing it, so expanding
	/// later reveals it rather than losing it.
	/// </summary>
	[Fact]
	public void AttachingToACollapsedMessage_ShowsItOnExpand()
	{
		var chat = Build();
		var id = chat.AddMessage(ChatRole.Tool, "x");
		chat.SetExpanded(id, false);
		var thumb = Thumbnail();

		chat.SetMessageAttachment(id, thumb);

		Assert.Same(thumb, chat.GetMessageAttachment(id));
		Assert.Null(chat.ShownAttachmentForTest(id));

		chat.SetExpanded(id, true);
		Assert.Same(thumb, chat.ShownAttachmentForTest(id));
	}

	#endregion

	#region The real thing

	/// <summary>
	/// A hosted transcript with a real image attachment, rendered and then scrolled, asserting the
	/// attachment survives the re-render rather than only existing between the call and the assert.
	/// </summary>
	[Fact]
	public void AnImageAttachment_SurvivesRenderingAndScrolling()
	{
		const int width = 50, height = 14;
		var system = TestWindowSystemBuilder.CreateTestSystem(width, height);
		var window = new Window(system) { Left = 0, Top = 0, Width = width, Height = height };

		var chat = new ChatTranscriptControl();
		window.AddControl(chat);
		system.AddWindow(window);

		// Enough messages that the transcript has somewhere to scroll.
		var target = chat.AddMessage(ChatRole.User, "here is the image");
		for (int i = 0; i < 20; i++)
			chat.AddMessage(ChatRole.Assistant, $"reply {i}");

		var pixels = new PixelBuffer(8, 8);
		for (int y = 0; y < 8; y++)
			for (int x = 0; x < 8; x++)
				pixels.SetPixel(x, y, new ImagePixel(200, 60, 60));

		var image = new ImageControl { Source = pixels };
		chat.SetMessageAttachment(target, image);

		system.Render.UpdateDisplay();
		Assert.Same(image, chat.ShownAttachmentForTest(target));

		// Scroll, re-render, and assert it is still there: a re-render is where state that was only
		// set rather than stored gets lost.
		chat.ScrollToBottom();
		system.Render.UpdateDisplay();

		Assert.Same(image, chat.GetMessageAttachment(target));
		Assert.Same(image, chat.ShownAttachmentForTest(target));
		GC.KeepAlive(system);
	}

	#endregion
}
