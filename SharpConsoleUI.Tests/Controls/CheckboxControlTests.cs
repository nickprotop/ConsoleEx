// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using Xunit;
using ControlsFactory = SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Tests.Controls;

public class CheckboxControlTests
{
	#region Property defaults and assignment

	[Fact]
	public void CheckedCharacter_DefaultsToX()
	{
		var cb = new CheckboxControl("Label");
		Assert.Equal("X", cb.CheckedCharacter);
	}

	[Fact]
	public void UncheckedCharacter_DefaultsToSpace()
	{
		var cb = new CheckboxControl("Label");
		Assert.Equal(" ", cb.UncheckedCharacter);
	}

	[Fact]
	public void CheckedCharacter_CanBeSetToCustomGlyph()
	{
		var cb = new CheckboxControl("Label") { CheckedCharacter = "✓" };
		Assert.Equal("✓", cb.CheckedCharacter);
	}

	[Fact]
	public void UncheckedCharacter_CanBeSetToCustomGlyph()
	{
		var cb = new CheckboxControl("Label") { UncheckedCharacter = "·" };
		Assert.Equal("·", cb.UncheckedCharacter);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public void CheckedCharacter_NullOrEmpty_FallsBackToDefault(string? value)
	{
		var cb = new CheckboxControl("Label") { CheckedCharacter = "✓" };
		cb.CheckedCharacter = value!;
		Assert.Equal("X", cb.CheckedCharacter);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public void UncheckedCharacter_NullOrEmpty_FallsBackToDefault(string? value)
	{
		var cb = new CheckboxControl("Label") { UncheckedCharacter = "·" };
		cb.UncheckedCharacter = value!;
		Assert.Equal(" ", cb.UncheckedCharacter);
	}

	#endregion

	#region Builder fluent methods

	[Fact]
	public void WithCheckedCharacter_AppliesToBuiltControl()
	{
		var cb = ControlsFactory.Checkbox("Label").WithCheckedCharacter("✓").Build();
		Assert.Equal("✓", cb.CheckedCharacter);
	}

	[Fact]
	public void WithUncheckedCharacter_AppliesToBuiltControl()
	{
		var cb = ControlsFactory.Checkbox("Label").WithUncheckedCharacter("·").Build();
		Assert.Equal("·", cb.UncheckedCharacter);
	}

	[Fact]
	public void WithCheckmark_SetsBothCharacters()
	{
		var cb = ControlsFactory.Checkbox("Label").WithCheckmark("✓", "·").Build();
		Assert.Equal("✓", cb.CheckedCharacter);
		Assert.Equal("·", cb.UncheckedCharacter);
	}

	[Fact]
	public void WithCheckmark_DefaultUncheckedIsSpace()
	{
		var cb = ControlsFactory.Checkbox("Label").WithCheckmark("✓").Build();
		Assert.Equal("✓", cb.CheckedCharacter);
		Assert.Equal(" ", cb.UncheckedCharacter);
	}

	[Fact]
	public void Builder_WithoutCharacterMethods_LeavesDefaults()
	{
		var cb = ControlsFactory.Checkbox("Label").Build();
		Assert.Equal("X", cb.CheckedCharacter);
		Assert.Equal(" ", cb.UncheckedCharacter);
	}

	#endregion

	#region Rendering

	[Fact]
	public void Render_CustomCheckedCharacter_AppearsInOutput()
	{
		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		var cb = ControlsFactory.Checkbox("Enable")
			.WithCheckedCharacter("✓")
			.Checked()
			.Build();
		window.AddControl(cb);

		var output = window.RenderAndGetVisibleContent();
		var plainText = ContainerTestHelpers.StripAnsiCodes(output);

		Assert.Contains("[✓]", plainText);
		Assert.DoesNotContain("[X]", plainText);
	}

	[Fact]
	public void Render_CustomUncheckedCharacter_AppearsInOutput()
	{
		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		var cb = ControlsFactory.Checkbox("Enable")
			.WithUncheckedCharacter("·")
			.Build();
		window.AddControl(cb);

		var output = window.RenderAndGetVisibleContent();
		var plainText = ContainerTestHelpers.StripAnsiCodes(output);

		Assert.Contains("[·]", plainText);
	}

	[Fact]
	public void Render_DefaultCharacters_StillProduceXAndSpace()
	{
		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		var cb = ControlsFactory.Checkbox("Enable").Checked().Build();
		window.AddControl(cb);

		var output = window.RenderAndGetVisibleContent();
		var plainText = ContainerTestHelpers.StripAnsiCodes(output);

		Assert.Contains("[X]", plainText);
	}

	[Fact]
	public void Render_ToggleCheckedState_SwitchesBetweenCharacters()
	{
		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		var cb = ControlsFactory.Checkbox("Enable")
			.WithCheckmark("✓", "·")
			.Build();
		window.AddControl(cb);

		var uncheckedOutput = ContainerTestHelpers.StripAnsiCodes(window.RenderAndGetVisibleContent());
		Assert.Contains("[·]", uncheckedOutput);
		Assert.DoesNotContain("[✓]", uncheckedOutput);

		cb.Toggle();
		var checkedOutput = ContainerTestHelpers.StripAnsiCodes(window.RenderAndGetVisibleContent());
		Assert.Contains("[✓]", checkedOutput);
		Assert.DoesNotContain("[·]", checkedOutput);
	}

	[Fact]
	public void Render_MarkupSensitiveCharacter_DoesNotBreakOutput()
	{
		// User-supplied "[" must be escaped so it isn't parsed as markup.
		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		var cb = ControlsFactory.Checkbox("Enable")
			.WithCheckedCharacter("[")
			.Checked()
			.Build();
		window.AddControl(cb);

		var exception = Record.Exception(() => window.RenderAndGetVisibleContent());
		Assert.Null(exception);

		var plainText = ContainerTestHelpers.StripAnsiCodes(window.RenderAndGetVisibleContent());
		Assert.Contains("[[]", plainText);
		Assert.Contains("Enable", plainText);
	}

	#endregion
}
