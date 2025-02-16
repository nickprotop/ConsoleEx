using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleEx
{
    public interface IContainer
    {
        public Color BackgroundColor { get; set; }
        public Color ForegroundColor { get; set; }

        public void Invalidate();

        public bool IsDirty { get; set; }
    }
}