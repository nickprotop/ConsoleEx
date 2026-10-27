// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Linq;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.ColorCascading
{
	/// <summary>
	/// Why a role is the colour it is.
	/// </summary>
	/// <remarks>
	/// A resolved role is a computed colour, not a looked-up one: a theme anchor
	/// seeds it, contrast against the surface may move it, a focused state tints
	/// it and a disabled state fades it. The result is one Color, and when it is
	/// wrong the question is which of those steps moved it - which the colour
	/// alone cannot answer.
	/// </remarks>
	public class ColorCascadeTests
	{
		private static ITheme Theme() => new ModernGrayTheme();

		[Fact]
		public void TheCascadeAlwaysEndsAtTheColourThatWasResolved()
		{
			// The one guarantee the whole verb rests on: the explanation must
			// describe the REAL answer, not a parallel computation that usually
			// agrees. Checking one role in one state is not enough - a step
			// dropped from the middle still lands on the right colour in the
			// simple cases and lies in the rest.
			var theme = Theme();
			var mismatches = new System.Collections.Generic.List<string>();

			foreach (var role in System.Enum.GetValues<ColorRole>())
			{
				foreach (var state in System.Enum.GetValues<ColorRoleState>())
				{
					foreach (bool outline in new[] { false, true })
					{
						var resolved = ColorRoleResolver.Resolve(role, theme, outline, state);
						var cascade = ColorRoleResolver.Explain(role, theme, outline, state);

						if (cascade.Count == 0)
						{
							mismatches.Add($"{role}/{state}/outline={outline}: empty cascade");
							continue;
						}

						if (cascade[^1].Color != resolved.Background)
						{
							mismatches.Add(
								$"{role}/{state}/outline={outline}: cascade ends at "
									+ $"{cascade[^1].Color} but Resolve returned {resolved.Background}");
						}
					}
				}
			}

			Assert.True(mismatches.Count == 0, string.Join("; ", mismatches.Take(5)));
		}

		[Fact]
		public void TheCascadeNamesTheThemeAnchorItStartedFrom()
		{
			var cascade = ColorRoleResolver.Explain(ColorRole.Success, Theme());

			Assert.Contains(cascade, step => step.Source.Contains("SuccessColor"));
		}

		[Fact]
		public void AFocusedRoleShowsTheTintStep()
		{
			var normal = ColorRoleResolver.Explain(ColorRole.Primary, Theme());
			var focused = ColorRoleResolver.Explain(ColorRole.Primary, Theme(),
				state: ColorRoleState.Focused);

			Assert.DoesNotContain(normal, s => s.Source.Contains("focused"));
			Assert.Contains(focused, s => s.Source.Contains("focused"));
		}

		[Fact]
		public void ADisabledRoleShowsTheFadeStep()
		{
			var cascade = ColorRoleResolver.Explain(ColorRole.Danger, Theme(),
				state: ColorRoleState.Disabled);

			Assert.Contains(cascade, s => s.Source.Contains("disabled"));
		}

		[Fact]
		public void AContrastAdjustmentIsNamedWhenItHappens()
		{
			// A role whose seed sits close to the window background gets moved so
			// it stays legible, and that is the answer to "why is this not the
			// colour I set". Which roles need it depends on the theme, so this
			// asks every role on every built-in theme and requires that at least
			// one does - a cascade that never reported the step would otherwise
			// pass by saying nothing.
			var roles = System.Enum.GetValues<ColorRole>()
				.Where(r => r != ColorRole.Default).ToList();

			// A near-surface seed is the case that needs adjusting, so one theme is
			// built with exactly that: a primary almost the colour of its own
			// background, which must be moved to stay legible.
			var nearSurface = new MutableTheme
			{
				WindowBackgroundColor = new Color(30, 30, 30),
				WindowForegroundColor = new Color(230, 230, 230),
				PrimaryColor = new Color(34, 34, 34),
			};

			var themes = new ITheme[] { new ModernGrayTheme(), nearSurface };

			var adjusted = (
				from theme in themes
				from role in roles
				from outline in new[] { false, true }
				let steps = ColorRoleResolver.Explain(role, theme, outline)
				where steps.Any(s => s.Source.Contains("contrast"))
				select $"{theme.GetType().Name}/{role}").ToList();

			Assert.True(adjusted.Count > 0,
				"no role on any built-in theme reported a contrast adjustment");
		}

		[Fact]
		public void TheDefaultRoleSaysItTookTheSurface()
		{
			// Default is not a colour of its own: it is the window's own surface,
			// and a cascade that invented a seed for it would be lying.
			var cascade = ColorRoleResolver.Explain(ColorRole.Default, Theme());

			Assert.Contains(cascade, s => s.Source.Contains("surface"));
		}
	}
}
