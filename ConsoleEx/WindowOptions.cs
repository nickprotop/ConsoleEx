namespace ConsoleEx
{
    public class WindowOptions
    {
        public string Title { get; set; } = "Window";
		public int Top { get; set; }
        public int Left { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool IsResizable { get; set; } = true;
        public bool IsMoveable { get; set; } = true;
	}
}
