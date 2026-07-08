using System;
using System.Threading;
using System.Threading.Tasks;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Dialogs;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Flows;
using SharpConsoleUI.Parsing;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Flows
{
    public class ProgressContentTests
    {
        private static ConsoleWindowSystem NewSystem() =>
            new ConsoleWindowSystem(new MockConsoleDriver(120, 40));

        private static ProgressContent<int> Build(ConsoleWindowSystem ws, bool allowMarkup = false)
        {
            var c = new ProgressContent<int>(ws, "start",
                (_, _) => Task.FromResult(0), allowMarkup);
            _ = c.BuildContent(new FlowChrome("t", autoSizeHeight: true));
            return c;
        }

        [Fact]
        public void Bar_IsHidden_UntilFractionReported()
        {
            var ws = NewSystem();
            var c = Build(ws);
            Assert.False(c.Bar!.Visible);
        }

        [Fact]
        public void FractionUpdate_ShowsBar_SetsValue_Determinate()
        {
            var ws = NewSystem();
            var c = Build(ws);
            c.ApplyUpdate(new ProgressUpdate(fraction: 0.4));
            Assert.True(c.Bar!.Visible);
            Assert.False(c.Bar.IsIndeterminate);
            Assert.Equal(0.4, c.Bar.Value, 3);
        }

        [Fact]
        public void FractionUpdate_ClampsOutOfRange()
        {
            var ws = NewSystem();
            var c = Build(ws);
            c.ApplyUpdate(new ProgressUpdate(fraction: 1.4));
            Assert.Equal(1.0, c.Bar!.Value, 3);
            c.ApplyUpdate(new ProgressUpdate(fraction: -0.1));
            Assert.Equal(0.0, c.Bar.Value, 3);
        }

        [Fact]
        public void IndeterminateUpdate_ShowsBar_SetsPulse()
        {
            var ws = NewSystem();
            var c = Build(ws);
            c.ApplyUpdate(new ProgressUpdate(indeterminate: true));
            Assert.True(c.Bar!.Visible);
            Assert.True(c.Bar.IsIndeterminate);
        }

        [Fact]
        public void FractionAfterIndeterminate_ReturnsToDeterminate()
        {
            var ws = NewSystem();
            var c = Build(ws);
            c.ApplyUpdate(new ProgressUpdate(indeterminate: true));
            c.ApplyUpdate(new ProgressUpdate(fraction: 0.9));
            Assert.False(c.Bar!.IsIndeterminate);
            Assert.Equal(0.9, c.Bar.Value, 3);
        }

        [Fact]
        public void MessageOnlyUpdate_ChangesStatus_LeavesBarHidden()
        {
            var ws = NewSystem();
            var c = Build(ws);
            c.ApplyUpdate(new ProgressUpdate(message: "hello"));
            Assert.False(c.Bar!.Visible);
            Assert.Contains("hello", c.Status!.Text);
        }

        [Fact]
        public void FractionOnlyUpdate_DoesNotTouchStatusText()
        {
            var ws = NewSystem();
            var c = Build(ws);
            c.ApplyUpdate(new ProgressUpdate(message: "keepme"));
            c.ApplyUpdate(new ProgressUpdate(fraction: 0.5));
            Assert.Contains("keepme", c.Status!.Text);
        }

        [Fact]
        public void AllowMarkupFalse_EscapesMessage()
        {
            var ws = NewSystem();
            var c = Build(ws, allowMarkup: false);
            c.ApplyUpdate(new ProgressUpdate(message: "[red]x[/]"));
            // Escaped: the literal bracket text survives (parser sees escaped tags, not a red run).
            Assert.Equal(MarkupParser.Escape("[red]x[/]"), c.Status!.Text);
        }

        [Fact]
        public void AllowMarkupTrue_PassesMessageRaw()
        {
            var ws = NewSystem();
            var c = Build(ws, allowMarkup: true);
            c.ApplyUpdate(new ProgressUpdate(message: "[red]x[/]"));
            Assert.Equal("[red]x[/]", c.Status!.Text);
        }
    }
}
