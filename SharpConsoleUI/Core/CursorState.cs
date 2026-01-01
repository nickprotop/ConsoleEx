// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Controls;

namespace SharpConsoleUI.Core
{
	/// <summary>
	/// Interface for controls that can specify a preferred cursor shape.
	/// Controls implementing this interface can customize how the cursor appears when focused.
	/// </summary>
	public interface ICursorShapeProvider
	{
		/// <summary>
		/// Gets the preferred cursor shape for this control.
		/// Return null to use the default cursor shape.
		/// </summary>
		CursorShape? PreferredCursorShape { get; }
	}

	/// <summary>
	/// Represents the shape/style of the cursor
	/// </summary>
	public enum CursorShape
	{
		/// <summary>Solid block cursor (default)</summary>
		Block,
		/// <summary>Underline cursor</summary>
		Underline,
		/// <summary>Vertical bar cursor</summary>
		VerticalBar,
		/// <summary>No visible cursor</summary>
		Hidden
	}

	/// <summary>
	/// Immutable record representing the current state of the cursor.
	/// Provides a single source of truth for cursor visibility, position, and ownership.
	/// </summary>
	public record CursorState
	{
		/// <summary>
		/// Whether the cursor is currently visible
		/// </summary>
		public bool IsVisible { get; init; }

		/// <summary>
		/// Absolute screen position of the cursor
		/// </summary>
		public Point AbsolutePosition { get; init; }

		/// <summary>
		/// Logical position within the owner control's coordinate space (if any)
		/// </summary>
		public Point? LogicalPosition { get; init; }

		/// <summary>
		/// The control that currently owns the cursor (if any)
		/// </summary>
		public IWindowControl? OwnerControl { get; init; }

		/// <summary>
		/// The window containing the cursor owner control (if any)
		/// </summary>
		public Window? OwnerWindow { get; init; }

		/// <summary>
		/// The visual shape/style of the cursor
		/// </summary>
		public CursorShape Shape { get; init; }

		/// <summary>
		/// Timestamp when this state was created
		/// </summary>
		public DateTime UpdateTime { get; init; }

		/// <summary>
		/// Creates a new cursor state
		/// </summary>
		public CursorState(
			bool isVisible = false,
			Point? absolutePosition = null,
			Point? logicalPosition = null,
			IWindowControl? ownerControl = null,
			Window? ownerWindow = null,
			CursorShape shape = CursorShape.Block)
		{
			IsVisible = isVisible;
			AbsolutePosition = absolutePosition ?? Point.Empty;
			LogicalPosition = logicalPosition;
			OwnerControl = ownerControl;
			OwnerWindow = ownerWindow;
			Shape = shape;
			UpdateTime = DateTime.UtcNow;
		}

		/// <summary>
		/// A hidden cursor state with no owner
		/// </summary>
		public static readonly CursorState Hidden = new(isVisible: false);

		/// <summary>
		/// Returns true if cursor position has changed compared to another state
		/// </summary>
		public bool HasPositionChanged(CursorState other)
		{
			return AbsolutePosition != other.AbsolutePosition;
		}

		/// <summary>
		/// Returns true if cursor visibility has changed compared to another state
		/// </summary>
		public bool HasVisibilityChanged(CursorState other)
		{
			return IsVisible != other.IsVisible;
		}

		/// <summary>
		/// Returns true if cursor owner has changed compared to another state
		/// </summary>
		public bool HasOwnerChanged(CursorState other)
		{
			return OwnerControl != other.OwnerControl || OwnerWindow != other.OwnerWindow;
		}
	}

	/// <summary>
	/// Event arguments for cursor state changes
	/// </summary>
	public class CursorStateChangedEventArgs : EventArgs
	{
		/// <summary>
		/// The previous cursor state
		/// </summary>
		public CursorState PreviousState { get; }

		/// <summary>
		/// The new cursor state
		/// </summary>
		public CursorState NewState { get; }

		/// <summary>
		/// Whether the position changed
		/// </summary>
		public bool PositionChanged => NewState.HasPositionChanged(PreviousState);

		/// <summary>
		/// Whether the visibility changed
		/// </summary>
		public bool VisibilityChanged => NewState.HasVisibilityChanged(PreviousState);

		/// <summary>
		/// Whether the owner changed
		/// </summary>
		public bool OwnerChanged => NewState.HasOwnerChanged(PreviousState);

		public CursorStateChangedEventArgs(CursorState previousState, CursorState newState)
		{
			PreviousState = previousState;
			NewState = newState;
		}
	}
}
