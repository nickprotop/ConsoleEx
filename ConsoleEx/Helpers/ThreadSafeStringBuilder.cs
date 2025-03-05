using System.Text;

namespace ConsoleEx.Helpers
{
	public class ThreadSafeStringBuilder
	{
		private readonly object _lockObject = new object();
		private readonly StringBuilder _stringBuilder = new StringBuilder();

		public void Append(string? value)
		{
			lock (_lockObject)
			{
				_stringBuilder.Append(value);
			}
		}

		public void AppendLine(string? value)
		{
			lock (_lockObject)
			{
				_stringBuilder.AppendLine(value);
			}
		}

		public override string ToString()
		{
			lock (_lockObject)
			{
				return _stringBuilder.ToString();
			}
		}
	}
}