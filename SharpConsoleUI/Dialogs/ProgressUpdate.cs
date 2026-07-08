// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Dialogs
{
	/// <summary>
	/// A single progress update for <see cref="Dialogs.RunWithProgressAsync{T}(ConsoleWindowSystem, string, string, System.Func{System.Threading.CancellationToken, System.IProgress{ProgressUpdate}, System.Threading.Tasks.Task{T}}, bool, Window)"/>.
	/// Every field is optional; an omitted (null) field leaves that element of the dialog unchanged,
	/// so callers can update the bar and the status line independently.
	/// </summary>
	public readonly struct ProgressUpdate
	{
		/// <summary>0..1 → determinate bar (clamped on apply). <c>null</c> → leave the bar unchanged.</summary>
		public double? Fraction { get; init; }

		/// <summary>Status-line text. <c>null</c> → leave the text unchanged. <c>""</c> → clear the line (a real update).</summary>
		public string? Message { get; init; }

		/// <summary><c>true</c> → bar switches to indeterminate pulse; <c>false</c> → determinate; <c>null</c> → unchanged.</summary>
		public bool? Indeterminate { get; init; }

		/// <summary>Creates a progress update. Omit any argument to leave that element unchanged.</summary>
		public ProgressUpdate(double? fraction = null, string? message = null, bool? indeterminate = null)
		{
			Fraction = fraction;
			Message = message;
			Indeterminate = indeterminate;
		}
	}
}
