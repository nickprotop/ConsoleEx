using System.Text;

namespace SharpConsoleUI.Helpers
{
	/// <summary>
	/// Provides a thread-safe wrapper around <see cref="StringBuilder"/> for concurrent string building operations.
	/// </summary>
	/// <remarks>
	/// All methods in this class are synchronized using a lock to ensure thread safety
	/// when multiple threads are appending to or reading from the string builder.
	/// </remarks>
	public class ThreadSafeStringBuilder
	{
		private readonly object _lockObject = new object();
		private readonly StringBuilder _stringBuilder = new StringBuilder();

		/// <summary>
		/// Appends the specified string to the end of this instance in a thread-safe manner.
		/// </summary>
		/// <param name="value">The string to append, or <c>null</c>.</param>
		public void Append(string? value)
		{
			lock (_lockObject)
			{
				_stringBuilder.Append(value);
			}
		}

		/// <summary>
		/// Appends the specified string followed by the default line terminator in a thread-safe manner.
		/// </summary>
		/// <param name="value">The string to append, or <c>null</c>.</param>
		public void AppendLine(string? value)
		{
			lock (_lockObject)
			{
				_stringBuilder.AppendLine(value);
			}
		}

		/// <summary>
		/// Converts the value of this instance to a <see cref="string"/> in a thread-safe manner.
		/// </summary>
		/// <returns>A string whose value is the same as this instance.</returns>
		public override string ToString()
		{
			lock (_lockObject)
			{
				return _stringBuilder.ToString();
			}
		}
	}
}