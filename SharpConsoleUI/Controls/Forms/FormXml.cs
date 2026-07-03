// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Xml;
using System.Xml.Linq;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls.Forms
{
	/// <summary>
	/// Builds a <see cref="FormControl"/> from a declarative XML document. This is a thin call-through:
	/// it parses the XML with the BCL <see cref="XDocument"/> and drives the existing
	/// <see cref="FormControl"/> API — it invents no layout, validation, or submit logic of its own.
	/// </summary>
	/// <remarks>
	/// AOT-safe: uses only <see cref="System.Xml.Linq"/> (no reflection, no dynamic code, no extra
	/// dependency). All errors — malformed XML, a non-<c>form</c> root, an unknown element, or a missing
	/// required attribute — surface as a <see cref="FormXmlException"/> carrying line/position context.
	/// </remarks>
	public static class FormXml
	{
		/// <summary>
		/// Parses <paramref name="xml"/> and builds a <see cref="FormControl"/> by calling its field-add API.
		/// </summary>
		/// <param name="xml">The form XML. The root element must be <c>&lt;form&gt;</c>.</param>
		/// <param name="namedValidators">
		/// Optional registry of named validators referenced by <c>rule=</c> attributes. Each entry maps a name to a
		/// <c>Func&lt;string?, string?&gt;</c> that returns <c>null</c> when the value is valid, or an error message
		/// otherwise. A <c>rule=</c> whose name is absent from the registry throws a <see cref="FormXmlException"/>.
		/// </param>
		/// <returns>The built <see cref="FormControl"/>.</returns>
		/// <exception cref="FormXmlException">
		/// Thrown when the XML is malformed, the root is not <c>form</c>, an element is unknown, or a required
		/// attribute is missing.
		/// </exception>
		public static FormControl FromXml(
			string xml,
			IReadOnlyDictionary<string, Func<string?, string?>>? namedValidators = null)
		{
			XDocument doc;
			try
			{
				doc = XDocument.Parse(xml, LoadOptions.SetLineInfo);
			}
			catch (XmlException ex)
			{
				throw new FormXmlException($"Malformed form XML: {ex.Message}", ex);
			}

			var root = doc.Root;
			if (root?.Name.LocalName != "form")
			{
				throw new FormXmlException(
					$"Root element must be <form>, but was <{root?.Name.LocalName ?? "(none)"}>{At(root)}.");
			}

			var form = new FormControl();

			if (TryIntAttr(root, "columnGap", out int columnGap))
				form.ColumnGap = columnGap;
			if (TryIntAttr(root, "rowGap", out int rowGap))
				form.RowGap = rowGap;

			WalkChildren(form, root, namedValidators);
			return form;
		}

		/// <summary>
		/// Reads the form XML from a file and builds a <see cref="FormControl"/> via <see cref="FromXml"/>.
		/// </summary>
		/// <param name="path">Path to the XML file whose root element must be <c>&lt;form&gt;</c>.</param>
		/// <param name="namedValidators">
		/// Optional registry of named validators referenced by <c>rule=</c> attributes.
		/// </param>
		/// <returns>The built <see cref="FormControl"/>.</returns>
		/// <exception cref="FormXmlException">
		/// Thrown when the file cannot be read, the XML is malformed, or the document is otherwise invalid
		/// (see <see cref="FromXml"/>).
		/// </exception>
		public static FormControl FromXmlFile(
			string path,
			IReadOnlyDictionary<string, Func<string?, string?>>? namedValidators = null)
		{
			string xml;
			try
			{
				xml = File.ReadAllText(path);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
			{
				throw new FormXmlException($"Cannot read form file '{path}': {ex.Message}", ex);
			}

			return FromXml(xml, namedValidators);
		}

		/// <summary>Dispatches each child element to the matching field-add helper.</summary>
		private static void WalkChildren(
			FormControl form,
			XElement parent,
			IReadOnlyDictionary<string, Func<string?, string?>>? namedValidators)
		{
			foreach (var el in parent.Elements())
			{
				switch (el.Name.LocalName)
				{
					case "section":
						form.AddSection(Attr(el, "title"), BoolAttr(el, "collapsible", false), BoolAttr(el, "collapsed", false));
						WalkChildren(form, el, namedValidators);
						form.AddSection(null);
						break;

					case "row":
						form.AddRow(el.Elements()
							.Select(child => (Action<FormControl>)(f => DispatchField(f, child, namedValidators)))
							.ToArray());
						break;

					case "buttons":
						form.WithButtons(Attr(el, "ok") ?? "OK", Attr(el, "cancel") ?? "Cancel", BoolAttr(el, "showCancel", true));
						break;

					default:
						DispatchField(form, el, namedValidators);
						break;
				}
			}
		}

		/// <summary>
		/// Dispatches a single field element (<c>text</c>/<c>checkbox</c>/<c>dropdown</c>/<c>radio</c>/<c>slider</c>/
		/// <c>multiline</c>) to the matching field-add helper. A truly-unknown element throws a
		/// <see cref="FormXmlException"/>. Shared by <see cref="WalkChildren"/> and the <c>&lt;row&gt;</c> handler
		/// so a row child reuses the exact same field dispatch.
		/// </summary>
		private static void DispatchField(
			FormControl form,
			XElement el,
			IReadOnlyDictionary<string, Func<string?, string?>>? namedValidators)
		{
			switch (el.Name.LocalName)
			{
				case "text":
					AddTextField(form, el, namedValidators);
					break;

				case "checkbox":
					AddCheckboxField(form, el);
					break;

				case "dropdown":
					AddDropdownField(form, el);
					break;

				case "radio":
					AddRadioField(form, el);
					break;

				case "slider":
					AddSliderField(form, el);
					break;

				case "multiline":
					AddMultilineField(form, el);
					break;

				default:
					throw new FormXmlException($"Unknown form element <{el.Name.LocalName}>{At(el)}.");
			}
		}

		/// <summary>Adds a single-line text field, wiring any declarative validation attributes.</summary>
		private static void AddTextField(
			FormControl form,
			XElement el,
			IReadOnlyDictionary<string, Func<string?, string?>>? registry)
		{
			var (validate, required) = BuildValidator(el, registry);
			form.AddText(
				RequiredAttr(el, "name"),
				Attr(el, "label") ?? "",
				Attr(el, "initial") ?? "",
				validate,
				required,
				Attr(el, "hint"),
				NullableIntAttr(el, "width"),
				NullableAlignAttr(el));
		}

		/// <summary>
		/// Builds a composed validator from an element's declarative validation attributes and returns it
		/// alongside the <c>required</c> flag. Each recognised attribute contributes one rule that returns
		/// <c>null</c> when the value is acceptable, or an error message otherwise. The <c>message</c>
		/// attribute, when present, overrides the default message text for every rule on the field.
		/// </summary>
		/// <param name="el">The field element carrying the validation attributes.</param>
		/// <param name="registry">
		/// Optional registry of named validators resolved by <c>rule=</c> attributes. A <c>rule</c> that
		/// cannot be resolved throws a <see cref="FormXmlException"/>.
		/// </param>
		/// <returns>
		/// A tuple of the composed validator (or <c>null</c> when no rules apply) and the required flag.
		/// </returns>
		/// <exception cref="FormXmlException">Thrown when a <c>rule=</c> name is not found in the registry.</exception>
		private static (Func<string?, string?>? validate, bool required) BuildValidator(
			XElement el,
			IReadOnlyDictionary<string, Func<string?, string?>>? registry)
		{
			bool required = BoolAttr(el, "required", false);
			var message = Attr(el, "message");
			var rules = new List<Func<string?, string?>>();

			var type = Attr(el, "type");
			if (type == "int")
			{
				rules.Add(v =>
					string.IsNullOrEmpty(v) || int.TryParse(v, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out _)
						? null
						: message ?? "must be a whole number");
			}
			else if (type == "number")
			{
				rules.Add(v =>
					string.IsNullOrEmpty(v) || double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _)
						? null
						: message ?? "must be a number");
			}

			var pattern = Attr(el, "pattern");
			if (pattern != null)
			{
				System.Text.RegularExpressions.Regex rx;
				try
				{
					rx = new System.Text.RegularExpressions.Regex(pattern,
						System.Text.RegularExpressions.RegexOptions.None,
						System.TimeSpan.FromSeconds(1));
				}
				catch (System.ArgumentException ex)
				{
					throw new FormXmlException($"<{el.Name.LocalName}> attribute 'pattern' is not a valid regular expression: {ex.Message}{At(el)}.", ex);
				}
				rules.Add(v =>
					string.IsNullOrEmpty(v) || rx.IsMatch(v)
						? null
						: message ?? "invalid format");
			}

			var minStr = Attr(el, "min");
			if (minStr != null && double.TryParse(minStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double min))
			{
				rules.Add(v =>
				{
					if (string.IsNullOrEmpty(v))
						return null;
					return double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double d) && d >= min
						? null
						: message ?? $"must be ≥ {minStr}";
				});
			}

			var maxStr = Attr(el, "max");
			if (maxStr != null && double.TryParse(maxStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double max))
			{
				rules.Add(v =>
				{
					if (string.IsNullOrEmpty(v))
						return null;
					return double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double d) && d <= max
						? null
						: message ?? $"must be ≤ {maxStr}";
				});
			}

			if (TryIntAttr(el, "minLength", out int minLength))
			{
				rules.Add(v =>
					string.IsNullOrEmpty(v) || (v?.Length ?? 0) >= minLength
						? null
						: message ?? $"must be at least {minLength} characters");
			}

			if (TryIntAttr(el, "maxLength", out int maxLength))
			{
				rules.Add(v =>
					string.IsNullOrEmpty(v) || (v?.Length ?? 0) <= maxLength
						? null
						: message ?? $"at most {maxLength} characters");
			}

			var ruleName = Attr(el, "rule");
			if (ruleName != null)
			{
				if (registry != null && registry.TryGetValue(ruleName, out var fn))
					rules.Add(fn);
				else
					throw new FormXmlException($"Unknown validator '{ruleName}'{At(el)}.");
			}

			if (rules.Count == 0)
				return (null, required);

			Func<string?, string?> validate = v =>
			{
				foreach (var r in rules)
				{
					var e = r(v);
					if (e != null)
						return e;
				}
				return null;
			};
			return (validate, required);
		}

		/// <summary>Adds a boolean checkbox field.</summary>
		private static void AddCheckboxField(FormControl form, XElement el)
		{
			form.AddCheckbox(
				RequiredAttr(el, "name"),
				Attr(el, "label") ?? "",
				BoolAttr(el, "initial", false),
				Attr(el, "hint"));
		}

		/// <summary>
		/// Adds a single-select dropdown from a comma-separated <c>options</c> list. The convenience
		/// <see cref="FormControl.AddDropdown(string, string, System.Collections.Generic.IEnumerable{string}, string?, string?, int?, SharpConsoleUI.Layout.HorizontalAlignment?)"/>
		/// overload has no <c>validate</c>/<c>required</c> parameter, so declarative validation attributes are
		/// not applied here — a dropdown always holds one of its options, so it is inherently "required".
		/// </summary>
		private static void AddDropdownField(FormControl form, XElement el)
		{
			var opts = (Attr(el, "options") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
			form.AddDropdown(
				RequiredAttr(el, "name"),
				Attr(el, "label") ?? "",
				opts,
				Attr(el, "initial"),
				Attr(el, "hint"),
				NullableIntAttr(el, "width"),
				NullableAlignAttr(el));
		}

		/// <summary>
		/// Adds a single-select radio group from a comma-separated <c>options</c> list and, when an
		/// <c>initial</c> attribute is present, selects it via the typed <see cref="RadioGroup{T}"/> editor.
		/// The <see cref="FormControl.AddRadio(string, string, string[])"/> overload has no
		/// <c>validate</c>/<c>required</c> parameter, so declarative validation attributes are not applied — a
		/// radio group always resolves to one of its options. The params-string overload also takes no
		/// <c>width</c>, so a <c>width</c> attribute is not honoured for <c>&lt;radio&gt;</c> (radios keep their
		/// natural, left-packed width).
		/// </summary>
		private static void AddRadioField(FormControl form, XElement el)
		{
			var name = RequiredAttr(el, "name");
			var opts = (Attr(el, "options") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
			form.AddRadio(name, Attr(el, "label") ?? "", opts);

			var initial = Attr(el, "initial");
			if (initial != null)
				((RadioGroup<string>)form.GetEditor(name)).SelectedValue = initial;
		}

		/// <summary>
		/// Adds a numeric slider. The <see cref="FormControl.AddSlider(string, string, double, double, double, string?, int?, SharpConsoleUI.Layout.HorizontalAlignment?)"/>
		/// overload has no <c>validate</c>/<c>required</c> parameter and the slider already clamps to its
		/// <c>min</c>/<c>max</c> range, so declarative validation attributes are not applied here.
		/// </summary>
		private static void AddSliderField(FormControl form, XElement el)
		{
			form.AddSlider(
				RequiredAttr(el, "name"),
				Attr(el, "label") ?? "",
				DoubleAttr(el, "min", 0),
				DoubleAttr(el, "max", 100),
				DoubleAttr(el, "initial", 0),
				Attr(el, "hint"),
				NullableIntAttr(el, "width"),
				NullableAlignAttr(el));
		}

		/// <summary>
		/// Adds a multi-line text field. The
		/// <see cref="FormControl.AddMultilineEdit(string, string, string, int, string?, int?, SharpConsoleUI.Layout.HorizontalAlignment?)"/> overload has no
		/// <c>validate</c>/<c>required</c> parameter, so multi-line fields are unvalidated in this version.
		/// </summary>
		private static void AddMultilineField(FormControl form, XElement el)
		{
			form.AddMultilineEdit(
				RequiredAttr(el, "name"),
				Attr(el, "label") ?? "",
				Attr(el, "initial") ?? "",
				IntAttr(el, "height", 3),
				Attr(el, "hint"),
				NullableIntAttr(el, "width"),
				NullableAlignAttr(el));
		}

		/// <summary>Returns the value of a required attribute, or throws with line context if it is missing.</summary>
		private static string RequiredAttr(XElement el, string name)
		{
			var value = Attr(el, name);
			if (value == null)
			{
				throw new FormXmlException(
					$"<{el.Name.LocalName}> is missing required attribute '{name}'{At(el)}.");
			}
			return value;
		}

		/// <summary>Returns an attribute's value, or <c>null</c> when absent.</summary>
		private static string? Attr(XElement el, string name) => el.Attribute(name)?.Value;

		/// <summary>Returns a boolean attribute's value, or <paramref name="defaultValue"/> when absent or unparseable.</summary>
		private static bool BoolAttr(XElement el, string name, bool defaultValue)
		{
			var value = Attr(el, name);
			if (value == null)
				return defaultValue;
			return bool.TryParse(value, out bool parsed) ? parsed : defaultValue;
		}

		/// <summary>
		/// Returns an integer attribute's value using the invariant culture, or
		/// <paramref name="defaultValue"/> when the attribute is absent. A present-but-unparseable value throws
		/// a <see cref="FormXmlException"/> with line context.
		/// </summary>
		private static int IntAttr(XElement el, string name, int defaultValue)
		{
			var raw = Attr(el, name);
			if (raw == null)
				return defaultValue;
			if (int.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int value))
				return value;
			throw new FormXmlException(
				$"<{el.Name.LocalName}> attribute '{name}' is not a valid integer: '{raw}'{At(el)}.");
		}

		/// <summary>
		/// Returns a floating-point attribute's value using the invariant culture, or
		/// <paramref name="defaultValue"/> when the attribute is absent. A present-but-unparseable value throws
		/// a <see cref="FormXmlException"/> with line context.
		/// </summary>
		private static double DoubleAttr(XElement el, string name, double defaultValue)
		{
			var raw = Attr(el, name);
			if (raw == null)
				return defaultValue;
			if (double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double value))
				return value;
			throw new FormXmlException(
				$"<{el.Name.LocalName}> attribute '{name}' is not a valid number: '{raw}'{At(el)}.");
		}

		/// <summary>
		/// Returns an optional integer attribute's value using the invariant culture: <c>null</c> when the
		/// attribute is absent, the parsed value when present and valid. A present-but-unparseable value throws
		/// a <see cref="FormXmlException"/> with line context (consistent with <see cref="IntAttr"/>).
		/// </summary>
		private static int? NullableIntAttr(XElement el, string name)
		{
			var raw = Attr(el, name);
			if (raw == null)
				return null;
			if (int.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int value))
				return value;
			throw new FormXmlException(
				$"<{el.Name.LocalName}> attribute '{name}' is not a valid integer: '{raw}'{At(el)}.");
		}

		/// <summary>
		/// Returns the optional <c>align</c> attribute parsed to a <see cref="HorizontalAlignment"/>: <c>null</c>
		/// when the attribute is absent, otherwise the value parsed case-insensitively from
		/// <c>left</c>/<c>center</c>/<c>right</c>/<c>stretch</c>. A present-but-invalid value throws a
		/// <see cref="FormXmlException"/> with line context (consistent with the other typed attribute readers).
		/// </summary>
		private static HorizontalAlignment? NullableAlignAttr(XElement el)
		{
			var raw = Attr(el, "align");
			if (raw == null)
				return null;
			return raw.Trim().ToLowerInvariant() switch
			{
				"left" => HorizontalAlignment.Left,
				"center" => HorizontalAlignment.Center,
				"right" => HorizontalAlignment.Right,
				"stretch" => HorizontalAlignment.Stretch,
				_ => throw new FormXmlException(
					$"<{el.Name.LocalName}> attribute 'align' is not a valid alignment (left|center|right|stretch): '{raw}'{At(el)}."),
			};
		}

		/// <summary>Reads an integer attribute if present; returns <c>false</c> when absent or unparseable.</summary>
		private static bool TryIntAttr(XElement el, string name, out int value)
		{
			var raw = Attr(el, name);
			if (raw != null && int.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out value))
				return true;
			value = 0;
			return false;
		}

		/// <summary>Formats " (line N, position M)" line context for an element, or empty when unavailable.</summary>
		private static string At(XElement? el)
		{
			if (el is IXmlLineInfo info && info.HasLineInfo())
				return $" (line {info.LineNumber}, position {info.LinePosition})";
			return string.Empty;
		}
	}
}
