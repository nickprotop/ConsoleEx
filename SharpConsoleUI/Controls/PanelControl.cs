// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;
using SharpConsoleUI.Themes;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A control that renders a bordered panel with content.
	/// Implemented as a thin wrapper over an internal non-collapsible
	/// <see cref="CollapsiblePanel"/> which owns the border/header chrome and body layout.
	/// </summary>
	public class PanelControl : BaseControl, IMouseAwareControl, IColorRoleableControl, IContainer
	{

		#region ColorRole

		private ColorRole _role = ColorRole.Default;
		private ThemeMode? _colorRoleMode;
		private bool _outline;

		/// <inheritdoc/>
		public ColorRole ColorRole
		{
			get => _role;
			set { _role = value; _inner.ColorRole = value; Container?.Invalidate(true); }
		}

		/// <inheritdoc/>
		public ThemeMode? ColorRoleMode
		{
			get => _colorRoleMode;
			set { _colorRoleMode = value; _inner.ColorRoleMode = value; Container?.Invalidate(true); }
		}

		/// <inheritdoc/>
		public bool Outline
		{
			get => _outline;
			set { _outline = value; _inner.Outline = value; Container?.Invalidate(true); }
		}

		#endregion

		private Color? _backgroundColorValue;
		private Color? _foregroundColorValue;
		private int? _height;

		// Panel-specific properties
		private string? _content;
		private BorderStyle _borderStyle = BorderStyle.Single;
		private Color? _borderColorValue;
		private string? _header;
		private TextJustification _headerAlignment = TextJustification.Left;
		private Padding _padding = new Padding(1, 0, 1, 0);
		private bool _useSafeBorder = false;
		private bool _wordWrap = true;
		private readonly CollapsiblePanel _inner = new CollapsiblePanel { Collapsible = false, ShowHeader = false };
		internal CollapsiblePanel Inner => _inner;

		// Mouse interaction state
		private bool _wantsMouseEvents = true;
		private bool _canFocusWithMouse = false;

		/// <summary>
		/// Initializes a new instance of the <see cref="PanelControl"/> class.
		/// </summary>
		public PanelControl() { _inner.Container = this; WireInnerMouse(); SyncBorder(); }

		/// <summary>
		/// Initializes a new instance of the <see cref="PanelControl"/> class with text content.
		/// </summary>
		/// <param name="text">The text to display inside the panel (supports markup).</param>
		public PanelControl(string text) { _inner.Container = this; WireInnerMouse(); _content = text; SyncBorder(); }

		private void WireInnerMouse()
		{
			_inner.MouseClick += (_, e) => MouseClick?.Invoke(this, e);
			_inner.MouseDoubleClick += (_, e) => MouseDoubleClick?.Invoke(this, e);
			_inner.MouseRightClick += (_, e) => MouseRightClick?.Invoke(this, e);
			_inner.MouseEnter += (_, e) => MouseEnter?.Invoke(this, e);
			_inner.MouseLeave += (_, e) => MouseLeave?.Invoke(this, e);
			_inner.MouseMove += (_, e) => MouseMove?.Invoke(this, e);
		}

		#region Properties

		/// <inheritdoc/>
		public override int? ContentWidth => _inner.ContentWidth;

		/// <summary>
		/// Gets or sets the background color.
		/// When null, inherits from the container.
		/// </summary>
		public Color? BackgroundColor
		{
			get => _backgroundColorValue;
			set { _backgroundColorValue = value; _inner.BackgroundColor = value ?? Color.Transparent; Container?.Invalidate(true); }
		}

		/// <summary>
		/// Gets or sets the foreground color.
		/// When null, inherits from the container.
		/// </summary>
		public Color? ForegroundColor
		{
			get => _foregroundColorValue;
			set => SetProperty(ref _foregroundColorValue, value);
		}

		/// <summary>
		/// Gets or sets the explicit height of the panel.
		/// When set, the panel border will render at this height.
		/// When null and VerticalAlignment is Fill, the panel stretches to fill available height.
		/// </summary>
		public override int? Height
		{
			get => _height;
			set { _height = value.HasValue ? Math.Max(0, value.Value) : value; _inner.Height = _height; Container?.Invalidate(true); }
		}

		/// <inheritdoc/>
		public override VerticalAlignment VerticalAlignment
		{
			get => base.VerticalAlignment;
			set { base.VerticalAlignment = value; _inner.VerticalAlignment = value; Container?.Invalidate(true); }
		}

		/// <inheritdoc/>
		public override HorizontalAlignment HorizontalAlignment
		{
			get => base.HorizontalAlignment;
			set { base.HorizontalAlignment = value; _inner.HorizontalAlignment = value; Container?.Invalidate(true); }
		}

		#endregion

		#region Panel-specific Properties

		/// <summary>
		/// Gets or sets the content to display inside the panel (supports markup).
		/// </summary>
		public string? Content
		{
			get => _content;
			set => SetProperty(ref _content, value);
		}

		/// <summary>
		/// Gets or sets the border style for the panel.
		/// </summary>
		public BorderStyle BorderStyle
		{
			get => _borderStyle;
			set { _borderStyle = value; SyncBorder(); Container?.Invalidate(true); }
		}

		/// <summary>
		/// Gets or sets the border color.
		/// When null, uses the resolved foreground color.
		/// </summary>
		public Color? BorderColor
		{
			get => _borderColorValue;
			set { _borderColorValue = value; _inner.BorderColor = value; Container?.Invalidate(true); }
		}

		/// <summary>
		/// Gets or sets the header text displayed at the top of the panel border.
		/// </summary>
		public string? Header
		{
			get => _header;
			set { _header = value; SyncBorder(); Container?.Invalidate(true); }
		}

		/// <summary>
		/// Gets or sets the horizontal alignment of the header text.
		/// </summary>
		public TextJustification HeaderAlignment
		{
			get => _headerAlignment;
			set { _headerAlignment = value; _inner.HeaderAlignment = MapHeaderAlignment(value); Container?.Invalidate(true); }
		}

		/// <summary>
		/// Gets or sets the padding inside the panel border.
		/// </summary>
		public Padding Padding
		{
			get => _padding;
			set { _padding = value; _inner.Padding = value; Container?.Invalidate(true); }
		}

		/// <summary>
		/// Gets or sets whether to use safe border characters for better terminal compatibility.
		/// </summary>
		public bool UseSafeBorder
		{
			get => _useSafeBorder;
			set { _useSafeBorder = value; _inner.UseSafeBorder = value; Container?.Invalidate(true); }
		}

		/// <summary>
		/// Gets or sets whether content lines that exceed the panel width are word-wrapped.
		/// When true (default), long lines are broken at word boundaries.
		/// When false, long lines are clipped at the panel boundary — use this for
		/// pre-formatted content such as graphs and progress bars.
		/// </summary>
		public bool WordWrap
		{
			get => _wordWrap;
			set => SetProperty(ref _wordWrap, value);
		}

		#endregion

		#region IMouseAwareControl Properties

		/// <inheritdoc/>
		public bool WantsMouseEvents
		{
			get => _wantsMouseEvents;
			set
			{
				if (_wantsMouseEvents == value) return;
				_wantsMouseEvents = value;
			}
		}

		/// <inheritdoc/>
		public bool CanFocusWithMouse
		{
			get => _canFocusWithMouse;
			set
			{
				if (_canFocusWithMouse == value) return;
				_canFocusWithMouse = value;
			}
		}

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseClick;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseDoubleClick;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseRightClick;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseEnter;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseLeave;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseMove;

		#endregion

		#region IContainer

		// These provide the inner panel's container colors. They must resolve from PanelControl's OWN
		// state and outer Container — delegating to _inner would recurse (the inner panel's Container is
		// this PanelControl, so it would ask us back).
		Color IContainer.BackgroundColor
		{
			get => ColorResolver.ResolveBackground(_backgroundColorValue, Container);
			set { _backgroundColorValue = value; Container?.Invalidate(true); }
		}
		Color IContainer.ForegroundColor
		{
			get => ColorResolver.ResolveForeground(_foregroundColorValue, Container);
			set { _foregroundColorValue = value; Container?.Invalidate(true); }
		}
		ConsoleWindowSystem? IContainer.GetConsoleWindowSystem => Container?.GetConsoleWindowSystem;
		bool IContainer.IsDirty { get => ((IContainer)_inner).IsDirty; set => ((IContainer)_inner).IsDirty = value; }
		void IContainer.Invalidate(bool redrawAll, IWindowControl? callerControl) => Container?.Invalidate(redrawAll, callerControl);
		int? IContainer.GetVisibleHeightForControl(IWindowControl control) => Container?.GetVisibleHeightForControl(control);

		#endregion

		#region Private Rendering Methods

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
			_inner.HeaderStyle = MapBorderStyle(_borderStyle);
			_inner.ShowHeader = !string.IsNullOrEmpty(_header);
			_inner.Title = _header;
			_inner.HeaderAlignment = MapHeaderAlignment(_headerAlignment);
		}

		#endregion

		#region IMouseAwareControl Implementation

		/// <inheritdoc/>
		public bool ProcessMouseEvent(MouseEventArgs args)
		{
			if (!WantsMouseEvents)
				return false;

			return _inner.ProcessMouseEvent(args);
		}

		#endregion

		#region BaseControl Overrides

		/// <summary>
		/// Called during Dispose before Container is set to null.
		/// Clears mouse event handlers and disposes the inner panel.
		/// </summary>
		protected override void OnDisposing()
		{
			MouseClick = null;
			MouseDoubleClick = null;
			MouseRightClick = null;
			MouseEnter = null;
			MouseLeave = null;
			MouseMove = null;
			_inner.Dispose();
		}

		/// <summary>
		/// Creates a new builder for configuring a PanelControl
		/// </summary>
		/// <returns>A new builder instance</returns>
		public static Builders.PanelBuilder Create()
		{
			return new Builders.PanelBuilder();
		}

		/// <inheritdoc/>
		public override System.Drawing.Size GetLogicalContentSize() => _inner.GetLogicalContentSize();

		/// <summary>
		/// Sets the content to display inside the panel using text (supports markup).
		/// </summary>
		/// <param name="text">The text to display.</param>
		public void SetContent(string text)
		{
			Content = text;
		}

		#endregion

		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public override LayoutSize MeasureDOM(LayoutConstraints constraints) => _inner.MeasureDOM(constraints);

		/// <inheritdoc/>
		public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			SetActualBounds(bounds);
		}

		#endregion
	}
}
