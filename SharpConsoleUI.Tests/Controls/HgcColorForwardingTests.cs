// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using Xunit;
using Color = SharpConsoleUI.Color;

namespace SharpConsoleUI.Tests.Controls
{
	public class HgcColorForwardingTests
	{
		[Fact]
		public void BackgroundColor_Setter_ForwardsToBase_AndRoundTrips()
		{
			var hgc = new HorizontalGridControl();
			hgc.BackgroundColor = Color.Blue;

			// HGC's Color? surface round-trips exactly...
			Assert.Equal(Color.Blue, hgc.BackgroundColor);
			// ...AND it reaches the base GridControl property that paint reads (was a silent no-op before).
			Assert.Equal(Color.Blue, ((GridControl)hgc).BackgroundColor);
		}

		[Fact]
		public void ForegroundColor_Setter_ForwardsToBase_AndRoundTrips()
		{
			var hgc = new HorizontalGridControl();
			hgc.ForegroundColor = Color.Red;
			Assert.Equal(Color.Red, hgc.ForegroundColor);
			Assert.Equal(Color.Red, ((GridControl)hgc).ForegroundColor);
		}

		[Fact]
		public void BackgroundColor_Null_ForwardsUnsetSentinelToBase()
		{
			var hgc = new HorizontalGridControl();
			hgc.BackgroundColor = Color.Blue;
			hgc.BackgroundColor = null;

			Assert.Null(hgc.BackgroundColor); // shadow round-trips null
			// null forwards Color.Default (the framework's "unset" sentinel) into the base, NOT a literal
			// color — ColorResolver.Coalesce treats Default as no-explicit-value (resolves to no fill).
			Assert.True(((GridControl)hgc).BackgroundColor.IsDefault);
		}

		[Fact]
		public void ForegroundColor_Null_ResolvesToThemeNotTransparent()
		{
			var hgc = new HorizontalGridControl();
			hgc.ForegroundColor = Color.Red;
			hgc.ForegroundColor = null;

			Assert.Null(hgc.ForegroundColor); // shadow round-trips null
			// A null foreground resolves to the theme/default, never Color.Transparent (meaningless for text).
			Assert.NotEqual(Color.Transparent, ((GridControl)hgc).ForegroundColor);
		}
	}
}
