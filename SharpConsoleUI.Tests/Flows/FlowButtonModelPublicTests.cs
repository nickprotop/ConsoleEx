using System;
using SharpConsoleUI.Flows;
using Xunit;

namespace SharpConsoleUI.Tests.Flows;

public class FlowButtonModelPublicTests
{
	[Fact]
	public void FlowButton_IsPublic()
	{
		Assert.True(typeof(FlowButton).IsPublic);
	}

	[Fact]
	public void FlowButtons_IsPublic()
	{
		Assert.True(typeof(FlowButtons).IsPublic);
	}

	[Fact]
	public void FlowVerdict_ExistingMembers_StillDefined()
	{
		Assert.True(Enum.IsDefined(typeof(FlowVerdict), FlowVerdict.Next));
		Assert.True(Enum.IsDefined(typeof(FlowVerdict), FlowVerdict.Back));
		Assert.True(Enum.IsDefined(typeof(FlowVerdict), FlowVerdict.Cancel));
		Assert.True(Enum.IsDefined(typeof(FlowVerdict), FlowVerdict.Finish));
		Assert.True(Enum.IsDefined(typeof(FlowVerdict), FlowVerdict.Stay));
	}

	[Fact]
	public void FlowVerdict_NewDialogMembers_Defined()
	{
		Assert.True(Enum.IsDefined(typeof(FlowVerdict), FlowVerdict.Ok));
		Assert.True(Enum.IsDefined(typeof(FlowVerdict), FlowVerdict.Yes));
		Assert.True(Enum.IsDefined(typeof(FlowVerdict), FlowVerdict.No));
		Assert.True(Enum.IsDefined(typeof(FlowVerdict), FlowVerdict.Retry));
		Assert.True(Enum.IsDefined(typeof(FlowVerdict), FlowVerdict.Abort));
		Assert.True(Enum.IsDefined(typeof(FlowVerdict), FlowVerdict.Ignore));
		Assert.True(Enum.IsDefined(typeof(FlowVerdict), FlowVerdict.None));
	}

	[Fact]
	public void FlowVerdict_ExistingNumericValues_Unchanged()
	{
		// Existing members must keep their original numeric values (new members appended after).
		Assert.Equal(0, (int)FlowVerdict.Next);
		Assert.Equal(1, (int)FlowVerdict.Back);
		Assert.Equal(2, (int)FlowVerdict.Cancel);
		Assert.Equal(3, (int)FlowVerdict.Finish);
		Assert.Equal(4, (int)FlowVerdict.Stay);
	}
}
