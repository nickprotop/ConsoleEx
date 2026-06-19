using SharpConsoleUI.Core;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Verifies the "defaults by purpose" behaviour: built-in components with an inherent
/// semantic purpose default their <see cref="ControlRole"/> from their severity so a
/// warning surface is amber, an error surface red, etc. — without the caller setting anything.
///
/// NOTE: the library has no MessageBox type (dialogs in <c>SharpConsoleUI.Dialogs</c> have no
/// severity variants), so the only defaults-by-purpose surface is the notification path. These
/// tests assert the severity → role mapping that the notification window applies to its themed
/// (close button) control.
/// </summary>
public class RoleDefaultsByPurposeTests
{
	[Fact]
	public void Warning_MapsToWarningRole()
	{
		Assert.Equal(ControlRole.Warning,
			NotificationStateService.MapSeverityToRole(NotificationSeverityEnum.Warning));
	}

	[Fact]
	public void Danger_MapsToDangerRole()
	{
		Assert.Equal(ControlRole.Danger,
			NotificationStateService.MapSeverityToRole(NotificationSeverityEnum.Danger));
	}

	[Fact]
	public void Success_MapsToSuccessRole()
	{
		Assert.Equal(ControlRole.Success,
			NotificationStateService.MapSeverityToRole(NotificationSeverityEnum.Success));
	}

	[Fact]
	public void Info_MapsToInfoRole()
	{
		Assert.Equal(ControlRole.Info,
			NotificationStateService.MapSeverityToRole(NotificationSeverityEnum.Info));
	}

	[Fact]
	public void None_MapsToDefaultRole_NoImpliedAccent()
	{
		// None has no semantic purpose, so it must leave the control on its legacy (Default) colours.
		Assert.Equal(ControlRole.Default,
			NotificationStateService.MapSeverityToRole(NotificationSeverityEnum.None));
	}
}
