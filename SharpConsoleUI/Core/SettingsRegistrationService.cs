using SharpConsoleUI.Controls;

namespace SharpConsoleUI.Core;

/// <summary>
/// Registration for a single settings page within a group.
/// </summary>
public record SettingsPageRegistration(
	string Name,
	string? Icon,
	string? Subtitle,
	Action<ScrollablePanelControl> ContentFactory);

/// <summary>
/// Registration for a settings group containing one or more pages.
/// </summary>
public record SettingsGroupRegistration(
	string Name,
	Color AccentColor,
	List<SettingsPageRegistration> Pages);

/// <summary>
/// Builder for configuring pages within a settings group during registration.
/// </summary>
public sealed class SettingsGroupBuilder
{
	internal readonly List<SettingsPageRegistration> Pages = new();

	/// <summary>
	/// Adds a page to this settings group.
	/// </summary>
	/// <param name="name">The display name of the settings page.</param>
	/// <param name="icon">Optional icon character or string shown beside the page name.</param>
	/// <param name="subtitle">Optional subtitle shown below the page name.</param>
	/// <param name="content">Factory that populates the page content panel.</param>
	/// <returns>This builder for method chaining.</returns>
	public SettingsGroupBuilder AddPage(string name, string? icon = null,
		string? subtitle = null, Action<ScrollablePanelControl>? content = null)
	{
		Pages.Add(new SettingsPageRegistration(name, icon, subtitle, content ?? (_ => { })));
		return this;
	}
}

/// <summary>
/// Stores custom settings group and page registrations for the Settings dialog.
/// </summary>
public sealed class SettingsRegistrationService
{
	private readonly List<SettingsGroupRegistration> _groups = new();
	private static readonly Color DefaultExtensionsColor = new Color(100, 180, 100);

	/// <summary>
	/// Gets all registered settings groups.
	/// </summary>
	public IReadOnlyList<SettingsGroupRegistration> Groups => _groups.AsReadOnly();

	/// <summary>
	/// Registers a settings group with multiple pages.
	/// </summary>
	/// <param name="name">The display name of the settings group.</param>
	/// <param name="accentColor">The accent color used for this group in the navigation sidebar.</param>
	/// <param name="configure">Action that configures the group's pages via a builder.</param>
	public void RegisterGroup(string name, Color accentColor, Action<SettingsGroupBuilder> configure)
	{
		var builder = new SettingsGroupBuilder();
		configure(builder);
		_groups.Add(new SettingsGroupRegistration(name, accentColor, builder.Pages));
	}

	/// <summary>
	/// Registers a single settings page under the "Extensions" group.
	/// Creates the Extensions group if it does not already exist.
	/// </summary>
	/// <param name="name">The display name of the settings page.</param>
	/// <param name="icon">Optional icon character or string shown beside the page name.</param>
	/// <param name="subtitle">Optional subtitle shown below the page name.</param>
	/// <param name="content">Factory that populates the page content panel.</param>
	public void RegisterPage(string name, string? icon = null,
		string? subtitle = null, Action<ScrollablePanelControl>? content = null)
	{
		var extGroup = _groups.Find(g => g.Name == "Extensions");
		if (extGroup == null)
		{
			extGroup = new SettingsGroupRegistration("Extensions", DefaultExtensionsColor, new List<SettingsPageRegistration>());
			_groups.Add(extGroup);
		}
		extGroup.Pages.Add(new SettingsPageRegistration(name, icon, subtitle, content ?? (_ => { })));
	}

	/// <summary>
	/// Removes a registered settings group by name.
	/// </summary>
	/// <param name="name">The name of the group to remove.</param>
	public void UnregisterGroup(string name)
	{
		_groups.RemoveAll(g => g.Name == name);
	}
}
