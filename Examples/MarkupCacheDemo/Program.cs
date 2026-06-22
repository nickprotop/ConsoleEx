// -----------------------------------------------------------------------
// MarkupCache Demo — a 1000-line MarkupControl in a ScrollablePanelControl
// stuffed with CJK, emojis, ZWJ sequences, math/Greek/Cyrillic/Arabic/Hebrew,
// box-drawing, and multi-span markup. The bottom status bar polls
// MarkupControl.TotalParseCount once a second so you can watch the parse
// cache stay warm during scroll and drag-select — the rate should hold at
// "+0/s" once the LRU has warmed.
// -----------------------------------------------------------------------

using System.Text;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Rendering;

namespace MarkupCacheDemo;

internal static class Program
{
	static int Main(string[] args)
	{
		if (PtyShim.RunIfShim(args)) return 127;

		try
		{
			var windowSystem = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer));
			windowSystem.PanelStateService.TopStatus = "MarkupControl ParseCache Demo — CJK / Emoji / Unicode";
			windowSystem.PanelStateService.BottomStatus =
				"PgUp/PgDn or wheel to scroll  •  drag to select, Ctrl+C to copy  •  ESC to quit";

			var lines = BuildLines(1000);

			var markup = new MarkupControl(lines)
			{
				Margin = new Margin(0, 0, 0, 0),
				EnableSelection = true,
			};

			var panel = new ScrollablePanelBuilder()
				.WithVerticalScroll(ScrollMode.Scroll)
				.WithHorizontalScroll(ScrollMode.None)
				.WithScrollbar(true)
				.ScrollbarRight()
				.WithMouseWheel(true)
				.WithBorderStyle(BorderStyle.Rounded)
				.WithHeader(" MarkupControl inside ScrollablePanelControl ")
				.WithPadding(1, 0, 1, 0)
				.WithAlignment(HorizontalAlignment.Stretch)
				.WithVerticalAlignment(VerticalAlignment.Fill)
				.AddControl(markup)
				.Build();

			new WindowBuilder(windowSystem)
				.WithTitle("MarkupControl ParseCache Demo")
				.Maximized()
				.WithBorderStyle(BorderStyle.Single)
				.OnKeyPressed((s, e) =>
				{
					if (e.KeyInfo.Key == ConsoleKey.Escape)
					{
						windowSystem.Shutdown(0);
						e.Handled = true;
					}
				})
				.AddControl(panel)
				.BuildAndShow();

