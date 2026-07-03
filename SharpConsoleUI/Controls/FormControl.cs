// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.ObjectModel;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;
using SharpConsoleUI.Themes;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A labeled-input form: a two-column grid (label | editor) that composes real input controls.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <see cref="FormControl"/> is an <b>honest subclass</b> of <see cref="GridControl"/> — it adds no
	/// custom paint or measure. Each field is a real <see cref="MarkupControl"/> label placed in column 0
	/// and a real input control (e.g. <see cref="PromptControl"/>, <see cref="CheckboxControl"/>) placed in
	/// column 1, via the inherited <see cref="GridControl.Place"/>. The layout, focus, and rendering are the
	/// grid's; the form only wires up fields and value getters.
	/// </para>
	/// <para>
	/// Column 0 is <see cref="GridLength.Auto"/> (sized to the widest label); column 1 is
	/// <see cref="GridLength.Star"/> (takes the remaining width). One row is added per field.
	/// </para>
	/// <para>
	/// Value access is AOT-safe: each field carries a plain <see cref="System.Func{TResult}"/> getter — no
	/// reflection. <see cref="GetValues"/> invokes every getter to produce a name→value snapshot.
	/// </para>
	/// </remarks>
	public class FormControl : GridControl
	{
		/// <summary>
		/// A single labeled field: its name, the label control, the control placed in the editor cell, the
		/// object whose value the field reads (the placed editor for most controls, or the typed group for
		/// radios), and the value getter. Extended in later work with validation/hint/error/section state.
		/// </summary>
		/// <param name="Name">The field's key in <see cref="GetValues"/>.</param>
		/// <param name="Label">The markup label placed in column 0.</param>
		/// <param name="PlacedEditor">The control placed in the editor cell (column 1).</param>
		/// <param name="ValueEditor">
		/// The object returned by <see cref="GetEditor"/> — the placed editor for most fields, but the
		/// <see cref="RadioGroup{T}"/> for radios (so callers get the typed selection surface, not the panel).
		/// </param>
		/// <param name="Getter">The plain delegate that reads the field's current value.</param>
		/// <param name="Validate">Optional custom validator run on the field's current value.</param>
		/// <param name="Required">Whether the field is required (empty/null value fails validation).</param>
		/// <param name="ErrorView">The hidden, col-spanning error line shown beneath the field.</param>
		/// <param name="ErrorRow">The grid row index of <paramref name="ErrorView"/>.</param>
		/// <param name="LiveSubscribe">
		/// Optional editor-specific hook that subscribes the given callback to the editor's value-changed event,
		/// for generic editors (e.g. <see cref="RadioGroup{T}"/>) whose closed type is only known at add time.
		/// </param>
		/// <param name="HintView">The always-visible dim hint line placed beneath the editor, or <c>null</c> when the field has no hint.</param>
		/// <param name="HintRow">The grid row index of <paramref name="HintView"/>, or <c>-1</c> when the field has no hint.</param>
		/// <param name="SectionId">
		/// The id of the section this field belongs to, or <c>-1</c> when the field belongs to no section. Fields
		/// tagged with a collapsible section's id are hidden/shown together when that section is toggled.
		/// </param>
		internal record FormField(
			string Name,
			MarkupControl Label,
			IWindowControl PlacedEditor,
			object ValueEditor,
			Func<string?> Getter,
			Func<string?, string?>? Validate = null,
			bool Required = false,
			MarkupControl? ErrorView = null,
			int ErrorRow = -1,
			Action<Action>? LiveSubscribe = null,
			MarkupControl? HintView = null,
			int HintRow = -1,
			int SectionId = -1);

		/// <summary>
		/// A collapsible field group rendered as a full-width, col-spanning header row in the same flat grid.
		/// A section is <b>not</b> a nested panel: collapsing it toggles <see cref="IWindowControl.Visible"/> on
		/// every control of every field tagged with its <see cref="Id"/>, and flips the header's ▸/▾ glyph.
		/// </summary>
		private sealed class FormSection
		{
			public int Id { get; init; }
			public string? Title { get; init; }
			public MarkupControl HeaderTitle { get; init; } = null!;
			public ButtonControl? Toggle { get; init; }
			public bool Collapsed { get; set; }
		}

		private readonly List<FormField> _fields = new();
		private readonly Dictionary<string, FormField> _fieldsByName = new();
		private readonly List<FormSection> _sections = new();
		private readonly List<List<FormField>> _rowGroups = new();
		private int _nextRow;
		private bool _validateOnChange;
		private int _currentSectionId = -1;
		private int _nextSectionId;

		/// <summary>
		/// The active multi-field row-packing context, or <c>null</c> when fields are placed one-per-row.
		/// Set for the duration of an <see cref="AddRow"/> call so the field-registration path packs each
		/// added field onto the same grid row in successive column pairs instead of advancing rows.
		/// </summary>
		private RowGroupContext? _rowGroupContext;

		/// <summary>
		/// Per-<see cref="AddRow"/> packing state: the single shared grid row for the field cells, the index of
		/// the next field within the row (so field <c>i</c> occupies columns <c>2*i</c>/<c>2*i+1</c>), and the
		/// running list of fields captured for the row-group.
		/// </summary>
		private sealed class RowGroupContext
		{
			public int Row { get; init; }
			public int FieldIndex { get; set; }
			public List<FormField> Fields { get; } = new();
		}

		/// <summary>The OK button placed by <see cref="WithButtons"/>, or <c>null</c> when none was added (test seam target).</summary>
		private ButtonControl? _okButton;

		/// <summary>The Cancel button placed by <see cref="WithButtons"/>, or <c>null</c> when none was added.</summary>
		private ButtonControl? _cancelButton;

		/// <summary>The collapsed-state glyph shown in a collapsible section header.</summary>
		private const string CollapsedGlyph = "▸";

		/// <summary>The expanded-state glyph shown in a collapsible section header.</summary>
		private const string ExpandedGlyph = "▾";

		/// <summary>
		/// Initializes a new, empty form with a two-column grid: an auto-sized label column and a
		/// star-sized editor column, with no gap between rows.
		/// </summary>
		public FormControl()
		{
			ColumnDefinitions.Add(GridLength.Auto());
			ColumnDefinitions.Add(GridLength.Star(1));
			RowGap = 0;
		}

		/// <summary>
		/// Adds a field with a caller-supplied editor and value getter — the escape hatch used by the typed
		/// overloads and by callers needing a control this form does not build itself.
		/// </summary>
		/// <param name="name">The field key (used in <see cref="GetValues"/> and <see cref="GetEditor"/>).</param>
		/// <param name="label">The label text (escaped before display).</param>
		/// <param name="editor">The control to place in the editor column.</param>
		/// <param name="valueGetter">A delegate that reads the editor's current value.</param>
		/// <param name="validate">Optional validator (reserved; applied in a later task).</param>
		/// <param name="required">Whether the field is required (reserved; applied in a later task).</param>
		/// <param name="hint">Optional dim hint text shown beneath the editor.</param>
		/// <returns>This form, for fluent chaining.</returns>
		public FormControl AddField(
			string name,
			string label,
			IWindowControl editor,
			Func<string?> valueGetter,
			Func<string?, string?>? validate = null,
			bool required = false,
			string? hint = null)
			=> AddFieldCore(name, label, editor, valueEditor: editor, valueGetter, validate, required, hint: hint);

		/// <summary>
		/// Shared field-registration path. Places the label (column 0) and editor (column 1) in a new row,
		/// stores the field, and advances the row counter. <paramref name="valueEditor"/> is what
		/// <see cref="GetEditor"/> returns — the placed editor for most fields, the group for radios.
		/// </summary>
		private FormControl AddFieldCore(
			string name,
			string label,
			IWindowControl placedEditor,
			object valueEditor,
			Func<string?> valueGetter,
			Func<string?, string?>? validate,
			bool required,
			Action<Action>? liveSubscribe = null,
			string? hint = null)
		{
			var labelControl = new MarkupControl(new List<string> { MarkupParser.Escape(label) });

			MarkupControl? hintView;
			int hintRow;
			MarkupControl errorView;
			int errorRow;

			if (_rowGroupContext is { } group)
			{
				// Multi-field packing: field i shares the group's single row, occupying the label/editor
				// column pair (2*i, 2*i+1). Hint and error lines go on their own rows spanning that pair.
				int labelCol = group.FieldIndex * 2;
				int editorCol = labelCol + 1;
				EnsureColumnPair(group.FieldIndex);

				Place(labelControl, group.Row, labelCol);
				Place(placedEditor, group.Row, editorCol);

				hintView = null;
				hintRow = -1;
				if (hint != null)
				{
					hintRow = _nextRow;
					RowDefinitions.Add(GridLength.Auto());
					hintView = new MarkupControl(new List<string> { MarkupParser.Escape(hint) })
					{
						ColorRole = ColorRole.Info,
					};
					Place(hintView, hintRow, editorCol);
					_nextRow++;
				}

				errorRow = _nextRow;
				RowDefinitions.Add(GridLength.Auto());
				errorView = new MarkupControl(new List<string> { string.Empty })
				{
					ColorRole = ColorRole.Danger,
					Visible = false,
				};
				Place(errorView, errorRow, labelCol, colSpan: 2);
				_nextRow++;

				group.FieldIndex++;
			}
			else
			{
				int row = _nextRow;
				RowDefinitions.Add(GridLength.Auto());
				Place(labelControl, row, 0);
				Place(placedEditor, row, 1);
				_nextRow++;

				// Optional always-visible dim hint line, placed under the editor (column 1).
				hintView = null;
				hintRow = -1;
				if (hint != null)
				{
					hintRow = _nextRow;
					RowDefinitions.Add(GridLength.Auto());
					hintView = new MarkupControl(new List<string> { MarkupParser.Escape(hint) })
					{
						ColorRole = ColorRole.Info,
					};
					Place(hintView, hintRow, 1);
					_nextRow++;
				}

				// Hidden, col-spanning error line placed on the row directly beneath the field (and hint, if any).
				errorRow = _nextRow;
				RowDefinitions.Add(GridLength.Auto());
				errorView = new MarkupControl(new List<string> { string.Empty })
				{
					ColorRole = ColorRole.Danger,
					Visible = false,
				};
				Place(errorView, errorRow, 0, colSpan: 2);
				_nextRow++;
			}

			var field = new FormField(
				name, labelControl, placedEditor, valueEditor, valueGetter, validate, required, errorView, errorRow, liveSubscribe,
				hintView, hintRow, _currentSectionId);
			_fields.Add(field);
			_fieldsByName[name] = field;
			_rowGroupContext?.Fields.Add(field);

			// A field added while a collapsed section is active starts hidden.
			if (_currentSectionId >= 0)
			{
				var section = _sections.Find(s => s.Id == _currentSectionId);
				if (section is { Collapsed: true })
					SetFieldVisible(field, false);
			}

			if (_validateOnChange)
				SubscribeFieldForLiveValidation(field);

			return this;
		}

		/// <summary>Shows or hides every control that belongs to a field: label, editor, hint, and error line.</summary>
		private static void SetFieldVisible(FormField field, bool visible)
		{
			field.Label.Visible = visible;
			field.PlacedEditor.Visible = visible;
			if (field.HintView is { } hint)
				hint.Visible = visible;
			if (field.ErrorView is { } error)
			{
				// The error line only shows when it actually carries an error; collapsing always hides it.
				error.Visible = visible && !string.IsNullOrEmpty(error.Text);
			}
		}

		/// <summary>Adds a single-line text field backed by a <see cref="PromptControl"/>.</summary>
		/// <param name="name">The field key.</param>
		/// <param name="label">The label text.</param>
		/// <param name="initial">The initial text value.</param>
		/// <param name="validate">Optional validator run on the field's current text.</param>
		/// <param name="required">Whether the field is required (empty text fails validation).</param>
		/// <param name="hint">Optional dim hint text shown beneath the editor.</param>
		/// <returns>This form, for fluent chaining.</returns>
		public FormControl AddText(
			string name,
			string label,
			string initial = "",
			Func<string?, string?>? validate = null,
			bool required = false,
			string? hint = null)
		{
			var prompt = new PromptControl();
			prompt.SetInput(initial);
			return AddField(name, label, prompt, () => prompt.Input, validate, required, hint);
		}

		/// <summary>Adds a multi-line text field backed by a <see cref="MultilineEditControl"/>.</summary>
		/// <param name="name">The field key.</param>
		/// <param name="label">The label text.</param>
		/// <param name="initial">The initial content.</param>
		/// <param name="height">The editor's viewport height in rows.</param>
		/// <param name="hint">Optional dim hint text shown beneath the editor.</param>
		/// <returns>This form, for fluent chaining.</returns>
		public FormControl AddMultilineEdit(string name, string label, string initial = "", int height = 3, string? hint = null)
		{
			var mle = new MultilineEditControl(initial, height);
			return AddField(name, label, mle, () => mle.Content, hint: hint);
		}

		/// <summary>
		/// Adds a boolean field backed by a <see cref="CheckboxControl"/>. The checkbox carries its own
		/// label in the editor column, so the form's label column is left empty for this field.
		/// </summary>
		/// <param name="name">The field key.</param>
		/// <param name="label">The checkbox's own label (shown in the editor column).</param>
		/// <param name="initial">The initial checked state.</param>
		/// <param name="hint">Optional dim hint text shown beneath the editor.</param>
		/// <returns>This form, for fluent chaining. The value is <c>"true"</c>/<c>"false"</c>.</returns>
		public FormControl AddCheckbox(string name, string label, bool initial = false, string? hint = null)
		{
			var checkbox = new CheckboxControl(label, initial);
			// Checkbox carries its own label in the editor column; form label column stays empty.
			return AddField(name, string.Empty, checkbox, () => checkbox.Checked ? "true" : "false", hint: hint);
		}

		/// <summary>Adds a single-select field backed by a <see cref="DropdownControl"/>.</summary>
		/// <param name="name">The field key.</param>
		/// <param name="label">The label text.</param>
		/// <param name="options">The selectable options.</param>
		/// <param name="initial">The initially selected option, or <c>null</c> for none.</param>
		/// <param name="hint">Optional dim hint text shown beneath the editor.</param>
		/// <returns>This form, for fluent chaining.</returns>
		public FormControl AddDropdown(string name, string label, IEnumerable<string> options, string? initial = null, string? hint = null)
		{
			// The form's col-0 label already labels this field; passing the form label as the dropdown's
			// own prompt would render it twice ("Driver  Driver PostgreSQL"). Use an empty prompt so the
			// dropdown shows only its selected value.
			var dropdown = new DropdownControl(string.Empty, options);
			if (initial != null)
				dropdown.SelectedValue = initial;
			return AddField(name, label, dropdown, () => dropdown.SelectedValue, hint: hint);
		}

		/// <summary>
		/// Adds a typed single-select field rendered as a group of radios. The radios are hosted in a
		/// borderless <see cref="PanelControl"/> placed in the editor cell, while <see cref="GetEditor"/>
		/// returns the typed <see cref="RadioGroup{T}"/> so callers can read/write
		/// <see cref="RadioGroup{T}.SelectedValue"/>.
		/// </summary>
		/// <typeparam name="T">The option value type.</typeparam>
		/// <param name="name">The field key.</param>
		/// <param name="label">The label text.</param>
		/// <param name="options">The (value, display-label) option pairs.</param>
		/// <param name="hint">Optional dim hint text shown beneath the editor.</param>
		/// <returns>This form, for fluent chaining.</returns>
		public FormControl AddRadio<T>(string name, string label, IEnumerable<(T Value, string Label)> options, string? hint = null)
		{
			var group = new RadioGroup<T>();
			var panel = new PanelControl { BorderStyle = BorderStyle.None };
			foreach (var (value, optionLabel) in options)
				panel.AddControl(new RadioControl<T>(group, value, optionLabel));

			return AddFieldCore(
				name,
				label,
				placedEditor: panel,
				valueEditor: group,
				valueGetter: () => group.SelectedValue?.ToString(),
				validate: null,
				required: false,
				// T is known here, so we can bridge the typed SelectionChanged event to a plain callback.
				liveSubscribe: onChanged => group.SelectionChanged += (_, _) => onChanged(),
				hint: hint);
		}

		/// <summary>
		/// Adds a string radio field where each option is both its own value and display label.
		/// </summary>
		/// <param name="name">The field key.</param>
		/// <param name="label">The label text.</param>
		/// <param name="options">The option strings (value = label).</param>
		/// <returns>This form, for fluent chaining.</returns>
		public FormControl AddRadio(string name, string label, params string[] options)
			=> AddRadio<string>(name, label, options.Select(o => (o, o)));

		/// <summary>Adds a numeric field backed by a <see cref="SliderControl"/>.</summary>
		/// <param name="name">The field key.</param>
		/// <param name="label">The label text.</param>
		/// <param name="min">The slider minimum.</param>
		/// <param name="max">The slider maximum.</param>
		/// <param name="initial">The initial value.</param>
		/// <param name="hint">Optional dim hint text shown beneath the editor.</param>
		/// <returns>This form, for fluent chaining. The value is the number's string form.</returns>
		public FormControl AddSlider(string name, string label, double min, double max, double initial, string? hint = null)
		{
			var slider = new SliderControl { MinValue = min, MaxValue = max, Value = initial };
			return AddField(name, label, slider, () => slider.Value.ToString(), hint: hint);
		}

		/// <summary>
		/// Starts a collapsible field group as a full-width header row in the same flat grid. Every field added
		/// after this call (until the next <see cref="AddSection"/>) belongs to this section, so a collapsible
		/// section can hide/show all its fields together.
		/// </summary>
		/// <remarks>
		/// A section is <b>not</b> a nested panel: the header is a title <see cref="MarkupControl"/> in column 0
		/// and (when <paramref name="collapsible"/>) a ▸/▾ toggle <see cref="ButtonControl"/> in column 1, both on
		/// one grid row. Collapsing toggles <see cref="IWindowControl.Visible"/> on each member field's controls.
		/// Pass a <c>null</c> <paramref name="title"/> to end the current section: following fields belong to none.
		/// </remarks>
		/// <param name="title">The section title (escaped and bolded), or <c>null</c> to end the current section.</param>
		/// <param name="collapsible">When <c>true</c>, a toggle button is rendered that hides/shows the section's fields.</param>
		/// <param name="startCollapsed">When <c>true</c> (and collapsible), the section's fields start hidden and the glyph starts ▸.</param>
		/// <returns>This form, for fluent chaining.</returns>
		public FormControl AddSection(string? title, bool collapsible = false, bool startCollapsed = false)
		{
			// A null title clears the active section: subsequent fields belong to no section.
			if (title == null)
			{
				_currentSectionId = -1;
				return this;
			}

			int id = _nextSectionId++;
			bool collapsed = collapsible && startCollapsed;

			int row = _nextRow;
			RowDefinitions.Add(GridLength.Auto());

			var headerTitle = new MarkupControl(new List<string> { $"[bold]{MarkupParser.Escape(title)}[/]" });
			ButtonControl? toggle = null;

			if (collapsible)
			{
				// Flat two-cell header row: title in column 0, toggle button in column 1. No nested container.
				toggle = new ButtonControl { Text = collapsed ? CollapsedGlyph : ExpandedGlyph };
				var section = new FormSection
				{
					Id = id,
					Title = title,
					HeaderTitle = headerTitle,
					Toggle = toggle,
					Collapsed = collapsed,
				};
				toggle.Click += (_, _) => ToggleSection(section);
				_sections.Add(section);

				Place(headerTitle, row, 0);
				Place(toggle, row, 1);
			}
			else
			{
				_sections.Add(new FormSection
				{
					Id = id,
					Title = title,
					HeaderTitle = headerTitle,
					Toggle = null,
					Collapsed = false,
				});
				Place(headerTitle, row, 0, colSpan: 2);
			}

			_nextRow++;
			_currentSectionId = id;
			return this;
		}

		/// <summary>
		/// Adds several fields onto a single grid row, packed side by side. Each adder runs against this form
		/// while a row-packing context is active, so the field it adds is placed on the shared row in the next
		/// label/editor column pair (field <c>i</c> → columns <c>2*i</c>/<c>2*i+1</c>) instead of a new row.
		/// </summary>
		/// <remarks>
		/// This is the only path that widens the grid beyond its two base columns: extra
		/// <see cref="GridLength.Auto"/> label / <see cref="GridLength.Star"/> editor column pairs are added as
		/// needed. After all adders run, single-field-per-row placement is restored and the row advances. The
		/// packed fields are tracked as one row-group (see <see cref="RowGroupCountForTest"/>). Fields stay packed
		/// side by side regardless of width; responsive stacking on narrow terminals is a planned follow-up (a
		/// wrap layout the form will compose).
		/// </remarks>
		/// <param name="fieldAdders">The per-field add callbacks (e.g. <c>f =&gt; f.AddText(...)</c>), one per column.</param>
		/// <returns>This form, for fluent chaining.</returns>
		public FormControl AddRow(params Action<FormControl>[] fieldAdders)
		{
			ArgumentNullException.ThrowIfNull(fieldAdders);
			if (fieldAdders.Length == 0)
				return this;

			// Reserve the single shared grid row all packed fields will occupy.
			int row = _nextRow;
			RowDefinitions.Add(GridLength.Auto());
			_nextRow++;

			var context = new RowGroupContext { Row = row };
			_rowGroupContext = context;
			try
			{
				foreach (var adder in fieldAdders)
					adder(this);
			}
			finally
			{
				_rowGroupContext = null;
			}

			_rowGroups.Add(context.Fields);
			return this;
		}

		/// <summary>
		/// Ensures the grid has label/editor column definitions for the field at <paramref name="fieldIndex"/>
		/// in a packed row. The base grid starts with columns 0/1; this adds <see cref="GridLength.Auto"/> /
		/// <see cref="GridLength.Star"/> pairs so columns up to <c>2*fieldIndex+1</c> exist before placement.
		/// </summary>
		private void EnsureColumnPair(int fieldIndex)
		{
			int requiredColumns = (fieldIndex + 1) * 2;
			while (ColumnDefinitions.Count < requiredColumns)
			{
				ColumnDefinitions.Add(GridLength.Auto());
				ColumnDefinitions.Add(GridLength.Star(1));
			}
		}

		/// <summary>
		/// Adds a final, full-width row of right-aligned action buttons: an OK button (which submits the form)
		/// and, when <paramref name="showCancel"/> is <c>true</c>, a Cancel button (which raises
		/// <see cref="Cancelled"/>). The buttons live in a right-aligned <see cref="HorizontalGridControl"/>
		/// placed col-spanning the whole form.
		/// </summary>
		/// <param name="ok">The OK button caption.</param>
		/// <param name="cancel">The Cancel button caption.</param>
		/// <param name="showCancel">Whether to include the Cancel button.</param>
		/// <returns>This form, for fluent chaining.</returns>
		/// <remarks>
		/// Call this LAST, after any <c>AddRow</c> that widens the grid: the button row's col-span is
		/// fixed at call time to the current <see cref="GridControl.ColumnDefinitions"/> count, so adding
		/// columns afterwards would leave the button row short of the form's full width.
		/// </remarks>
		public FormControl WithButtons(string ok = "OK", string cancel = "Cancel", bool showCancel = true)
		{
			var okButton = new ButtonControl { Text = ok };
			okButton.Click += (_, _) => Submit();
			_okButton = okButton;

			var buttons = new List<ButtonControl> { okButton };
			if (showCancel)
			{
				var cancelButton = new ButtonControl { Text = cancel };
				cancelButton.Click += (_, _) => Cancelled?.Invoke(this, EventArgs.Empty);
				_cancelButton = cancelButton;
				buttons.Add(cancelButton);
			}

			var buttonRow = HorizontalGridControl.ButtonRow(buttons, HorizontalAlignment.Right);

			int row = _nextRow;
			RowDefinitions.Add(GridLength.Auto());
			// Span every existing column so the right-aligned row uses the form's full width.
			Place(buttonRow, row, 0, colSpan: Math.Max(1, ColumnDefinitions.Count));
			_nextRow++;

			return this;
		}

		/// <summary>Flips a section between collapsed/expanded: toggles its fields' visibility and the header glyph.</summary>
		private void ToggleSection(FormSection section)
		{
			section.Collapsed = !section.Collapsed;
			bool visible = !section.Collapsed;

			foreach (var field in _fields)
			{
				if (field.SectionId == section.Id)
					SetFieldVisible(field, visible);
			}

			if (section.Toggle is { } toggle)
				toggle.Text = section.Collapsed ? CollapsedGlyph : ExpandedGlyph;
		}

		/// <summary>
		/// Reads every field's current value into a name→value snapshot. AOT-safe: each value comes from the
		/// field's plain getter delegate.
		/// </summary>
		/// <returns>A read-only map of field name to current value.</returns>
		public IReadOnlyDictionary<string, string?> GetValues()
		{
			var values = new Dictionary<string, string?>(_fields.Count);
			foreach (var field in _fields)
				values[field.Name] = field.Getter();
			return new ReadOnlyDictionary<string, string?>(values);
		}

		/// <summary>
		/// Gets the value-editor for a field: the placed input control for most fields, or the typed
		/// <see cref="RadioGroup{T}"/> for radio fields (so callers get the typed selection surface).
		/// </summary>
		/// <param name="name">The field key.</param>
		/// <returns>The field's value-editor object.</returns>
		/// <exception cref="KeyNotFoundException">Thrown when no field with <paramref name="name"/> exists.</exception>
		public object GetEditor(string name) => _fieldsByName[name].ValueEditor;

		/// <summary>Raised by <see cref="Submit"/> when validation passes; carries the current values snapshot.</summary>
		public event EventHandler<IReadOnlyDictionary<string, string?>>? Submitted;

		/// <summary>Raised when the form is cancelled (e.g. by a Cancel button wired in a later task).</summary>
		public event EventHandler? Cancelled;

		/// <summary>
		/// When <c>true</c>, each field re-validates itself as soon as its editor's value changes (live
		/// validation). Fields whose editor exposes no value-changed event are validated only by
		/// <see cref="Validate"/> / <see cref="Submit"/>.
		/// </summary>
		/// <remarks>
		/// Setting this to <c>true</c> subscribes all existing and future fields to their editor's native
		/// change event where one exists: <see cref="PromptControl.InputChanged"/>,
		/// <see cref="MultilineEditControl.ContentChanged"/>, <see cref="CheckboxControl.CheckedChanged"/>,
		/// <see cref="DropdownControl.SelectedValueChanged"/>, <see cref="SliderControl.ValueChanged"/>, and
		/// <see cref="RadioGroup{T}.SelectionChanged"/>.
		/// </remarks>
		public bool ValidateOnChange
		{
			get => _validateOnChange;
			set
			{
				if (SetProperty(ref _validateOnChange, value) && value)
				{
					foreach (var f in _fields)
						SubscribeFieldForLiveValidation(f);
				}
			}
		}

		/// <summary>
		/// Validates every field: a required field with an empty/null value fails with <c>"Required"</c>;
		/// otherwise the field's custom validator (if any) runs on its current value. Each field's error line
		/// is updated (shown with the message, or hidden when valid). Idempotent.
		/// </summary>
		/// <returns><c>true</c> when every field is valid; otherwise <c>false</c>.</returns>
		public bool Validate()
		{
			bool allValid = true;
			foreach (var field in _fields)
			{
				if (!ValidateField(field))
					allValid = false;
			}
			return allValid;
		}

		/// <summary>
		/// Validates the form and, if valid, raises <see cref="Submitted"/> with the current values snapshot.
		/// </summary>
		public void Submit()
		{
			if (Validate())
				Submitted?.Invoke(this, GetValues());
		}

		/// <summary>Raises the <see cref="Cancelled"/> event.</summary>
		public void Cancel() => Cancelled?.Invoke(this, EventArgs.Empty);

		/// <summary>Computes a field's error message, or <c>null</c> when the field is valid.</summary>
		private static string? ComputeError(FormField field)
		{
			var value = field.Getter();
			if (field.Required && string.IsNullOrEmpty(value))
				return "Required";
			return field.Validate?.Invoke(value);
		}

		/// <summary>Validates one field and updates its error line. Returns <c>true</c> when the field is valid.</summary>
		private bool ValidateField(FormField field)
		{
			string? error = ComputeError(field);
			if (field.ErrorView is { } view)
			{
				view.SetContent(new List<string> { error is null ? string.Empty : MarkupParser.Escape(error) });
				// Only surface the error row when the field itself is shown; a collapsed section hides
				// its label (via SetFieldVisible), so a validation error there must stay hidden too
				// rather than floating over the collapsed header.
				view.Visible = error != null && field.Label.Visible;
			}
			return error == null;
		}

		/// <summary>
		/// Subscribes a field's editor to its native value-changed event (where one exists) so the field
		/// re-validates live. Editors without such an event are skipped (validated only on demand).
		/// </summary>
		private void SubscribeFieldForLiveValidation(FormField field)
		{
			// Generic editors (radio groups) bridge their typed event via a hook captured at add time.
			if (field.LiveSubscribe is { } subscribe)
			{
				subscribe(() => ValidateField(field));
				return;
			}

			switch (field.ValueEditor)
			{
				case PromptControl prompt:
					prompt.InputChanged += (_, _) => ValidateField(field);
					break;
				case MultilineEditControl mle:
					mle.ContentChanged += (_, _) => ValidateField(field);
					break;
				case CheckboxControl checkbox:
					checkbox.CheckedChanged += (_, _) => ValidateField(field);
					break;
				case DropdownControl dropdown:
					dropdown.SelectedValueChanged += (_, _) => ValidateField(field);
					break;
				case SliderControl slider:
					slider.ValueChanged += (_, _) => ValidateField(field);
					break;
					// Other editors expose no value-changed event: no live validation.
			}
		}

		/// <summary>Returns whether the given field's error line is currently visible (test seam).</summary>
		internal bool HasErrorForTest(string name)
			=> _fieldsByName[name].ErrorView?.Visible ?? false;

		/// <summary>Returns the given field's column-0 label control (test seam).</summary>
		internal MarkupControl GetLabelForTest(string name) => _fieldsByName[name].Label;

		/// <summary>Returns the given field's current error text, or <c>null</c> when hidden (test seam).</summary>
		internal string? ErrorTextForTest(string name)
		{
			var view = _fieldsByName[name].ErrorView;
			if (view is null || !view.Visible)
				return null;
			return view.Text;
		}

		/// <summary>Invokes the named section's toggle, as if its button were clicked (test seam).</summary>
		internal void ToggleSectionForTest(string title)
		{
			var section = _sections.Find(s => s.Title == title)
				?? throw new KeyNotFoundException($"No section titled '{title}'.");
			ToggleSection(section);
		}

		/// <summary>Returns the given field's hint text, or <c>null</c> when the field has no hint (test seam).</summary>
		internal string? HintTextForTest(string name) => _fieldsByName[name].HintView?.Text;

		/// <summary>Returns whether the given field's hint line is currently visible (test seam).</summary>
		internal bool HintVisibleForTest(string name) => _fieldsByName[name].HintView?.Visible ?? false;

		/// <summary>Returns the number of packed multi-field row-groups added via <see cref="AddRow"/> (test seam).</summary>
		internal int RowGroupCountForTest() => _rowGroups.Count;

		/// <summary>Invokes the OK button's <see cref="ButtonControl.Click"/>, as if it were clicked (test seam).</summary>
		internal void ClickOkForTest()
		{
			if (_okButton is null)
				throw new InvalidOperationException("No OK button was added; call WithButtons() first.");
			_okButton.PerformClickForTest();
		}
	}
}
