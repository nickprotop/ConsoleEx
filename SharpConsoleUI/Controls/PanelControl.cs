// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A bordered panel that hosts child controls. Implemented as a permanently non-collapsible
	/// <see cref="CollapsiblePanel"/>: the collapse API is sealed off (the panel is always expanded).
	/// <see cref="Content"/>/<see cref="SetContent(string)"/> replace all body content with a single
	/// borderless <see cref="MarkupControl"/> child. Container operations (<c>AddControl</c>,
	/// <c>RemoveControl</c>, <c>ClearControls</c>, <c>Children</c>), the border/header chrome
	/// (<c>BorderColor</c>, <c>Padding</c>, <c>UseSafeBorder</c>), the colour role
	/// surface and the mouse events are all inherited from <see cref="CollapsiblePanel"/>.
	/// </summary>
	public class PanelControl : CollapsiblePanel
	{
		private MarkupControl? _contentChild;
		// Raw content string stored verbatim so the Content getter round-trips exactly,
		// independent of any MarkupControl.Text normalization.
		private string? _contentText;
		private bool _wordWrap = true;

		// Nullable color shadows: PanelControl's public BackgroundColor/ForegroundColor have a Color?
		// contract (callers do `panel.BackgroundColor ?? fallback`). CollapsiblePanel's are non-nullable
		// Color. We keep our own nullable copy and forward into the base.
		// TODO(nullable-colors): remove these shadows once CollapsiblePanel colors are Color?.
		private Color? _bgColor;
		private Color? _fgColor;

		// Panel border state. The control still exposes a BorderStyle (BorderStyle enum) surface for
		// backward compatibility; it maps onto the base HeaderStyle (CollapsibleHeaderStyle).
		private BorderStyle _borderStyle = BorderStyle.Single;
		private TextJustification _headerAlignment = TextJustification.Left;

		/// <summary>
		/// Initializes a new instance of the <see cref="PanelControl"/> class.
		/// </summary>
		public PanelControl()
		{
			// base.Collapsible setter forces the panel permanently expanded and clears the indicator.
			// We go through the base setter (not the sealed no-op override) so the state actually sticks.
			base.Collapsible = false;
			// A panel only shows a header when one is set (matches the old facade default).
			base.ShowHeader = false;
			SyncBorder();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="PanelControl"/> class with text content.
		/// </summary>
		/// <param name="text">The text to display inside the panel (supports markup).</param>
		public PanelControl(string text) : this()
		{
			Content = text;
		}

		#region Sealed collapse API — a Panel can never become collapsible

		/// <inheritdoc/>
		/// <remarks>Always <see langword="false"/> for a panel; the setter is a no-op.</remarks>
		public sealed override bool Collapsible
		{
			get => false;
			set { /* no-op: panels are non-collapsible */ }
		}

		/// <inheritdoc/>
		/// <remarks>Always <see langword="true"/> for a panel; the setter is a no-op.</remarks>
		public sealed override bool IsExpanded
		{
			get => true;
			set { /* no-op: panels are always expanded */ }
		}

		/// <inheritdoc/>
		/// <remarks>No-op: a panel cannot be toggled.</remarks>
		public sealed override void Toggle() { /* no-op */ }

		/// <inheritdoc/>
		/// <remarks>No-op: a panel is always expanded.</remarks>
		public sealed override void Expand() { /* no-op */ }

		/// <inheritdoc/>
		/// <remarks>No-op: a panel cannot be collapsed.</remarks>
		public sealed override void Collapse() { /* no-op */ }

		// ShowHeader / ShowHeaderSeparator are legitimate on a panel (they don't toggle collapse).
		// Seal them so they can't be re-virtualized, while still delegating to the base behavior.

		/// <inheritdoc/>
		public sealed override bool ShowHeader
		{
			get => base.ShowHeader;
			set => base.ShowHeader = value;
		}

		/// <inheritdoc/>
		public sealed override bool ShowHeaderSeparator
		{
			get => base.ShowHeaderSeparator;
			set => base.ShowHeaderSeparator = value;
		}

		#endregion

		#region Nullable color shadows (preserve the Color? Panel contract)

		/// <summary>
		/// Gets or sets the background color. When <see langword="null"/>, the panel inherits from its
		/// container. Exposes a <see cref="Color"/>? contract (callers may write
		/// <c>panel.BackgroundColor ?? fallback</c>).
		/// </summary>
		public new Color? BackgroundColor
		{
			get => _bgColor;
			// Push the RAW nullable through the internal hook so null truly clears the base's explicit
			// color (resolves from container/theme), instead of pinning it to a concrete value. The
			// public non-nullable base setter cannot express "no explicit color".
			set { _bgColor = value; base.SetBackgroundColorNullable(value); }
		}

		/// <summary>
		/// Gets or sets the foreground color. When <see langword="null"/>, the panel resolves the
		/// foreground from the theme/container. Exposes a <see cref="Color"/>? contract.
		/// </summary>
		public new Color? ForegroundColor
		{
			get => _fgColor;
			set
			{
				_fgColor = value;
				// Push the RAW nullable through the internal hook. A null reset must clear the base's
				// explicit color so CollapsiblePanel.PaintDOM (which reads _foregroundColorValue directly)
				// resolves from the theme — NOT retain the prior explicit color.
				base.SetForegroundColorNullable(value);
				if (_contentChild != null) _contentChild.ForegroundColor = value;
			}
		}

		#endregion

		#region Content — replace-all single borderless markup child

		/// <summary>
		/// Gets or sets the content to display inside the panel (supports markup). Setting this replaces
		/// ALL body content with a single borderless <see cref="MarkupControl"/> child. Setting it to
		/// <see langword="null"/> or empty removes that child.
		/// </summary>
		public string? Content
		{
			get => _contentText;
			set
			{
				ClearControls();        // replace ALL content (also disposes prior children)
				_contentChild = null;
				_contentText = null;
				if (!string.IsNullOrEmpty(value))
				{
					_contentText = value;   // store verbatim so the getter round-trips exactly
					_contentChild = new MarkupControl(new System.Collections.Generic.List<string> { value })
					{
						Wrap = _wordWrap,
						ForegroundColor = _fgColor
					};
					AddControl(_contentChild);
				}
			}
		}

		/// <summary>
		/// Sets the content to display inside the panel using text (supports markup). Replaces all
		/// existing body content.
		/// </summary>
		/// <param name="text">The text to display.</param>
		public void SetContent(string text) => Content = text;

		/// <summary>
		/// Gets or sets whether content lines that exceed the panel width are word-wrapped.
		/// When <see langword="true"/> (default), long lines are broken at word boundaries.
		/// When <see langword="false"/>, long lines are clipped — use this for pre-formatted content
		/// such as graphs and progress bars.
		/// </summary>
		public bool WordWrap
		{
			get => _wordWrap;
			set { _wordWrap = value; if (_contentChild != null) _contentChild.Wrap = value; }
		}

		#endregion

		#region Panel chrome — backward-compatible BorderStyle / Header / HeaderAlignment

		/// <summary>
		/// Gets or sets the border style for the panel. Maps onto the base
		/// <see cref="CollapsiblePanel.HeaderStyle"/>: <see cref="BorderStyle.Rounded"/> →
		/// <see cref="CollapsibleHeaderStyle.Rounded"/>, <see cref="BorderStyle.DoubleLine"/> →
		/// <see cref="CollapsibleHeaderStyle.DoubleLine"/>, <see cref="BorderStyle.None"/>/
		/// <see cref="BorderStyle.Frameless"/> → <see cref="CollapsibleHeaderStyle.Borderless"/>, and
		/// <see cref="BorderStyle.Single"/> → <see cref="CollapsibleHeaderStyle.Bordered"/>.
		/// </summary>
		public BorderStyle BorderStyle
		{
			get => _borderStyle;
			set { _borderStyle = value; SyncBorder(); }
		}

		/// <summary>
		/// Gets or sets the header text displayed at the top of the panel border. An alias over
		/// <see cref="CollapsiblePanel.Title"/> that also toggles header visibility.
		/// </summary>
		public string? Header
		{
			get => base.Title;
			set { base.Title = value; base.ShowHeader = !string.IsNullOrEmpty(value); }
		}

		/// <summary>
		/// Gets or sets the horizontal alignment of the header text, using
		/// <see cref="TextJustification"/>. Maps onto the base
		/// <see cref="CollapsiblePanel.HeaderAlignment"/> (<see cref="HorizontalAlignment"/>).
		/// </summary>
		public new TextJustification HeaderAlignment
		{
			get => _headerAlignment;
			set { _headerAlignment = value; base.HeaderAlignment = MapHeaderAlignment(value); }
		}

		private static CollapsibleHeaderStyle MapBorderStyle(BorderStyle style) => style switch
		{
			BorderStyle.Rounded => CollapsibleHeaderStyle.Rounded,
			BorderStyle.DoubleLine => CollapsibleHeaderStyle.DoubleLine,
			BorderStyle.None => CollapsibleHeaderStyle.Borderless,
			BorderStyle.Frameless => CollapsibleHeaderStyle.Borderless,
			_ => CollapsibleHeaderStyle.Bordered,
		};

		private static HorizontalAlignment MapHeaderAlignment(TextJustification j) => j switch
		{
			TextJustification.Center => HorizontalAlignment.Center,
			TextJustification.Right => HorizontalAlignment.Right,
			_ => HorizontalAlignment.Left,
		};

		private void SyncBorder()
		{
			// Header text/visibility is owned by the Header property (delegates straight to base.Title).
			// SyncBorder only maps the BorderStyle and header alignment onto the base.
			base.HeaderStyle = MapBorderStyle(_borderStyle);
			base.HeaderAlignment = MapHeaderAlignment(_headerAlignment);
		}

		#endregion

		/// <summary>
		/// Creates a new builder for configuring a <see cref="PanelControl"/>.
		/// </summary>
		/// <returns>A new builder instance.</returns>
		public static Builders.PanelBuilder Create()
		{
			return new Builders.PanelBuilder();
		}
	}
}
