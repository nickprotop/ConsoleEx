using DemoApp.Helpers;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Controls.Forms;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;

namespace DemoApp.DemoWindows;

/// <summary>
/// Aggregator page for the <see cref="FormControl"/> and its declarative XML loader (<see cref="FormXml"/>).
/// A column of launcher buttons opens form dialogs from simple to complex — imperative
/// <c>Controls.Form()</c> tiers (basic fields, validation, sections &amp; hints, multi-field rows) and
/// two <c>FormXml.FromXml(...)</c> tiers (loaded-from-XML, and XML + a named C# validator). Each dialog
/// is a modal window hosting the form plus a live result panel updated on submit; Esc closes it.
/// </summary>
internal static class FormDemoWindow
{
	private const int WindowWidth = 60;
	private const int WindowHeight = 26;
	private const int DialogWidth = 74;
	private const int DialogHeight = 28;
	private const int ButtonWidth = 34;
	private const int ButtonLeftMargin = 2;

	public static Window Create(ConsoleWindowSystem ws)
	{
		var header = Controls.Markup("[bold]Forms[/]  [dim]— labeled inputs, validation, sections; code + XML[/]")
			.StickyTop()
			.WithMargin(1, 1, 1, 0)
			.Build();

		var hint = Controls.Markup("[dim]Pick a tier to open its form dialog · Esc closes any dialog[/]")
			.StickyBottom()
			.WithMargin(1, 0, 1, 0)
			.Build();

		var intro = Controls.Markup()
			.AddLine("[dim]Each button opens a modal form dialog, simplest first.[/]")
			.AddLine("[dim]The last two are built from a declarative XML string via FormXml.[/]")
			.WithMargin(1, 1, 1, 0)
			.Build();

		var codeLabel = Controls.Label("[bold]Imperative (code)[/]");
		codeLabel.Margin = new Margin { Left = 1, Top = 1 };

		var basicBtn = LauncherButton("1 · Basic fields (code)", () => OpenBasicFields(ws));
		var validationBtn = LauncherButton("2 · Validation (code)", () => OpenValidation(ws));
		var sectionsBtn = LauncherButton("3 · Sections & hints (code)", () => OpenSectionsAndHints(ws));
		var rowsBtn = LauncherButton("4 · Multi-field rows (code)", () => OpenMultiFieldRows(ws));

		var xmlLabel = Controls.Label("[bold]Declarative (XML)[/]");
		xmlLabel.Margin = new Margin { Left = 1, Top = 1 };

		var xmlBtn = LauncherButton("5 · Loaded from XML", () => OpenLoadedFromXml(ws));
		var xmlRuleBtn = LauncherButton("6 · XML + named validator", () => OpenXmlWithNamedValidator(ws));

		var window = new WindowBuilder(ws)
			.WithTitle("Form")
			.WithSize(WindowWidth, WindowHeight)
			.Centered()
			.AddControls(
				header, intro,
				codeLabel, basicBtn, validationBtn, sectionsBtn, rowsBtn,
				xmlLabel, xmlBtn, xmlRuleBtn,
				hint)
			.OnKeyPressed((sender, e) =>
			{
				if (e.KeyInfo.Key == ConsoleKey.Escape)
				{
					ws.CloseWindow((Window)sender!);
					e.Handled = true;
				}
			})
			.BuildAndShow();
		DemoTheme.ApplyThemeGradient(window, ws);
		return window;
	}

	private static ButtonControl LauncherButton(string text, System.Action onClick)
	{
		var btn = Controls.Button(text)
			.WithWidth(ButtonWidth)
			.OnClick((_, _) => onClick())
			.Build();
		btn.Margin = new Margin { Left = ButtonLeftMargin };
		return btn;
	}

	// ---- Tier 1: basic fields (imperative) ---------------------------------------------------

	private static void OpenBasicFields(ConsoleWindowSystem ws)
	{
		var result = ResultPanel();
		var form = Controls.Form()
			.AddText("name", "Name:", hint: "your full name")
			.AddText("email", "Email:", hint: "we won't share it")
			.WithButtons()
			.OnSubmit(values => ShowValues(result, values))
			.Build();

		OpenDialog(ws, "Basic fields", form, result);
	}

	// ---- Tier 2: validation (imperative) -----------------------------------------------------

	private static void OpenValidation(ConsoleWindowSystem ws)
	{
		var result = ResultPanel();
		var form = Controls.Form()
			.AddText("host", "Host:", required: true, hint: "required")
			.AddText("port", "Port:", initial: "5432",
				validate: v => int.TryParse(v, out var n) && n is > 0 and <= 65535
					? null
					: "must be a port between 1 and 65535",
				hint: "1–65535")
			.WithButtons(ok: "Validate")
			.OnSubmit(values => ShowValues(result, values))
			.Build();
		form.ValidateOnChange = true;

		OpenDialog(ws, "Validation", form, result);
	}

	// ---- Tier 3: sections & hints (imperative) — preserves the original showcase form --------

