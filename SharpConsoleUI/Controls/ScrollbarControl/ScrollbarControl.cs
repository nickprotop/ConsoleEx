// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Helpers.Scrollbar;
using SharpConsoleUI.Layout;
using Size = System.Drawing.Size;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Orientation for a standalone <see cref="ScrollbarControl"/>.
	/// </summary>
	public enum ScrollbarOrientation
	{
		/// <summary>Runs top to bottom (the default).</summary>
		Vertical,

		/// <summary>Runs left to right.</summary>
		Horizontal
	}

	/// <summary>
	/// The sub-region of a <see cref="ScrollbarControl"/> a mouse gesture belongs to. <see cref="None"/>
	/// is deliberately the first (default) member so an uncaptured <see cref="GestureRoute{TRegion}"/>
	/// (which carries <c>default(TRegion)</c>) never resolves to a real region.
	/// </summary>
	internal enum ScrollbarGestureRegion
	{
		/// <summary>No gesture captured.</summary>
		None,

		/// <summary>The bar's track (arrows, paging and the thumb).</summary>
		Track
	}

	/// <summary>
	/// A standalone scrollbar control, decoupled from any single scrollable view. One
	/// <see cref="ScrollbarControl"/> can drive several views at once — the case that motivated it
	/// (issue #85) is a side-by-side diff viewer where one bar must scroll two panes together.
	/// </summary>
	/// <remarks>
	/// Built on the shared engine in <see cref="SharpConsoleUI.Helpers.Scrollbar"/>
	/// (<see cref="ScrollbarMetrics"/>, <see cref="ScrollbarGeometry"/>, <see cref="ScrollbarInput"/>,
	/// <see cref="ScrollbarRenderer"/>, <see cref="ScrollbarPaletteResolver"/>) that every built-in
	/// scrolling control now shares, so this bar behaves identically to the one embedded in, say,
	/// <see cref="ScrollablePanelControl"/>.
	///
	/// The two events are the point of the control: <see cref="ValueChanged"/> fires on every change,
	/// including a code-set <see cref="Value"/> (conventional, INPC-consistent); <see cref="UserValueChanged"/>
	/// fires only for a change the user made through the bar (arrow, track page, thumb drag, wheel). A
	/// composite wires <see cref="UserValueChanged"/> to push the new offset to its views, and the views'
	/// own scroll events back to <see cref="Value"/> — with no echo and no guard flag needed, because a
	/// code-set <see cref="Value"/> never re-raises <see cref="UserValueChanged"/>.
	/// </remarks>
	public partial class ScrollbarControl : BaseControl, IWindowControl, IMouseAwareControl
	{
		#region Fields

		private ScrollbarOrientation _orientation = ScrollbarOrientation.Vertical;
		private int _maximum;
		private int _viewportLength = 1;
		private int _value;
		private int _smallChange = ControlDefaults.DefaultScrollWheelLines;
		private int? _largeChange;
		private bool _isActive;
		private bool _isEnabled = true;
		private Color? _scrollbarColor;
		private Color? _scrollbarThumbColor;

		private readonly MouseGestureCapture<ScrollbarGestureRegion> _gesture = new();
		private bool _thumbDragging;
		private int _dragStartPointerPos;
		private int _dragStartThumbPos;

		#endregion

		#region Constructor

		/// <summary>
		/// Initializes a new instance of the <see cref="ScrollbarControl"/> class.
		/// </summary>
		public ScrollbarControl()
		{
			HorizontalAlignment = HorizontalAlignment.Stretch;
			VerticalAlignment = VerticalAlignment.Fill;
		}

		#endregion

		#region Events

		/// <summary>
		/// Occurs whenever <see cref="Value"/> changes, however it changed — including a value set by
		/// code. Matches the conventional, INPC-consistent behaviour of <see cref="SliderControl.ValueChanged"/>.
		/// </summary>
		public event EventHandler<int>? ValueChanged;

		/// <summary>
		/// Occurs only when the user changes <see cref="Value"/> through the bar itself: an arrow click,
		/// a track page click, a thumb drag, or a mouse wheel notch. Never raised by a code-set
		/// <see cref="Value"/>. A composite subscribes to this (not <see cref="ValueChanged"/>) to push
		/// the new offset to the views it drives, then writes the same value back to <see cref="Value"/>
		/// without causing an echo, since that write raises only <see cref="ValueChanged"/>.
		/// </summary>
		public event EventHandler<int>? UserValueChanged;

#pragma warning disable CS0067
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
#pragma warning restore CS0067

		#endregion

		#region Value Properties

		/// <summary>
		/// Gets or sets the bar's orientation. Defaults to <see cref="ScrollbarOrientation.Vertical"/>.
		/// </summary>
		public ScrollbarOrientation Orientation
		{
			get => _orientation;
			set => SetProperty(ref _orientation, value);
		}

		/// <summary>
		/// Gets or sets the total length of the content being scrolled. Shrinking this re-clamps
		/// <see cref="Value"/> to the new range.
		/// </summary>
		public int Maximum
		{
			get => _maximum;
			set
			{
				int clamped = Math.Max(0, value);
				if (!SetProperty(ref _maximum, clamped)) return;
				ClampValue();
			}
		}

		/// <summary>
		/// Gets or sets the visible length of the view the bar represents. Shrinking or growing this
		/// re-clamps <see cref="Value"/> to the new range, and — when <see cref="LargeChange"/> was never
		/// set explicitly — changes the default paging amount to match.
		/// </summary>
		public int ViewportLength
		{
			get => _viewportLength;
			set
			{
				int clamped = Math.Max(1, value);
				if (!SetProperty(ref _viewportLength, clamped)) return;
				ClampValue();
			}
		}

		/// <summary>
		/// Gets or sets the current scroll offset, clamped to <c>[0, Maximum - ViewportLength]</c>. Every
		/// change — including this one, set by code — raises <see cref="ValueChanged"/>. Only a change
		/// made through the bar (arrow, track, drag, wheel) additionally raises <see cref="UserValueChanged"/>.
		/// </summary>
		public int Value
		{
			get => _value;
			set => SetValue(value, fromUser: false);
		}

		/// <summary>
		/// Gets or sets the amount <see cref="Value"/> moves for an arrow click or a mouse wheel notch.
		/// Defaults to <see cref="ControlDefaults.DefaultScrollWheelLines"/>.
		/// </summary>
		public int SmallChange
		{
			get => _smallChange;
			set => SetProperty(ref _smallChange, Math.Max(1, value));
		}

		/// <summary>
		/// Gets or sets the amount <see cref="Value"/> moves for a track (page) click. Defaults to
		/// <see cref="ViewportLength"/> when never set explicitly.
		/// </summary>
		public int LargeChange
		{
			get => _largeChange ?? _viewportLength;
			set => SetProperty(ref _largeChange, Math.Max(1, value));
		}

		#endregion

		#region Display Properties

		/// <summary>
		/// Gets or sets whether the bar paints itself as focused, even though it is never itself a Tab
		/// stop. A composite that drives several sibling views sets this to <c>true</c> while one of
		/// those views actually holds focus, so the bar's colour still reflects "the group is active."
		/// </summary>
		public bool IsActive
		{
			get => _isActive;
			set => SetProperty(ref _isActive, value);
		}

		/// <summary>
		/// Gets or sets whether the bar is enabled. A disabled bar dims to its unfocused colours and
		/// ignores mouse input.
		/// </summary>
		public bool IsEnabled
		{
			get => _isEnabled;
			set => PropertySetterHelper.SetBoolProperty(ref _isEnabled, value, Container);
		}

		/// <summary>
		/// Gets or sets the track colour. When <c>null</c> (the default), the track uses the shared
		/// scrollbar engine's focus-aware theme colour.
		/// </summary>
		public Color? ScrollbarColor
		{
			get => _scrollbarColor;
			set => SetProperty(ref _scrollbarColor, value);
		}

		/// <summary>
		/// Gets or sets the thumb colour. When <c>null</c> (the default), the thumb uses the shared
		/// scrollbar engine's focus-aware theme colour.
		/// </summary>
		public Color? ScrollbarThumbColor
		{
			get => _scrollbarThumbColor;
			set => SetProperty(ref _scrollbarThumbColor, value);
		}

		#endregion

		#region Interface Properties

		/// <inheritdoc/>
		public override int? ContentWidth => Width;

		/// <inheritdoc/>
		public override IContainer? Container
		{
			get => base.Container;
			set
			{
				base.Container = value;
				OnPropertyChanged();
				Invalidate(Invalidation.Relayout);
			}
		}

		/// <summary>
		/// A scrollbar is a bar, not a tab stop: it never takes keyboard focus.
		/// </summary>
		public bool CanReceiveFocus => false;

		/// <inheritdoc/>
		public bool WantsMouseEvents => _isEnabled;

		/// <summary>
		/// A scrollbar cannot be focused by a mouse click either — only dragged/clicked for its own
		/// scrolling behaviour.
		/// </summary>
		public bool CanFocusWithMouse => false;

		#endregion

		#region BaseControl Overrides

		/// <inheritdoc/>
		public override Size GetLogicalContentSize()
		{
			return _orientation == ScrollbarOrientation.Vertical
				? new Size(1 + Margin.Left + Margin.Right, ControlDefaults.DefaultVisibleItems + Margin.Top + Margin.Bottom)
				: new Size(ControlDefaults.SliderMinTrackLength + Margin.Left + Margin.Right, 1 + Margin.Top + Margin.Bottom);
		}

		/// <inheritdoc/>
		protected override void OnDisposing()
		{
		}

		#endregion

		#region Private Helpers

		/// <summary>
		/// The single choke-point for changing <see cref="_value"/>: clamps, guards against no-change,
		/// notifies, invalidates, and raises <see cref="ValueChanged"/> — and, only when
		/// <paramref name="fromUser"/> is <c>true</c>, <see cref="UserValueChanged"/> as well. Every
		/// input path (arrow, track, drag, wheel) calls this with <c>fromUser: true</c>; the
		/// <see cref="Value"/> setter is the only caller that passes <c>false</c>.
		/// </summary>
		private void SetValue(int value, bool fromUser)
		{
			int clamped = Math.Clamp(value, 0, MaxValue);
			if (clamped == _value) return;

			_value = clamped;
			OnPropertyChanged(nameof(Value));
			Invalidate(Invalidation.Repaint);

			ValueChanged?.Invoke(this, _value);
			if (fromUser)
				UserValueChanged?.Invoke(this, _value);
		}

		/// <summary>
		/// The user-facing entry point every mouse/wheel input path calls. Raises both events when the
		/// value actually changes.
		/// </summary>
		private void SetValueFromUser(int value) => SetValue(value, fromUser: true);

		/// <summary>The largest valid <see cref="Value"/> for the current range.</summary>
		private int MaxValue => Math.Max(0, _maximum - _viewportLength);

		/// <summary>Re-clamps <see cref="_value"/> after <see cref="Maximum"/> or <see cref="ViewportLength"/> shrinks.</summary>
		private void ClampValue() => SetValue(_value, fromUser: false);

		#endregion
	}
}
