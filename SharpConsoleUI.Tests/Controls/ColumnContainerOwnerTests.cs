// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Proves <see cref="ColumnContainer"/> can be owned by an HGC (compat path) or by any non-HGC
/// <see cref="IColumnGridOwner"/>, and that the owner-agnostic <see cref="ColumnContainer.OwnerControl"/>
/// exposes the owner in both cases while the compat <see cref="ColumnContainer.HorizontalGridContent"/>
/// property is null for a non-HGC owner.
/// </summary>
public class ColumnContainerOwnerTests
{
	/// <summary>Minimal non-HGC owner used to prove ColumnContainer never touches an HGC-specific member.</summary>
	private sealed class FakeOwner : IColumnGridOwner
	{
		public SharpConsoleUI.Color? ForegroundColor => null;

		public int? ContentWidth => null;
		public HorizontalAlignment HorizontalAlignment { get; set; } = HorizontalAlignment.Left;
		public VerticalAlignment VerticalAlignment { get; set; } = VerticalAlignment.Top;
		public IContainer? Container { get; set; }
		public Margin Margin { get; set; }
		public StickyPosition StickyPosition { get; set; }
		public string? Name { get; set; }
		public object? Tag { get; set; }
		public bool Visible { get; set; } = true;
		public int? Width { get; set; }
		public int? Height { get; set; }
		public int ActualX => 0;
		public int ActualY => 0;
		public int ActualWidth => 0;
		public int ActualHeight => 0;

		public Size GetLogicalContentSize() => Size.Empty;
		public void Invalidate(Invalidation work) { }
		public void Dispose() { }
	}

	[Fact]
	public void HgcOwnedColumn_ExposesHgcAsBothOwnerAndHorizontalGridContent()
	{
		var hgc = new HorizontalGridControl();
		var column = new ColumnContainer(hgc);

		Assert.Same(hgc, column.OwnerControl);
		Assert.Same(hgc, column.HorizontalGridContent);
	}

	[Fact]
	public void NonHgcOwnedColumn_ExposesOwner_AndHorizontalGridContentIsNull()
	{
		var owner = new FakeOwner();
		var column = new ColumnContainer(owner);

		Assert.Same(owner, column.OwnerControl);
		Assert.Null(column.HorizontalGridContent);
	}
}
