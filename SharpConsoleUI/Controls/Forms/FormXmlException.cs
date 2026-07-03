// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;

namespace SharpConsoleUI.Controls.Forms
{
	/// <summary>
	/// Thrown when a declarative form XML document is malformed, has an unknown/unsupported element,
	/// or is missing a required attribute. The message carries element and line/position context so the
	/// author can locate the offending markup.
	/// </summary>
	public sealed class FormXmlException : Exception
	{
		/// <summary>Initializes a new <see cref="FormXmlException"/> with the given message.</summary>
		/// <param name="message">The error message, including element/line context where available.</param>
		public FormXmlException(string message)
			: base(message)
		{
		}

		/// <summary>Initializes a new <see cref="FormXmlException"/> wrapping an inner exception.</summary>
		/// <param name="message">The error message, including element/line context where available.</param>
		/// <param name="inner">The underlying exception (e.g. an <see cref="System.Xml.XmlException"/>).</param>
		public FormXmlException(string message, Exception inner)
			: base(message, inner)
		{
		}
	}
}
