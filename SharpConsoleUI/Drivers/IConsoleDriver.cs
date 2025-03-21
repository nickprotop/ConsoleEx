﻿// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Size = SharpConsoleUI.Helpers.Size;

namespace SharpConsoleUI.Drivers
{
	public interface IConsoleDriver
	{
		public delegate void MouseEventHandler(object sender, List<MouseFlags> flags, Point point);

		public event EventHandler<ConsoleKeyInfo> KeyPressed;

		public event MouseEventHandler? MouseEvent;

		public event EventHandler<Size>? ScreenResized;

		public Size ScreenSize { get; }

		public void Clear();

		public void Flush();

		public void Start();

		public void Stop();

		public void WriteToConsole(int x, int y, string value);
	}
}