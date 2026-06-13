using System.Collections.Generic;
using System.Drawing;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using Xunit;
using Color = SharpConsoleUI.Color;

namespace SharpConsoleUI.Tests.Controls
{
    public class MarkupBuilderLinkTests
    {
        private static MouseEventArgs Click(int x, int y)
        {
            var p = new Point(x, y);
            return new MouseEventArgs(new List<MouseFlags> { MouseFlags.Button1Clicked }, p, p, p);
        }

        [Fact]
        public void Builder_OnLinkClicked_WiresHandler()
        {
            string? got = null;
            var control = MarkupControl.Create()
                .AddLine("[link=https://b.com]go[/]")
                .OnLinkClicked((_, e) => got = e.Url)
                .Build();

            var buffer = new CharacterBuffer(45, 9);
            var bounds = new LayoutRect(0, 0, 40, 4);
            control.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);
            control.ProcessMouseEvent(Click(0, 0));

            Assert.Equal("https://b.com", got);
        }
    }
}