	private static void OpenSectionsAndHints(ConsoleWindowSystem ws)
	{
		var result = ResultPanel();
		var form = Controls.Form()
			.AddSection("Connection")
			.AddText("host", "Host", required: true, hint: "e.g. localhost")
			.AddText("port", "Port", initial: "5432",
				validate: v => int.TryParse(v, out _) ? null : "must be a number",
				hint: "default 5432")
			.AddDropdown("driver", "Driver", new[] { "PostgreSQL", "MySQL", "SQLite", "SQL Server" }, initial: "PostgreSQL")
			.AddSection("Advanced", collapsible: true, startCollapsed: true)
			.AddCheckbox("ssl", "Use SSL/TLS", hint: "encrypt the connection")
			.AddRadio("mode", "Mode", "Read-write", "Read-only", "Replica")
			.AddSlider("timeout", "Timeout (s)", 0, 60, 30, hint: "connection timeout in seconds")
			.WithButtons()
			.OnSubmit(values => ShowValues(result, values))
			.Build();

		OpenDialog(ws, "Sections & hints", form, result);
	}

	// ---- Tier 4: multi-field rows (imperative) -----------------------------------------------

	private static void OpenMultiFieldRows(ConsoleWindowSystem ws)
	{
		var result = ResultPanel();
		var form = Controls.Form()
			.AddRow(
				f => f.AddText("first", "First:"),
				f => f.AddText("last", "Last:"))
			.AddText("email", "Email:")
			.WithButtons()
			.OnSubmit(values => ShowValues(result, values))
			.Build();

		OpenDialog(ws, "Multi-field rows", form, result);
	}

	// ---- Tier 5: loaded from XML -------------------------------------------------------------

	private static void OpenLoadedFromXml(ConsoleWindowSystem ws)
	{
		var result = ResultPanel();
		var form = FormXml.FromXml(@"
<form columnGap='1'>
  <text name='name'  label='Name:'  required='true' hint='required'/>
  <text name='email' label='Email:' pattern='^[^@\s]+@[^@\s]+$' message='enter a valid email'/>
  <dropdown name='role' label='Role:' options='Admin,User,Guest' initial='User'/>
  <section title='Advanced' collapsible='true' collapsed='true'>
    <checkbox name='newsletter' label='Subscribe to newsletter'/>
    <slider name='volume' label='Volume:' min='0' max='100' initial='50'/>
  </section>
  <buttons/>
</form>");
		form.Submitted += (_, values) => ShowValues(result, values);

		OpenDialog(ws, "Loaded from XML", form, result);
	}

	// ---- Tier 6: XML + named validator -------------------------------------------------------

	private static void OpenXmlWithNamedValidator(ConsoleWindowSystem ws)
	{
		var result = ResultPanel();
		var registry = new Dictionary<string, Func<string?, string?>>
		{
			["validDsn"] = v =>
				string.IsNullOrEmpty(v) || v.Contains('=')
					? null
					: "expected key=value pairs, e.g. Host=localhost;Port=5432",
		};

		var form = FormXml.FromXml(@"
<form columnGap='1'>
  <text name='host' label='Host:' initial='localhost' required='true'/>
  <text name='dsn'  label='Extra DSN:' rule='validDsn' hint='key=value;key=value'/>
  <buttons ok='Connect'/>
</form>", registry);
		form.Submitted += (_, values) => ShowValues(result, values);

		OpenDialog(ws, "XML + named validator", form, result);
	}

	// ---- shared dialog plumbing --------------------------------------------------------------

	private static MarkupControl ResultPanel() =>
		Controls.Markup("[dim]Submit the form to see the collected values here.[/]")
			.WithMargin(1, 1, 1, 0)
			.Build();

	private static void ShowValues(MarkupControl result, IReadOnlyDictionary<string, string?> values)
	{
		var lines = new List<string> { "[bold]Submitted[/]", "" };
		foreach (var (key, value) in values.OrderBy(p => p.Key))
			lines.Add($"[dim]{MarkupParser.Escape(key)}:[/] {MarkupParser.Escape(value ?? string.Empty)}");
		result.SetContent(lines);
	}

	private static void OpenDialog(ConsoleWindowSystem ws, string title, FormControl form, MarkupControl result)
	{
		var hint = Controls.Markup("[dim]Tab between fields · OK submits · Esc: Close[/]")
			.StickyBottom()
			.WithMargin(1, 0, 1, 0)
			.Build();

		// The form can grow taller than the dialog (esp. once a section expands), so host it in a
		// scrollable viewport with the result panel below it.
		var panel = Controls.ScrollablePanel()
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithMargin(1, 1, 1, 0)
			.Build();
		panel.AddControl(form);
		panel.AddControl(Controls.RuleBuilder().WithMargin(1, 1, 1, 0).Build());
		panel.AddControl(result);

		var dialog = new WindowBuilder(ws)
			.WithTitle(title)
			.WithSize(DialogWidth, DialogHeight)
			.Centered()
			.AsModal()
			.AddControls(panel, hint)
			.OnKeyPressed((sender, e) =>
			{
				if (e.KeyInfo.Key == ConsoleKey.Escape)
				{
					ws.CloseWindow((Window)sender!);
					e.Handled = true;
				}
			})
			.BuildAndShow();

		form.Cancelled += (_, _) => ws.CloseWindow(dialog);
		DemoTheme.ApplyThemeGradient(dialog, ws);
	}
}
