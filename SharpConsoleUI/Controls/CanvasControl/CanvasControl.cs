// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A free-form drawing surface control that exposes CharacterBuffer drawing primitives
	/// through a local-coordinate API. Supports both async/on-demand painting via
	/// <see cref="BeginPaint"/>/<see cref="EndPaint"/> and event-driven painting via the
	/// <see cref="Paint"/> event. Both modes can be combined.
	/// </summary>
	public partial class CanvasControl : BaseControl, IInteractiveControl, IFocusableControl, IMouseAwareControl
	{
		#region Fields

		private int _canvasWidth;
		private int _canvasHeight;
		private CharacterBuffer _internalBuffer;
		private readonly object _bufferLock = new();
		private bool _isEnabled = true;
		private bool _autoClear;
		private bool _autoSize;
		private Color? _backgroundColorValue;
		private Color? _foregroundColorValue;

		#endregion

		#region Constructors

		/// <summary>
		/// Initializes a new CanvasControl with default dimensions.
		/// </summary>
		public CanvasControl()
			: this(ControlDefaults.DefaultCanvasWidth, ControlDefaults.DefaultCanvasHeight)
		{
		}

		/// <summary>
		/// Initializes a new CanvasControl with the specified dimensions.
		/// </summary>
		/// <param name="canvasWidth">The initial canvas width in characters.</param>
		/// <param name="canvasHeight">The initial canvas height in characters.</param>
		public CanvasControl(int canvasWidth, int canvasHeight)
		{
			_canvasWidth = Math.Max(ControlDefaults.MinCanvasSize, canvasWidth);
			_canvasHeight = Math.Max(ControlDefaults.MinCanvasSize, canvasHeight);
			_internalBuffer = new CharacterBuffer(_canvasWidth, _canvasHeight);
		}

		#endregion

		#region Properties

		/// <inheritdoc/>
		public override int? ContentWidth => _canvasWidth + Margin.Left + Margin.Right;

		/// <summary>
		/// Gets or sets the canvas width in characters. Clamped to <see cref="ControlDefaults.MinCanvasSize"/>.
		/// Setting this recreates the internal buffer (previous content is lost).
		/// </summary>
		public int CanvasWidth
		{
			get => _canvasWidth;
			set
			{
				int clamped = Math.Max(ControlDefaults.MinCanvasSize, value);
				if (_canvasWidth == clamped) return;
				_canvasWidth = clamped;
				OnPropertyChanged();
				RecreateInternalBuffer();
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the canvas height in characters. Clamped to <see cref="ControlDefaults.MinCanvasSize"/>.
		/// Setting this recreates the internal buffer (previous content is lost).
		/// </summary>
		public int CanvasHeight
		{
			get => _canvasHeight;
			set
			{
				int clamped = Math.Max(ControlDefaults.MinCanvasSize, value);
				if (_canvasHeight == clamped) return;
				_canvasHeight = clamped;
				OnPropertyChanged();
				RecreateInternalBuffer();
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// When true, the internal buffer is cleared after compositing each frame,
		/// so the <see cref="Paint"/> event redraws from scratch. Default is false (retained mode).
		/// </summary>
		public bool AutoClear
		{
			get => _autoClear;
			set { _autoClear = value; OnPropertyChanged(); }
		}

		/// <summary>
		/// When true, the internal buffer automatically resizes to match the layout bounds
		/// assigned by the parent container. Enable this when using Stretch/Fill alignment
		/// so the canvas adapts to the available window space.
		/// </summary>
		public bool AutoSize
		{
			get => _autoSize;
			set { _autoSize = value; OnPropertyChanged(); }
		}

		/// <summary>
		/// Gets or sets the background color. Falls back to container, then black.
		/// </summary>
		public Color BackgroundColor
		{
			get => _backgroundColorValue ?? Container?.BackgroundColor ?? Color.Black;
			set => SetProperty(ref _backgroundColorValue, (Color?)value);
		}

		/// <summary>
		/// Gets or sets the foreground color. Falls back to container, then white.
		/// </summary>
		public Color ForegroundColor
		{
			get => _foregroundColorValue ?? Container?.ForegroundColor ?? Color.White;
			set => SetProperty(ref _foregroundColorValue, (Color?)value);
		}

		/// <summary>
		/// Gets or sets whether the control is enabled and can receive input.
		/// </summary>
		public bool IsEnabled
		{
			get => _isEnabled;
			set => SetProperty(ref _isEnabled, value);
		}

		#endregion

		#region BeginPaint / EndPaint

		/// <summary>
		/// Returns a <see cref="CanvasGraphics"/> wrapping the internal buffer for async drawing.
		/// Must be paired with <see cref="EndPaint"/>. Thread-safe via monitor lock.
		/// </summary>
		/// <returns>A drawing context for the internal canvas buffer.</returns>
		public CanvasGraphics BeginPaint()
		{
			Monitor.Enter(_bufferLock);
			return new CanvasGraphics(_internalBuffer, 0, 0,
				_canvasWidth, _canvasHeight, _internalBuffer.Bounds);
		}

		/// <summary>
		/// Releases the paint lock and triggers a repaint.
		/// </summary>
		public void EndPaint()
		{
			Monitor.Exit(_bufferLock);
			Container?.Invalidate(true);
		}

		/// <summary>
		/// Triggers a repaint without painting.
		/// </summary>
		public void Refresh()
			=> Container?.Invalidate(true);

		/// <summary>
		/// Clears the internal buffer with the control's background color.
		/// </summary>
		public void Clear()
		{
			lock (_bufferLock)
			{
				_internalBuffer.Clear(BackgroundColor);
			}
			Container?.Invalidate(true);
		}

		/// <summary>
		/// Clears the internal buffer with the specified background color.
		/// </summary>
		/// <param name="bg">The background color to fill with.</param>
		public void Clear(Color bg)
		{
			lock (_bufferLock)
			{
				_internalBuffer.Clear(bg);
			}
			Container?.Invalidate(true);
		}

		#endregion

		#region Events

		/// <summary>
		/// Fires during each render cycle after the internal buffer is composited.
		/// The <see cref="CanvasPaintEventArgs.Graphics"/> context draws directly to the window buffer.
		/// </summary>
		public event EventHandler<CanvasPaintEventArgs>? Paint;

		/// <summary>
		/// Fires when the canvas is left-clicked, with canvas-local coordinates.
		/// </summary>
		public event EventHandler<CanvasMouseEventArgs>? CanvasMouseClick;

		/// <summary>
		/// Fires when the mouse moves over the canvas, with canvas-local coordinates.
		/// </summary>
		public event EventHandler<CanvasMouseEventArgs>? CanvasMouseMove;

		/// <summary>
		/// Fires when the canvas is right-clicked, with canvas-local coordinates.
		/// </summary>
		public event EventHandler<CanvasMouseEventArgs>? CanvasMouseRightClick;

		/// <summary>
		/// Fires when a key is pressed while the canvas has focus.
		/// </summary>
		public event EventHandler<ConsoleKeyInfo>? CanvasKeyPressed;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseClick;

		/// <inheritdoc/>
		#pragma warning disable CS0067, CS0414
		public event EventHandler<MouseEventArgs>? MouseDoubleClick;
		#pragma warning restore CS0067, CS0414

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseRightClick;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseEnter;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseLeave;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseMove;

		#endregion

		#region IFocusableControl

		/// <inheritdoc/>
		public bool HasFocus
		{
			get => this.GetParentWindow()?.FocusManager.IsFocused(this) ?? false;
		}

		/// <inheritdoc/>
		public bool CanReceiveFocus => IsEnabled;

		#endregion

		#region Private Helpers

		private void RecreateInternalBuffer()
		{
			lock (_bufferLock)
			{
				_internalBuffer = new CharacterBuffer(_canvasWidth, _canvasHeight);
			}
		}

		#endregion

		#region Disposal

		/// <inheritdoc/>
		protected override void OnDisposing()
		{
			Paint = null;
			CanvasMouseClick = null;
			CanvasMouseMove = null;
			CanvasMouseRightClick = null;
			CanvasKeyPressed = null;
			MouseClick = null;
			MouseDoubleClick = null;
			MouseRightClick = null;
			MouseEnter = null;
			MouseLeave = null;
			MouseMove = null;
		}

		#endregion
	}
}
