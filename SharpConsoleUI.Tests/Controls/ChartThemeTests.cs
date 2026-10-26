// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls
{
	/// <summary>
	/// A chart that follows the theme rather than hard-coded colours.
	/// </summary>
	/// <remarks>
	/// Apps resolve theme colours themselves and hand them over - cxnet reads
	/// PrimaryColor and SecondaryColor into its own palette. That works and stays
	/// supported. But a series given no colour at all should fall back to the
	/// chart's ColorRole the way LineGraphControl does, instead of the role being
	/// a property the control ignores.
	/// </remarks>
	public class ChartThemeTests
	{
		[Fact]
		public void AChartExposesAColorRole()
		{
			var chart = new ChartControl { ColorRole = ColorRole.Danger };

			Assert.Equal(ColorRole.Danger, chart.ColorRole);
		}

		[Fact]
		public void TheRoleGivesADefaultSeriesColour()
		{
			// AddSeries without a colour should take one from the role, so a chart
			// can be themed without the caller resolving anything.
			var plain = new ChartControl();
			var danger = new ChartControl { ColorRole = ColorRole.Danger };

			var a = plain.AddSeries("s");
			var b = danger.AddSeries("s");

			Assert.NotEqual(a.Color, b.Color);
		}

		[Fact]
		public void AnExplicitColourWinsOverTheRole()
		{
			var chart = new ChartControl { ColorRole = ColorRole.Danger };

			var s = chart.AddSeries("s", Color.Green);

			Assert.Equal(Color.Green, s.Color);
		}

		[Fact]
		public void SeveralRolelessSeriesGetDistinctColours()
		{
			// A chart of three unnamed-colour series must not draw three identical
			// lines; a palette walk is what makes "just add series" usable.
			var chart = new ChartControl();

			var a = chart.AddSeries("a");
			var b = chart.AddSeries("b");
			var c = chart.AddSeries("c");

			Assert.NotEqual(a.Color, b.Color);
			Assert.NotEqual(b.Color, c.Color);
			Assert.NotEqual(a.Color, c.Color);
		}
	}
}
