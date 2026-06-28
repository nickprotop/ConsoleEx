using System;
using SharpConsoleUI.Flows;
using Xunit;

namespace SharpConsoleUI.Tests.Flows;

public class FlowResultTests
{
	[Fact]
	public void Complete_CarriesValue()
	{
		var r = FlowResult<int>.Complete(7);
		Assert.True(r.Completed); Assert.Equal(7, r.Value);
		Assert.False(r.Cancelled); Assert.False(r.Faulted);
	}

	[Fact]
	public void Cancel_IsCancelledOnly()
	{
		var r = FlowResult<int>.Cancel();
		Assert.True(r.Cancelled); Assert.False(r.Completed); Assert.False(r.Faulted);
	}

	[Fact]
	public void Fault_CarriesError()
	{
		var ex = new InvalidOperationException("x");
		var r = FlowResult<int>.Fault(ex);
		Assert.True(r.Faulted); Assert.Same(ex, r.Error); Assert.False(r.Completed);
	}
}
