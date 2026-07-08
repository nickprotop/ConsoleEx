using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Flows;
using SharpConsoleUI.Core;
using Xunit;

namespace SharpConsoleUI.Tests.Flows
{
	public class ProgressTopBandTests
	{
		[Fact]
		public void ProgressChrome_TitleBand_ContainsSpinnerMarkup_NotStaticGlyph()
		{
			var chrome = new FlowChrome(
				"Working", stepIndicator: null, widthHint: 54, heightHint: null,
				buttons: null, refreshButtons: null,
				severity: NotificationSeverityEnum.None,
				useProgressGlyph: true, autoSizeHeight: true);

			var band = FlowContentHelpers.BuildTopBand(chrome);

			// The first control is the title MarkupControl; its content must carry the animated
			// [spinner] tag, not the static glyph.
			var title = (MarkupControl)band[0];
			string content = title.Text; // MarkupControl.Text = the content lines joined with "\n"
			Assert.Contains(ControlDefaults.FlowSpinnerMarkup, content);
			Assert.DoesNotContain(ControlDefaults.FlowGlyphProgress, content);
		}
	}
}
