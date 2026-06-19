using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class ColorRoleRuleTests
{
	// The rule's role accent colours the rule line (ColorRoleForeground). The control exposes no
	// resolved-colour getter, so role correctness is verified via the resolver + a builder round-trip.

	[Fact]
	public void DefaultRole_ProducesNoRoleForeground()
	{
		var rule = new RuleControl();
		Assert.Null(ColorResolver.ColorRoleForeground(rule.ColorRole, rule.Container, rule.Outline));
	}

	[Fact]
	public void DangerRole_ProducesRoleForeground()
	{
		var rule = new RuleControl { ColorRole = ColorRole.Danger };
		Assert.NotNull(ColorResolver.ColorRoleForeground(rule.ColorRole, rule.Container, rule.Outline));
	}

	[Fact]
	public void ExplicitColorWins_RoleStillResolvable()
	{
		var rule = new RuleControl { ColorRole = ColorRole.Danger, Color = Color.Black };
		// Explicit Color is preserved on the control...
		Assert.Equal(Color.Black, rule.Color);
		// ...while the role remains resolvable (the renderer prefers the explicit value).
		Assert.NotNull(ColorResolver.ColorRoleForeground(rule.ColorRole, rule.Container, rule.Outline));
	}

	[Fact]
	public void Builder_RoundTripsRoleAndOutline()
	{
		var rule = new RuleBuilder()
			.WithColorRole(ColorRole.Success)
			.Outline()
			.Build();
		Assert.Equal(ColorRole.Success, rule.ColorRole);
		Assert.True(rule.Outline);
	}
}
