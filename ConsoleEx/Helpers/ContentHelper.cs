using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleEx.Helpers
{
	public static class ContentHelper
	{
		public static int GetCenter(int availableWidth, int ContentWidth)
		{
			int center = (availableWidth - ContentWidth) / 2;
			if (center < 0) center = 0;
			return center;
		}
	}
}