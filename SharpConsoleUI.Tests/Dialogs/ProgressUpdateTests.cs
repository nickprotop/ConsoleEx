using SharpConsoleUI.Dialogs;
using Xunit;

namespace SharpConsoleUI.Tests.Dialogs
{
	public class ProgressUpdateTests
	{
		[Fact]
		public void Ctor_DefaultsAllFieldsToNull()
		{
			var u = new ProgressUpdate();
			Assert.Null(u.Fraction);
			Assert.Null(u.Message);
			Assert.Null(u.Indeterminate);
		}

		[Fact]
		public void Ctor_FractionOnly_LeavesOthersNull()
		{
			var u = new ProgressUpdate(fraction: 0.4);
			Assert.Equal(0.4, u.Fraction);
			Assert.Null(u.Message);
			Assert.Null(u.Indeterminate);
		}

		[Fact]
		public void Ctor_MessageOnly_LeavesOthersNull()
		{
			var u = new ProgressUpdate(message: "hi");
			Assert.Null(u.Fraction);
			Assert.Equal("hi", u.Message);
			Assert.Null(u.Indeterminate);
		}

		[Fact]
		public void Ctor_IndeterminateOnly_LeavesOthersNull()
		{
			var u = new ProgressUpdate(indeterminate: true);
			Assert.Null(u.Fraction);
			Assert.Null(u.Message);
			Assert.True(u.Indeterminate);
		}

		[Fact]
		public void EmptyStringMessage_IsDistinctFromNull()
		{
			var u = new ProgressUpdate(message: "");
			Assert.Equal("", u.Message);
			Assert.NotNull(u.Message);
		}
	}
}
