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
using SharpConsoleUI.Layout;
using Size = System.Drawing.Size;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Orientation for slider controls.
	/// </summary>
	public enum SliderOrientation
	{
		/// <summary>
		/// Horizontal slider (left-to-right).
		/// </summary>
		Horizontal,

		/// <summary>
		/// Vertical slider (bottom-to-top).
		/// </summary>
		Vertical
	}

	/// <summary>
	/// A slider control that allows users to select a value from a range by dragging a thumb
	/// along a track. Supports both horizontal and vertical orientations, keyboard and mouse
	/// interaction, and optional value and min/max labels.
	/// </summary>
	public partial class SliderControl : BaseControl, IInteractiveControl, IFocusableControl, IMouseAwareControl
	{
		#region Fields

		private double _value = ControlDefaults.SliderDefaultMinValue;
		private double _minValue = ControlDefaults.SliderDefaultMinValue;
		private double _maxValue = ControlDefaults.SliderDefaultMaxValue;
		private double _step = ControlDefaults.SliderDefaultStep;
		private double _largeStep = ControlDefaults.SliderDefaultLargeStep;
		private SliderOrientation _orientation = SliderOrientation.Horizontal;
		private bool _hasFocus;
		private bool _isEnabled = true;
		private bool _isDragging;
		private bool _isMouseDragging;
		private bool _showValueLabel;
		private bool _showMinMaxLabels;
		private string _valueLabelFormat = ControlDefaults.SliderDefaultValueFormat;
		private Color? _trackColor;
		private Color? _filledTrackColor;
		private Color? _thumbColor;
		private Color? _focusedThumbColor;
		private Color? _backgroundColorValue;
		private IContainer? _container;
		private LayoutRect _lastLayoutBounds;

		#endregion

		#region Constructor

		/// <summary>
		/// Initializes a new instance of the <see cref="SliderControl"/> class.
		/// </summary>
		public SliderControl()
		{
			HorizontalAlignment = HorizontalAlignment.Stretch;
		}

		#endregion

		#region Events

		/// <summary>
		/// Occurs when the slider value changes.
		/// </summary>
		public event EventHandler<double>? ValueChanged;

		/// <inheritdoc/>
		public event EventHandler? GotFocus;

		/// <inheritdoc/>
		public event EventHandler? LostFocus;

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
		/// Gets or sets the current slider value. Clamped to [MinValue, MaxValue] and snapped to Step.
		/// </summary>
		public double Value
		{
			get => _value;
			set
			{
				double snapped = SliderRenderingHelper.SnapToStep(value, _minValue, _maxValue, _step);
				if (Math.Abs(_value - snapped) < ControlDefaults.SliderMinStep / 2)
					return;

				_value = snapped;
				OnPropertyChanged();
				Container?.Invalidate(true);
				ValueChanged?.Invoke(this, _value);
			}
		}

		/// <summary>
		/// Gets or sets the minimum value of the slider range.
		/// </summary>
		public double MinValue
		{
			get => _minValue;
			set
			{
				if (value >= _maxValue) return;
				_minValue = value;
				OnPropertyChanged();
				if (_value < _minValue)
					Value = _minValue;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the maximum value of the slider range.
		/// </summary>
		public double MaxValue
		{
			get => _maxValue;
			set
			{
				if (value <= _minValue) return;
				_maxValue = value;
				OnPropertyChanged();
				if (_value > _maxValue)
					Value = _maxValue;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the step increment. Must be at least <see cref="ControlDefaults.SliderMinStep"/>.
		/// </summary>
		public double Step
		{
			get => _step;
			set => SetProperty(ref _step, Math.Max(ControlDefaults.SliderMinStep, value));
		}

		/// <summary>
		/// Gets or sets the large step increment used for Page Up/Down and Shift+Arrow.
		/// </summary>
		public double LargeStep
		{
			get => _largeStep;
			set => SetProperty(ref _largeStep, Math.Max(ControlDefaults.SliderMinStep, value));
		}

		#endregion

		#region Display Properties

		/// <summary>
		/// Gets or sets the slider orientation.
		/// </summary>
		public SliderOrientation Orientation
		{
			get => _orientation;
			set => SetProperty(ref _orientation, value);
		}

		/// <summary>
		/// Gets or sets whether to show the current value label next to the slider.
		/// </summary>
		public bool ShowValueLabel
		{
			get => _showValueLabel;
			set => SetProperty(ref _showValueLabel, value);
		}

		/// <summary>
		/// Gets or sets whether to show the min and max value labels at the track ends.
		/// </summary>
		public bool ShowMinMaxLabels
		{
			get => _showMinMaxLabels;
			set => SetProperty(ref _showMinMaxLabels, value);
		}

		/// <summary>
		/// Gets or sets the format string for the value label (default: "F0").
		/// </summary>
		public string ValueLabelFormat
		{
			get => _valueLabelFormat;
			set => SetProperty(ref _valueLabelFormat, value ?? ControlDefaults.SliderDefaultValueFormat);
		}

		#endregion

		#region Color Properties

		/// <summary>
		/// Gets or sets the color of the unfilled track portion.
		/// </summary>
		public Color? TrackColor
		{
			get => _trackColor ?? Container?.GetConsoleWindowSystem?.Theme?.SliderTrackColor ?? Color.Grey35;
			set => SetProperty(ref _trackColor, value);
		}

		/// <summary>
		/// Gets or sets the color of the filled track portion.
		/// </summary>
		public Color? FilledTrackColor
		{
			get => _filledTrackColor ?? Container?.GetConsoleWindowSystem?.Theme?.SliderFilledTrackColor ?? Color.Cyan1;
			set => SetProperty(ref _filledTrackColor, value);
		}

		/// <summary>
		/// Gets or sets the color of the thumb indicator.
		/// </summary>
		public Color? ThumbColor
		{
			get => _thumbColor ?? Container?.GetConsoleWindowSystem?.Theme?.SliderThumbColor ?? Color.White;
			set => SetProperty(ref _thumbColor, value);
		}

		/// <summary>
		/// Gets or sets the color of the thumb indicator when focused.
		/// </summary>
		public Color? FocusedThumbColor
		{
			get => _focusedThumbColor ?? Container?.GetConsoleWindowSystem?.Theme?.SliderFocusedThumbColor ?? Color.Yellow;
			set => SetProperty(ref _focusedThumbColor, value);
		}

		/// <summary>
		/// Gets or sets the background color.
		/// </summary>
		public Color? BackgroundColor
		{
			get => _backgroundColorValue;
			set => SetProperty(ref _backgroundColorValue, value);
		}

		#endregion

		#region Interface Properties

		/// <inheritdoc/>
		public override int? ContentWidth => Width;

		/// <inheritdoc/>
		public override IContainer? Container
		{
			get => _container;
			set
			{
				_container = value;
				OnPropertyChanged();
				_container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public bool HasFocus
		{
			get => _hasFocus;
			set
			{
				if (_hasFocus == value) return;
				_hasFocus = value;
				OnPropertyChanged();
				Container?.Invalidate(true);

				if (value)
					GotFocus?.Invoke(this, EventArgs.Empty);
				else
				{
					_isDragging = false;
					_isMouseDragging = false;
					LostFocus?.Invoke(this, EventArgs.Empty);
				}
			}
		}

		/// <inheritdoc/>
		public bool CanReceiveFocus => _isEnabled;

		/// <summary>
		/// Gets or sets whether the slider is enabled and can be interacted with.
		/// </summary>
		public bool IsEnabled
		{
			get => _isEnabled;
			set => PropertySetterHelper.SetBoolProperty(ref _isEnabled, value, Container);
		}

		/// <inheritdoc/>
		public bool WantsMouseEvents => _isEnabled;

		/// <inheritdoc/>
		public bool CanFocusWithMouse => _isEnabled;

		/// <summary>
		/// Gets whether the slider is currently being dragged.
		/// </summary>
		public bool IsDragging => _isDragging;

		#endregion

		#region BaseControl Overrides

		/// <inheritdoc/>
		public override Size GetLogicalContentSize()
		{
			if (_orientation == SliderOrientation.Horizontal)
			{
				int contentWidth = CalculateMinTrackWidth() + Margin.Left + Margin.Right;
				return new Size(contentWidth, 1 + Margin.Top + Margin.Bottom);
			}
			else
			{
				int contentHeight = ControlDefaults.DefaultVisibleItems + Margin.Top + Margin.Bottom;
				return new Size(1 + Margin.Left + Margin.Right, contentHeight);
			}
		}

		/// <inheritdoc/>
		protected override void OnDisposing()
		{
		}

		/// <inheritdoc/>
		public void SetFocus(bool focus, FocusReason reason = FocusReason.Programmatic)
		{
			bool hadFocus = HasFocus;
			HasFocus = focus;

			if (hadFocus != focus)
			{
				this.NotifyParentWindowOfFocusChange(focus);
			}
		}

		#endregion

		#region Private Helpers

		private int CalculateMinTrackWidth()
		{
			int width = ControlDefaults.SliderMinTrackLength;
			if (_showMinMaxLabels)
			{
				width += FormatValue(_minValue).Length + FormatValue(_maxValue).Length +
					ControlDefaults.SliderLabelSpacing * 2;
			}
			if (_showValueLabel)
			{
				width += FormatValue(_maxValue).Length + ControlDefaults.SliderLabelSpacing;
			}
			return width;
		}

		internal string FormatValue(double val)
		{
			return val.ToString(_valueLabelFormat);
		}

		#endregion
	}
}
