namespace ConsoleEx
{
	public interface IWIndowContent
	{
		public string Guid { get; }
		public Window? Container { get; set; }
		public List<string> RenderContent(int? width, int? height);
		public List<string> RenderContent(int? width, int? height, bool overflow);

	}
}