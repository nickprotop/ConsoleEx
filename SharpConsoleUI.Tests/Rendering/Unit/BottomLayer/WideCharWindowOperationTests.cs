using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;
using System.Text;

namespace SharpConsoleUI.Tests.Rendering.Unit.BottomLayer;

/// <summary>
/// Tests that window move/resize/close operations produce correct rendering
/// when windows contain wide (CJK) characters. Validates that the ConsoleBuffer
/// cursor tracking handles continuation cells properly — the bug that caused
/// cyan vertical bar artifacts on move/close.
/// </summary>
public class WideCharWindowOperationTests
{
	private readonly ITestOutputHelper _output;

	public WideCharWindowOperationTests(ITestOutputHelper output)
	{
		_output = output;
	}

	#region Static Content — Zero Output Guarantee

	[Fact]
	public void WideChar_StaticContent_ZeroOutputOnFrame2()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10, Top = 5, Width = 30, Height = 10, Title = "CJK Static"
		};
		window.AddControl(new MarkupControl(new List<string> { "中文字テスト" }));
		system.WindowStateService.AddWindow(window);

		system.Render.UpdateDisplay();
		var frame1 = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(frame1);
		Assert.True(frame1.BytesWritten > 0, "Frame 1 should produce output");

		system.Render.UpdateDisplay();
		var frame2 = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(frame2);

		_output.WriteLine($"Frame 1: {frame1.BytesWritten} bytes, Frame 2: {frame2.BytesWritten} bytes");
		Assert.Equal(0, frame2.BytesWritten);
		Assert.Equal(0, frame2.CharactersChanged);
		Assert.True(frame2.IsStaticFrame);
	}

	[Fact]
	public void WideChar_MixedContent_ZeroOutputOnFrame2()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 5, Top = 3, Width = 40, Height = 12, Title = "Mixed"
		};
		window.AddControl(new MarkupControl(new List<string>
		{
			"Hello 世界 Test",
			"日本語 mixed with ASCII",
			"한국어 Korean text"
		}));
		system.WindowStateService.AddWindow(window);

		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		var frame2 = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(frame2);
		Assert.Equal(0, frame2.BytesWritten);
		Assert.True(frame2.IsStaticFrame);
	}

	#endregion

	#region Window Move — Cursor Tracking

	[Fact]
	public void WideChar_WindowMove_NoArtifactsAfterSettle()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();

		var background = new Window(system)
		{
			Left = 0, Top = 0, Width = 60, Height = 20, Title = "BG"
		};
		background.AddControl(new MarkupControl(new List<string>
		{
			"BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
			"BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
		}));

		var window = new Window(system)
		{
			Left = 10, Top = 5, Width = 30, Height = 10, Title = "CJK Move"
		};
		window.AddControl(new MarkupControl(new List<string>
		{
			"中文字テスト混合ABC",
			"日本語テキスト表示",
			"한국어 Korean テスト"
		}));

		system.WindowStateService.AddWindow(background);
		system.WindowStateService.AddWindow(window);

		// Frame 1: initial render
		system.Render.UpdateDisplay();
		var frame1 = system.RenderingDiagnostics?.LastMetrics;
		_output.WriteLine($"Frame 1 (initial): {frame1?.BytesWritten} bytes");

		// Frame 2: static — zero output
		system.Render.UpdateDisplay();
		var frame2 = system.RenderingDiagnostics?.LastMetrics;
		Assert.Equal(0, frame2?.BytesWritten);

		// Frame 3: move window right
		window.Left = 20;
		system.Render.UpdateDisplay();
		var frame3 = system.RenderingDiagnostics?.LastMetrics;
		_output.WriteLine($"Frame 3 (after move): {frame3?.BytesWritten} bytes, dirty={frame3?.DirtyCellsMarked}");
		Assert.True(frame3?.BytesWritten > 0, "Move should produce output");

		// Frame 4: settled — MUST be zero (this is the artifact test)
		system.Render.UpdateDisplay();
		var frame4 = system.RenderingDiagnostics?.LastMetrics;
		_output.WriteLine($"Frame 4 (settled): {frame4?.BytesWritten} bytes");
		Assert.Equal(0, frame4?.BytesWritten);
		Assert.True(frame4?.IsStaticFrame);
	}

	[Fact]
	public void WideChar_WindowMoveLeft_NoArtifacts()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();

		var background = new Window(system)
		{
			Left = 0, Top = 0, Width = 60, Height = 20, Title = "BG"
		};
		background.AddControl(new MarkupControl(new List<string>
		{
			new string('B', 56),
			new string('B', 56),
			new string('B', 56),
		}));

		var window = new Window(system)
		{
			Left = 20, Top = 5, Width = 30, Height = 10, Title = "CJK"
		};
		window.AddControl(new MarkupControl(new List<string> { "日本語テスト" }));

		system.WindowStateService.AddWindow(background);
		system.WindowStateService.AddWindow(window);

		system.Render.UpdateDisplay();

		// Move left
		window.Left = 5;
		system.Render.UpdateDisplay();

		// Settle — must be zero
		system.Render.UpdateDisplay();
		var settled = system.RenderingDiagnostics?.LastMetrics;
		_output.WriteLine($"Settled after move-left: {settled?.BytesWritten} bytes");
		Assert.Equal(0, settled?.BytesWritten);
	}

	[Fact]
	public void WideChar_MultipleMovesRapidly_NoAccumulatedArtifacts()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();

		var background = new Window(system)
		{
			Left = 0, Top = 0, Width = 80, Height = 25, Title = "BG"
		};
		background.AddControl(new MarkupControl(new List<string>
		{
			new string('X', 76),
			new string('X', 76),
			new string('X', 76),
			new string('X', 76),
		}));

		var window = new Window(system)
		{
			Left = 10, Top = 5, Width = 30, Height = 10, Title = "Moving CJK"
		};
		window.AddControl(new MarkupControl(new List<string>
		{
			"中文 mixed テスト ABC",
			"한국어 Korean 日本語",
		}));

		system.WindowStateService.AddWindow(background);
		system.WindowStateService.AddWindow(window);
		system.Render.UpdateDisplay();

		// Move multiple times
		for (int i = 0; i < 5; i++)
		{
			window.Left = 10 + i * 5;
			system.Render.UpdateDisplay();
		}

		// Final settle
		system.Render.UpdateDisplay();
		var settled = system.RenderingDiagnostics?.LastMetrics;
		_output.WriteLine($"Settled after 5 moves: {settled?.BytesWritten} bytes");
		Assert.Equal(0, settled?.BytesWritten);
		Assert.True(settled?.IsStaticFrame);
	}

	#endregion

	#region Window Move — Content Correctness

	[Fact]
	public void WideChar_WindowMove_BackgroundRestoredCorrectly()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();

		var background = new Window(system)
		{
			Left = 0, Top = 0, Width = 60, Height = 20, Title = "BG"
		};
		// Fill enough lines to cover the area where the CJK window will be
		var bgLines = Enumerable.Range(0, 16).Select(_ => new string('B', 56)).ToList();
		background.AddControl(new MarkupControl(bgLines));

		var window = new Window(system)
		{
			Left = 10, Top = 3, Width = 20, Height = 6, Title = "CJK"
		};
		window.AddControl(new MarkupControl(new List<string> { "中文字" }));

		system.WindowStateService.AddWindow(background);
		system.WindowStateService.AddWindow(window);
		system.Render.UpdateDisplay();

		// Move window completely to the right, exposing old position
		window.Left = 35;
		system.Render.UpdateDisplay();

		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		// Old position should now show background 'B'
		// Content row inside background window: y=1 (border) + content starts at y=1
		// Check row 5 which was covered by CJK window interior
		for (int x = 12; x < 28; x++)
		{
			var cell = snapshot.GetBack(x, 5);
			_output.WriteLine($"Old pos ({x},5): '{cell.Character}'");
			Assert.Equal(new Rune('B'), cell.Character);
		}
	}

	[Fact]
	public void WideChar_WindowMove_WideCharsAtNewPositionCorrect()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();

		var window = new Window(system)
		{
			Left = 5, Top = 3, Width = 30, Height = 10, Title = "CJK"
		};
		window.AddControl(new MarkupControl(new List<string> { "ABCD中文EF" }));
		system.WindowStateService.AddWindow(window);
		system.Render.UpdateDisplay();

		// Move right by 10
		window.Left = 15;
		system.Render.UpdateDisplay();

		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		// Content area starts at window.Left + 1 (border) = 16
		// "ABCD" at cols 16-19, then '中' at 20-21, '文' at 22-23, 'E' at 24, 'F' at 25
		var cellA = snapshot.GetBack(16, 4);
		Assert.Equal(new Rune('A'), cellA.Character);

		var cellD = snapshot.GetBack(19, 4);
		Assert.Equal(new Rune('D'), cellD.Character);

		// Wide char '中' at position 20
		var cellCJK = snapshot.GetBack(20, 4);
		Assert.Equal(new Rune('中'), cellCJK.Character);

		// 'E' after the two wide chars
		var cellE = snapshot.GetBack(24, 4);
		Assert.Equal(new Rune('E'), cellE.Character);
	}

	#endregion

	#region Window Resize

	[Fact]
	public void WideChar_WindowResize_NoArtifactsAfterSettle()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();

		var window = new Window(system)
		{
			Left = 5, Top = 3, Width = 30, Height = 10, Title = "CJK Resize"
		};
		window.AddControl(new MarkupControl(new List<string>
		{
			"中文字テスト表示確認",
			"日本語 ASCII 混合テスト",
		}));
		system.WindowStateService.AddWindow(window);

		system.Render.UpdateDisplay();

		// Resize wider
		window.Width = 40;
		system.Render.UpdateDisplay();
		var afterResize = system.RenderingDiagnostics?.LastMetrics;
		_output.WriteLine($"After resize: {afterResize?.BytesWritten} bytes");

		// Settle
		system.Render.UpdateDisplay();
		var settled = system.RenderingDiagnostics?.LastMetrics;
		_output.WriteLine($"Settled: {settled?.BytesWritten} bytes");
		Assert.Equal(0, settled?.BytesWritten);
		Assert.True(settled?.IsStaticFrame);
	}

	[Fact]
	public void WideChar_WindowResizeSmaller_NoArtifacts()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();

		var background = new Window(system)
		{
			Left = 0, Top = 0, Width = 60, Height = 20, Title = "BG"
		};
		background.AddControl(new MarkupControl(new List<string>
		{
			new string('B', 56),
			new string('B', 56),
			new string('B', 56),
		}));

		var window = new Window(system)
		{
			Left = 5, Top = 3, Width = 40, Height = 12, Title = "CJK Shrink"
		};
		window.AddControl(new MarkupControl(new List<string> { "中文日本語한국어" }));

		system.WindowStateService.AddWindow(background);
		system.WindowStateService.AddWindow(window);
		system.Render.UpdateDisplay();

		// Shrink — may truncate wide chars at new boundary
		window.Width = 20;
		system.Render.UpdateDisplay();

		// Settle
		system.Render.UpdateDisplay();
		var settled = system.RenderingDiagnostics?.LastMetrics;
		_output.WriteLine($"Settled after shrink: {settled?.BytesWritten} bytes");
		Assert.Equal(0, settled?.BytesWritten);
	}

	[Fact]
	public void WideChar_WindowResizeHeight_NoArtifacts()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();

		var background = new Window(system)
		{
			Left = 0, Top = 0, Width = 60, Height = 25, Title = "BG"
		};
		background.AddControl(new MarkupControl(new List<string>
		{
			new string('B', 56),
			new string('B', 56),
			new string('B', 56),
			new string('B', 56),
			new string('B', 56),
			new string('B', 56),
		}));

		var window = new Window(system)
		{
			Left = 5, Top = 3, Width = 30, Height = 15, Title = "CJK"
		};
		window.AddControl(new MarkupControl(new List<string> { "中文テスト" }));

		system.WindowStateService.AddWindow(background);
		system.WindowStateService.AddWindow(window);
		system.Render.UpdateDisplay();

		// Shrink height
		window.Height = 8;
		system.Render.UpdateDisplay();

		// Settle
		system.Render.UpdateDisplay();
		var settled = system.RenderingDiagnostics?.LastMetrics;
		_output.WriteLine($"Settled after height shrink: {settled?.BytesWritten} bytes");
		Assert.Equal(0, settled?.BytesWritten);
	}

	#endregion

	#region Window Close

	[Fact]
	public void WideChar_WindowClose_BackgroundRestoredNoArtifacts()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();

		var background = new Window(system)
		{
			Left = 0, Top = 0, Width = 60, Height = 20, Title = "BG"
		};
		background.AddControl(new MarkupControl(new List<string>
		{
			new string('B', 56),
			new string('B', 56),
			new string('B', 56),
			new string('B', 56),
			new string('B', 56),
		}));

		var window = new Window(system)
		{
			Left = 10, Top = 5, Width = 30, Height = 10, Title = "CJK Close"
		};
		window.AddControl(new MarkupControl(new List<string>
		{
			"中文字テスト",
			"日本語 mixed text",
			"한국어 Korean 텍스트"
		}));

		system.WindowStateService.AddWindow(background);
		system.WindowStateService.AddWindow(window);
		system.Render.UpdateDisplay();

		// Close the CJK window
		system.CloseWindow(window);
		system.Render.UpdateDisplay();
		var afterClose = system.RenderingDiagnostics?.LastMetrics;
		_output.WriteLine($"After close: {afterClose?.BytesWritten} bytes, dirty={afterClose?.DirtyCellsMarked}");

		// Settle — must be zero
		system.Render.UpdateDisplay();
		var settled = system.RenderingDiagnostics?.LastMetrics;
		_output.WriteLine($"Settled after close: {settled?.BytesWritten} bytes");
		Assert.Equal(0, settled?.BytesWritten);
		Assert.True(settled?.IsStaticFrame);
	}

	[Fact]
	public void WideChar_WindowClose_BackgroundContentCorrect()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();

		var background = new Window(system)
		{
			Left = 0, Top = 0, Width = 60, Height = 20, Title = "BG"
		};
		// Fill enough lines to cover the CJK window area
		var bgLines = Enumerable.Range(0, 16).Select(_ => new string('B', 56)).ToList();
		background.AddControl(new MarkupControl(bgLines));

		var window = new Window(system)
		{
			Left = 10, Top = 3, Width = 25, Height = 6, Title = "CJK"
		};
		window.AddControl(new MarkupControl(new List<string> { "中文日本語" }));

		system.WindowStateService.AddWindow(background);
		system.WindowStateService.AddWindow(window);
		system.Render.UpdateDisplay();

		// Close
		system.CloseWindow(window);
		system.Render.UpdateDisplay();

		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);

		// Where the CJK window was, background 'B' should be restored
		// Check row 5 which was inside the CJK window
		for (int x = 11; x < 34; x++)
		{
			var cell = snapshot.GetBack(x, 5);
			_output.WriteLine($"({x},5): '{cell.Character}'");
			Assert.Equal(new Rune('B'), cell.Character);
		}
	}

	#endregion

	#region Overlapping Windows with Wide Chars

	[Fact]
	public void WideChar_OverlappingWindows_MoveTopWindow_NoArtifacts()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();

		var bottomWindow = new Window(system)
		{
			Left = 5, Top = 3, Width = 40, Height = 15, Title = "Bottom CJK"
		};
		bottomWindow.AddControl(new MarkupControl(new List<string>
		{
			"底部中文テキスト表示内容",
			"日本語のコンテンツを表示"
		}));

		var topWindow = new Window(system)
		{
			Left = 15, Top = 6, Width = 25, Height = 8, Title = "Top CJK"
		};
		topWindow.AddControl(new MarkupControl(new List<string>
		{
			"上部한국어テスト",
			"中文 overlay text"
		}));

		system.WindowStateService.AddWindow(bottomWindow);
		system.WindowStateService.AddWindow(topWindow);
		system.Render.UpdateDisplay();

		// Move top window to expose bottom CJK content
		topWindow.Left = 30;
		system.Render.UpdateDisplay();

		// Settle
		system.Render.UpdateDisplay();
		var settled = system.RenderingDiagnostics?.LastMetrics;
		_output.WriteLine($"Settled: {settled?.BytesWritten} bytes");
		Assert.Equal(0, settled?.BytesWritten);
		Assert.True(settled?.IsStaticFrame);
	}

	[Fact]
	public void WideChar_OverlappingWindows_CloseTopWindow_NoArtifacts()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();

		var bottomWindow = new Window(system)
		{
			Left = 5, Top = 3, Width = 40, Height = 15, Title = "Bottom"
		};
		bottomWindow.AddControl(new MarkupControl(new List<string>
		{
			"中文字テスト底部ウィンドウ",
			"한국어 content 日本語",
		}));

		var topWindow = new Window(system)
		{
			Left = 10, Top = 5, Width = 30, Height = 10, Title = "Top"
		};
		topWindow.AddControl(new MarkupControl(new List<string> { "ASCII overlay" }));

		system.WindowStateService.AddWindow(bottomWindow);
		system.WindowStateService.AddWindow(topWindow);
		system.Render.UpdateDisplay();

		system.CloseWindow(topWindow);
		system.Render.UpdateDisplay();

		// Settle
		system.Render.UpdateDisplay();
		var settled = system.RenderingDiagnostics?.LastMetrics;
		_output.WriteLine($"Settled: {settled?.BytesWritten} bytes");
		Assert.Equal(0, settled?.BytesWritten);
	}

	#endregion

	#region Line-Mode Rendering

	[Fact]
	public void WideChar_LineModeRendering_MoveNoArtifacts()
	{
		// Line mode uses AppendRegionToBuilder — test that path too
		var system = TestWindowSystemBuilder.CreateTestSystemWithLineMode();

		var background = new Window(system)
		{
			Left = 0, Top = 0, Width = 60, Height = 20, Title = "BG"
		};
		background.AddControl(new MarkupControl(new List<string>
		{
			new string('B', 56),
			new string('B', 56),
			new string('B', 56),
		}));

		var window = new Window(system)
		{
			Left = 10, Top = 5, Width = 30, Height = 10, Title = "CJK Line"
		};
		window.AddControl(new MarkupControl(new List<string> { "中文字テスト" }));

		system.WindowStateService.AddWindow(background);
		system.WindowStateService.AddWindow(window);
		system.Render.UpdateDisplay();

		window.Left = 20;
		system.Render.UpdateDisplay();

		// Settle
		system.Render.UpdateDisplay();
		var settled = system.RenderingDiagnostics?.LastMetrics;
		_output.WriteLine($"Line-mode settled: {settled?.BytesWritten} bytes");
		Assert.Equal(0, settled?.BytesWritten);
	}

	#endregion

	#region Emoji (Surrogate Pair) — Zero Output Guarantee

	[Fact]
	public void Emoji_StaticContent_ZeroOutputOnFrame2()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system)
		{
			Left = 10, Top = 5, Width = 30, Height = 10, Title = "Emoji Static"
		};
		window.AddControl(new MarkupControl(new List<string> { "Hello \U0001F525\U0001F4A9 World" }));
		system.WindowStateService.AddWindow(window);

		system.Render.UpdateDisplay();
		var frame1 = system.RenderingDiagnostics?.LastMetrics;
		Assert.True(frame1?.BytesWritten > 0);

		system.Render.UpdateDisplay();
		var frame2 = system.RenderingDiagnostics?.LastMetrics;
		Assert.NotNull(frame2);

		_output.WriteLine($"Emoji Frame 1: {frame1?.BytesWritten} bytes, Frame 2: {frame2.BytesWritten} bytes");
		Assert.Equal(0, frame2.BytesWritten);
		Assert.True(frame2.IsStaticFrame);
	}

	[Fact]
	public void Emoji_WindowMove_NoArtifactsAfterSettle()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();

		var background = new Window(system)
		{
			Left = 0, Top = 0, Width = 60, Height = 20, Title = "BG"
		};
		background.AddControl(new MarkupControl(new List<string>
		{
			new string('B', 56),
			new string('B', 56),
			new string('B', 56),
			new string('B', 56),
			new string('B', 56),
		}));

		var window = new Window(system)
		{
			Left = 10, Top = 5, Width = 30, Height = 10, Title = "Emoji Move"
		};
		window.AddControl(new MarkupControl(new List<string>
		{
			"\U0001F525 Fire \U0001F4A9 mixed ABC",
			"\U0001F680 Rocket \U0001F389 Party",
		}));

		system.WindowStateService.AddWindow(background);
		system.WindowStateService.AddWindow(window);
		system.Render.UpdateDisplay();

		// Static frame
		system.Render.UpdateDisplay();
		Assert.Equal(0, system.RenderingDiagnostics?.LastMetrics?.BytesWritten);

		// Move
		window.Left = 20;
		system.Render.UpdateDisplay();
		Assert.True(system.RenderingDiagnostics?.LastMetrics?.BytesWritten > 0);

		// Settle
		system.Render.UpdateDisplay();
		var settled = system.RenderingDiagnostics?.LastMetrics;
		_output.WriteLine($"Emoji settled: {settled?.BytesWritten} bytes");
		Assert.Equal(0, settled?.BytesWritten);
		Assert.True(settled?.IsStaticFrame);
	}

	[Fact]
	public void Emoji_WindowClose_NoArtifacts()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();

		var background = new Window(system)
		{
			Left = 0, Top = 0, Width = 60, Height = 20, Title = "BG"
		};
		background.AddControl(new MarkupControl(new List<string>
		{
			new string('B', 56),
			new string('B', 56),
			new string('B', 56),
			new string('B', 56),
		}));

		var window = new Window(system)
		{
			Left = 10, Top = 5, Width = 30, Height = 10, Title = "Emoji Close"
		};
		window.AddControl(new MarkupControl(new List<string>
		{
			"\U0001F600 Hello \U0001F525 emoji \U0001F4A9",
		}));

		system.WindowStateService.AddWindow(background);
		system.WindowStateService.AddWindow(window);
		system.Render.UpdateDisplay();

		system.CloseWindow(window);
		system.Render.UpdateDisplay();

		// Settle
		system.Render.UpdateDisplay();
		var settled = system.RenderingDiagnostics?.LastMetrics;
		_output.WriteLine($"Emoji close settled: {settled?.BytesWritten} bytes");
		Assert.Equal(0, settled?.BytesWritten);
		Assert.True(settled?.IsStaticFrame);
	}

	[Fact]
	public void Emoji_MixedWithCjk_WindowMove_NoArtifacts()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();

		var background = new Window(system)
		{
			Left = 0, Top = 0, Width = 80, Height = 25, Title = "BG"
		};
		background.AddControl(new MarkupControl(new List<string>
		{
			new string('X', 76),
			new string('X', 76),
			new string('X', 76),
			new string('X', 76),
		}));

		var window = new Window(system)
		{
			Left = 10, Top = 5, Width = 35, Height = 10, Title = "Mixed"
		};
		window.AddControl(new MarkupControl(new List<string>
		{
			"中文 \U0001F525 Japanese 日本語 \U0001F680",
			"\U0001F4A9 한국어 emoji テスト \U0001F389",
		}));

		system.WindowStateService.AddWindow(background);
		system.WindowStateService.AddWindow(window);
		system.Render.UpdateDisplay();

		// Multiple moves
		window.Left = 20;
		system.Render.UpdateDisplay();
		window.Left = 30;
		system.Render.UpdateDisplay();

		// Settle
		system.Render.UpdateDisplay();
		var settled = system.RenderingDiagnostics?.LastMetrics;
		_output.WriteLine($"Mixed emoji+CJK settled: {settled?.BytesWritten} bytes");
		Assert.Equal(0, settled?.BytesWritten);
	}

	#endregion
}