			StartParseMeter(windowSystem);
			windowSystem.Run();
			return 0;
		}
		catch (Exception ex)
		{
			Console.Clear();
			ExceptionFormatter.WriteException(ex);
			return 1;
		}
	}

	// Background poller: writes a parses-per-second meter to the bottom status bar so the cache
	// hit/miss behavior is visible while you scroll. Stays at "+0/s" once the LRU has warmed.
	private static void StartParseMeter(ConsoleWindowSystem windowSystem)
	{
		var t = new System.Threading.Thread(() =>
		{
			long last = MarkupControl.TotalParseCount;
			while (true)
			{
				System.Threading.Thread.Sleep(1000);
				long now = MarkupControl.TotalParseCount;
				long delta = now - last;
				last = now;
				windowSystem.PanelStateService.BottomStatus =
					$"parses: {now,8}  (+{delta}/s)   •   scroll/drag should stay +0/s when cache is warm   •   ESC quits";
			}
		})
		{ IsBackground = true, Name = "parse-meter" };
		t.Start();
	}

	// Build N lines of mixed CJK / emoji / unicode / markup content.
	// The mix is deterministic (seeded RNG) so cache-hit numbers are reproducible across runs.
	private static List<string> BuildLines(int count)
	{
		var rng = new Random(1729);
		var lines = new List<string>(count);

		string[] cjk =
		{
			"你好世界,这是一段中文示例文本,用来测试缓存命中率。",
			"今日は良い天気ですね。コンソールアプリのテストを始めましょう。",
			"안녕하세요. 이 문장은 한국어 테스트 라인입니다.",
			"桜の花が満開で、春の訪れを告げています。",
			"中文、日本語、한국어 が同じ行に混在する例です。",
			"繁體字測試:這是一段繁體中文的範例文字。",
			"Konnichiwa! 你好! 안녕! Hello! Γειά! مرحبا!",
			"数据结构与算法分析:动态规划、贪心算法、回溯法、分治法。",
			"プログラミング言語:C#, F#, Rust, Go, Python, TypeScript。",
			"광활한 우주 속에서 우리는 작은 점에 불과합니다.",
		};

		string[] emojis =
		{
			"🚀 Launch sequence initiated 🛰️",
			"🎉 Party time! 🥳🎊🎈",
			"🐉🦄🦊🐼🐨🐯🐮🐷🐸🐵 menagerie",
			"🌸🌺🌻🌼🌷🌹💐 spring bouquet",
			"⚡🔥💧🌊🌪️🌈 elements",
			"🍕🍔🍟🌮🌯🍣🍜🍱 lunchtime",
			"📦🔧🔨🛠️⚙️📐📏🧰 toolbox",
			"❤️💛💚💙💜🖤🤍🤎 hearts",
			"👨‍💻👩‍💻🧑‍💻 multi-codepoint ZWJ sequences",
			"👨‍👩‍👧‍👦 family ZWJ sequence",
		};

		string[] unicode =
		{
			"Mathematical: ∀x∈ℝ, x² ≥ 0 ⟹ √(x²) = |x|",
			"Greek: αβγδεζηθικλμνξοπρστυφχψω ΑΒΓΔΕΖΗΘΙΚΛΜΝΞΟΠΡΣΤΥΦΧΨΩ",
			"Cyrillic: Съешь же ещё этих мягких французских булок, да выпей чаю.",
			"Arabic: مرحبا بالعالم — هذا اختبار للنص العربي.",
			"Hebrew: שלום עולם — בדיקה לטקסט עברי.",
			"Box drawing: ┌─┬─┐ │ │ │ ├─┼─┤ │ │ │ └─┴─┘",
			"Heavy: ┏━┳━┓ ┃ ┃ ┃ ┣━╋━┫ ┃ ┃ ┃ ┗━┻━┛",
			"Doubles: ╔═╦═╗ ║ ║ ║ ╠═╬═╣ ║ ║ ║ ╚═╩═╝",
			"Arrows: ← → ↑ ↓ ↔ ↕ ⇐ ⇒ ⇑ ⇓ ⇔ ⇕ ⟵ ⟶ ⟷",
			"Currency: $ € £ ¥ ₹ ₽ ₪ ₩ ₿ ﷼",
		};

		string[] palette =
		{
			"red", "green", "yellow", "blue", "magenta", "cyan", "white",
			"rgb(255,128,0)", "rgb(120,220,160)", "rgb(180,140,220)",
			"rgb(100,180,255)", "rgb(220,160,100)",
		};

		string[] styles = { "bold", "italic", "underline", "dim", "" };

		for (int i = 0; i < count; i++)
		{
			if (i % 50 == 0)
			{
				lines.Add($"[bold rgb(100,180,255)]━━━ Section {(i / 50) + 1} (line {i + 1}) ━━━[/]");
				continue;
			}
			if (i % 50 == 1)
			{
				lines.Add("");
				continue;
			}

			int kind = rng.Next(6);
			string body = kind switch
			{
				0 => cjk[rng.Next(cjk.Length)],
				1 => emojis[rng.Next(emojis.Length)],
				2 => unicode[rng.Next(unicode.Length)],
				3 => $"{cjk[rng.Next(cjk.Length)]}  {emojis[rng.Next(emojis.Length)]}",
				4 => $"{unicode[rng.Next(unicode.Length)]}  →  {cjk[rng.Next(cjk.Length)]}",
				_ => BuildLongPlainLine(rng),
			};

			string color = palette[rng.Next(palette.Length)];
			string style = styles[rng.Next(styles.Length)];
			string tag = string.IsNullOrEmpty(style) ? color : $"{style} {color}";
			string prefix = $"[dim]{i + 1,4}[/] ";

			// Mix:
			//  ~30% plain    — exercises the parser through unmarked text
			//  ~50% wrapped  — whole line in one tag
			//  ~20% per-word — many inline tags per line, heaviest parse work
			int dec = rng.Next(10);
			string line;
			if (dec < 3)
				line = prefix + body;
			else if (dec < 8)
				line = $"{prefix}[{tag}]{Escape(body)}[/]";
			else
			{
				var sb = new StringBuilder();
				sb.Append(prefix);
				var words = body.Split(' ');
				for (int w = 0; w < words.Length; w++)
				{
					if (w > 0) sb.Append(' ');
					string c2 = palette[rng.Next(palette.Length)];
					sb.Append('[').Append(c2).Append(']').Append(Escape(words[w])).Append("[/]");
				}
				line = sb.ToString();
			}

			lines.Add(line);
		}

		return lines;
	}

	private static string BuildLongPlainLine(Random rng)
	{
		const string lorem =
			"The quick brown fox jumps over the lazy dog while a Greek φ meets Chinese 字 and a Korean 글자 share lunch.";
		int reps = 1 + rng.Next(3);
		var sb = new StringBuilder(lorem.Length * reps);
		for (int i = 0; i < reps; i++)
		{
			if (i > 0) sb.Append(' ');
			sb.Append(lorem);
		}
		return sb.ToString();
	}

	// Spectre markup uses [ and ] as tag delimiters; escape literal brackets in source content.
	private static string Escape(string s) => s.Replace("[", "[[").Replace("]", "]]");
}
