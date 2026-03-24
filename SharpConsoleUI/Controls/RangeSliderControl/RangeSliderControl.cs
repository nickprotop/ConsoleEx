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
	/// Identifies which thumb is currently active on a RangeSliderControl.
	/// </summary>
	public enum ActiveThumb
	{
		/// <summary>The low-value thumb.</summary>
		Low,
		/// <summary>The high-value thumb.</summary>
		High
	}

	/// <summary>
	/// A dual-thumb range slider control that allows users to select a range of values
	/// by dragging two thumbs along a track. Supports keyboard and mouse interaction,
	/// minimum range enforcement, and optional value and min/max labels.
	/// </summary>
	public partial class RangeSliderControl : BaseControl, IInteractiveControl, IFocusableControl, IMouseAwareControl
	{
		#region Fields

		private double _lowValue;
		private double _highValue;
		private double _minValue = ControlDefaults.SliderDefaultMinValue;
		private double _maxValue = ControlDefaults.SliderDefaultMaxValue;
		private double _step = ControlDefaults.SliderDefaultStep;
		private double _largeStep = ControlDefaults.SliderDefaultLargeStep;
		private double _minRange = ControlDefaults.RangeSliderDefaultMinRange;
		private ActiveThumb _activeThumb = ActiveThumb.Low;
		private SliderOrientation _orientation = SliderOrientation.Horizontal;
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
		/// Initializes a new instance of the <see cref="RangeSliderControl"/> class
		/// with default low=0 and high=100.
		/// </summary>
		public RangeSliderControl()
		{
			HorizontalAlignment = HorizontalAlignment.Stretch;
			_lowValue = ControlDefaults.SliderDefaultMinValue;
			_highValue = ControlDefaults.SliderDefaultMaxValue;
		}

		#endregion

		#region Events

		/// <summary>
		/// Occurs when the low value changes.
		/// </summary>
		public event EventHandler<double>? LowValueChanged;

		/// <summary>
		/// Occurs when the high value changes.
		/// </summary>
		public event EventHandler<double>? HighValueChanged;

		/// <summary>
		/// Occurs when either value changes, providing both low and high.
		/// </summary>
		public event EventHandler<(double Low, double High)>? RangeChanged;

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
		/// Gets or sets the low value of the range. Enforces LowValue &lt;= HighValue - MinRange.
		/// Setting LowValue above HighValue - MinRange pushes HighValue up.
		/// </summary>
		public double LowValue
		{
			get => _lowValue;
			set
			{
				double snapped = SliderRenderingHelper.SnapToStep(value, _minValue, _maxValue, _step);
				snapped = Math.Clamp(snapped, _minValue, _maxValue - _minRange);

				if (Math.Abs(_lowValue - snapped) < ControlDefaults.SliderMinStep / 2)
					return;

				_lowValue = snapped;

				// Push high value if needed
				if (_highValue < _lowValue + _minRange)
				{
					_highValue = Math.Min(_lowValue + _minRange, _maxValue);
					HighValueChanged?.Invoke(this, _highValue);
				}

				OnPropertyChanged();
				Container?.Invalidate(true);
				LowValueChanged?.Invoke(this, _lowValue);
				RangeChanged?.Invoke(this, (_lowValue, _highValue));
			}
		}

		/// <summary>
		/// Gets or sets the high value of the range. Enforces HighValue &gt;= LowValue + MinRange.
		/// Setting HighValue below LowValue + MinRange pushes LowValue down.
		/// </summary>
		public double HighValue
		{
			get => _highValue;
			set
			{
				double snapped = SliderRenderingHelper.SnapToStep(value, _minValue, _maxValue, _step);
				snapped = Math.Clamp(snapped, _minValue + _minRange, _maxValue);

				if (Math.Abs(_highValue - snapped) < ControlDefaults.SliderMinStep / 2)
					return;

				_highValue = snapped;

				// Push low value if needed
				if (_lowValue > _highValue - _minRange)
				{
					_lowValue = Math.Max(_highValue - _minRange, _minValue);
					LowValueChanged?.Invoke(this, _lowValue);
				}

				OnPropertyChanged();
				Container?.Invalidate(true);
				HighValueChanged?.Invoke(this, _highValue);
				RangeChanged?.Invoke(this, (_lowValue, _highValue));
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
				if (_lowValue < _minValue) LowValue = _minValue;
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
				if (_highValue > _maxValue) HighValue = _maxValue;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the step increment.
		/// </summary>
		public double Step
		{
			get => _step;
			set => SetProperty(ref _step, Math.Max(ControlDefaults.SliderMinStep, value));
		}

		/// <summary>
		/// Gets or sets the large step increment.
		/// </summary>
		public double LargeStep
		{
			get => _largeStep;
			set => SetProperty(ref _largeStep, Math.Max(ControlDefaults.SliderMinStep, value));
		}

		/// <summary>
		/// Gets or sets the minimum required gap between the low and high values.
		/// </summary>
		public double MinRange
		{
			get => _minRange;
			set
			{
				_minRange = Math.Max(0, value);
				OnPropertyChanged();
				// Enforce constraint
				if (_highValue - _lowValue < _minRange)
				{
					HighValue = _lowValue + _minRange;
				}
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets which thumb is currently active (receives keyboard input).
		/// </summary>
		public ActiveThumb ActiveThumb
		{
			get => _activeThumb;
			set => SetProperty(ref _activeThumb, value);
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
		/// Gets or sets whether to show the range value label.
		/// </summary>
		public bool ShowValueLabel
		{
			get => _showValueLabel;
			set => SetProperty(ref _showValueLabel, value);
		}

		/// <summary>
		/// Gets or sets whether to show min/max labels at the track ends.
		/// </summary>
		public bool ShowMinMaxLabels
		{
			get => _showMinMaxLabels;
			set => SetProperty(ref _showMinMaxLabels, value);
		}

		/// <summary>
		/// Gets or sets the format string for value labels.
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
		/// Gets or sets the color of the filled track portion (between the two thumbs).
		/// </summary>
		public Color? FilledTrackColor
		{
			get => _filledTrackColor ?? Container?.GetConsoleWindowSystem?.Theme?.SliderFilledTrackColor ?? Color.Cyan1;
			set => SetProperty(ref _filledTrackColor, value);
		}

		/// <summary>
		/// Gets or sets the color of inactive thumb.
		/// </summary>
		public Color? ThumbColor
		{
			get => _thumbColor ?? Container?.GetConsoleWindowSystem?.Theme?.SliderThumbColor ?? Color.White;
			set => SetProperty(ref _thumbColor, value);
		}

		/// <summary>
		/// Gets or sets the color of the active/focused thumb.
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
			get => this.GetParentWindow()?.FocusManager.IsFocused(this) ?? false;
		}

		/// <inheritdoc/>
		public bool CanReceiveFocus => _isEnabled;

		/// <summary>
		/// Gets or sets whether the range slider is enabled.
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
		/// Gets whether the range slider is currently being dragged.
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
				// Range label like "25-75"
				width += FormatValue(_maxValue).Length * 2 + 1 + ControlDefaults.SliderLabelSpacing;
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
