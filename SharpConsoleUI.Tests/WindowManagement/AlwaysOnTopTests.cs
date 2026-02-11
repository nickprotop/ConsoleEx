using SharpConsoleUI;
using SharpConsoleUI.Tests.Infrastructure;
using System.Linq;
using Xunit;

namespace SharpConsoleUI.Tests.WindowManagement;

/// <summary>
/// Tests AlwaysOnTop window behavior: z-order invariant, activation, registration, rendering.
/// </summary>
public class AlwaysOnTopTests
{
	[Fact]
	public void AlwaysOnTop_ZIndex_AboveNormalWindows_AfterRegistration()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var normal = new Window(system) { Title = "Normal" };
		var onTop = new Window(system) { Title = "OnTop", AlwaysOnTop = true };

		system.WindowStateService.AddWindow(normal);
		system.WindowStateService.AddWindow(onTop);

		Assert.True(onTop.ZIndex > normal.ZIndex);
	}

	[Fact]
	public void AlwaysOnTop_ZIndex_AboveNormalWindows_WhenRegisteredFirst()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var onTop = new Window(system) { Title = "OnTop", AlwaysOnTop = true };
		var normal = new Window(system) { Title = "Normal" };

		system.WindowStateService.AddWindow(onTop);
		system.WindowStateService.AddWindow(normal);

		Assert.True(onTop.ZIndex > normal.ZIndex);
	}

	[Fact]
	public void AlwaysOnTop_StaysAbove_WhenNormalWindowActivated()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var normal1 = new Window(system) { Title = "Normal1" };
		var onTop = new Window(system) { Title = "OnTop", AlwaysOnTop = true };
		var normal2 = new Window(system) { Title = "Normal2" };

		system.WindowStateService.AddWindow(normal1);
		system.WindowStateService.AddWindow(onTop);
		system.WindowStateService.AddWindow(normal2);

		// Activate a normal window — should not surpass AlwaysOnTop
		system.WindowStateService.ActivateWindow(normal1);

		Assert.True(onTop.ZIndex > normal1.ZIndex);
		Assert.True(onTop.ZIndex > normal2.ZIndex);
	}

	[Fact]
	public void AlwaysOnTop_StaysAbove_WhenNormalWindowBroughtToFront()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var normal = new Window(system) { Title = "Normal" };
		var onTop = new Window(system) { Title = "OnTop", AlwaysOnTop = true };

		system.WindowStateService.AddWindow(normal);
		system.WindowStateService.AddWindow(onTop);

		system.WindowStateService.BringToFront(normal);

		Assert.True(onTop.ZIndex > normal.ZIndex);
	}

	[Fact]
	public void AlwaysOnTop_AppearsFirst_InZOrderList()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var normal1 = new Window(system) { Title = "Normal1" };
		var normal2 = new Window(system) { Title = "Normal2" };
		var onTop = new Window(system) { Title = "OnTop", AlwaysOnTop = true };

		system.WindowStateService.AddWindow(normal1);
		system.WindowStateService.AddWindow(normal2);
		system.WindowStateService.AddWindow(onTop);

		var zOrder = system.WindowStateService.GetWindowsByZOrder();
		Assert.Same(onTop, zOrder.First());
	}

	[Fact]
	public void AlwaysOnTop_MultipleOnTop_AllAboveNormal()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var normal = new Window(system) { Title = "Normal" };
		var onTop1 = new Window(system) { Title = "OnTop1", AlwaysOnTop = true };
		var onTop2 = new Window(system) { Title = "OnTop2", AlwaysOnTop = true };

		system.WindowStateService.AddWindow(normal);
		system.WindowStateService.AddWindow(onTop1);
		system.WindowStateService.AddWindow(onTop2);

		Assert.True(onTop1.ZIndex > normal.ZIndex);
		Assert.True(onTop2.ZIndex > normal.ZIndex);
	}

	[Fact]
	public void AlwaysOnTop_MultipleOnTop_MaintainRelativeOrder()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var onTop1 = new Window(system) { Title = "OnTop1", AlwaysOnTop = true };
		var onTop2 = new Window(system) { Title = "OnTop2", AlwaysOnTop = true };
		var normal = new Window(system) { Title = "Normal" };

		system.WindowStateService.AddWindow(onTop1);
		system.WindowStateService.AddWindow(onTop2);
		system.WindowStateService.AddWindow(normal);

		// onTop2 was added after onTop1, should have higher ZIndex
		Assert.True(onTop2.ZIndex > onTop1.ZIndex);
		// Both above normal
		Assert.True(onTop1.ZIndex > normal.ZIndex);
	}

	[Fact]
	public void AlwaysOnTop_StaysAbove_AfterMultipleActivations()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var normal1 = new Window(system) { Title = "Normal1" };
		var normal2 = new Window(system) { Title = "Normal2" };
		var normal3 = new Window(system) { Title = "Normal3" };
		var onTop = new Window(system) { Title = "OnTop", AlwaysOnTop = true };

		system.WindowStateService.AddWindow(normal1);
		system.WindowStateService.AddWindow(onTop);
		system.WindowStateService.AddWindow(normal2);
		system.WindowStateService.AddWindow(normal3);

		// Cycle through activating normal windows
		system.WindowStateService.ActivateWindow(normal1);
		system.WindowStateService.ActivateWindow(normal2);
		system.WindowStateService.ActivateWindow(normal3);
		system.WindowStateService.ActivateWindow(normal1);

		Assert.True(onTop.ZIndex > normal1.ZIndex);
		Assert.True(onTop.ZIndex > normal2.ZIndex);
		Assert.True(onTop.ZIndex > normal3.ZIndex);
	}

	[Fact]
	public void AlwaysOnTop_Invariant_AllOnTopAboveAllNormal()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var normals = Enumerable.Range(1, 5)
			.Select(i => new Window(system) { Title = $"Normal{i}" }).ToList();
		var onTops = Enumerable.Range(1, 3)
			.Select(i => new Window(system) { Title = $"OnTop{i}", AlwaysOnTop = true }).ToList();

		// Interleave registration
		system.WindowStateService.AddWindow(normals[0]);
		system.WindowStateService.AddWindow(onTops[0]);
		system.WindowStateService.AddWindow(normals[1]);
		system.WindowStateService.AddWindow(normals[2]);
		system.WindowStateService.AddWindow(onTops[1]);
		system.WindowStateService.AddWindow(normals[3]);
		system.WindowStateService.AddWindow(onTops[2]);
		system.WindowStateService.AddWindow(normals[4]);

		// Activate various normal windows
		system.WindowStateService.ActivateWindow(normals[2]);
		system.WindowStateService.ActivateWindow(normals[0]);
		system.WindowStateService.ActivateWindow(normals[4]);

		int maxNormalZ = normals.Max(w => w.ZIndex);
		int minOnTopZ = onTops.Min(w => w.ZIndex);

		Assert.True(minOnTopZ > maxNormalZ,
			$"Min AlwaysOnTop ZIndex ({minOnTopZ}) should be above max normal ZIndex ({maxNormalZ})");
	}

	[Fact]
	public void AlwaysOnTop_SendToBack_NormalWindow_OnTopStaysAbove()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var normal1 = new Window(system) { Title = "Normal1" };
		var normal2 = new Window(system) { Title = "Normal2" };
		var onTop = new Window(system) { Title = "OnTop", AlwaysOnTop = true };

		system.WindowStateService.AddWindow(normal1);
		system.WindowStateService.AddWindow(onTop);
		system.WindowStateService.AddWindow(normal2);

		system.WindowStateService.SendToBack(normal1);

		Assert.True(onTop.ZIndex > normal1.ZIndex);
		Assert.True(onTop.ZIndex > normal2.ZIndex);
	}

	[Fact]
	public void AlwaysOnTop_DefaultIsFalse()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Title = "Default" };

		Assert.False(window.AlwaysOnTop);
	}

	[Fact]
	public void AlwaysOnTop_ZIndex_Invariant_SurvivesRenderCycle()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();

		var normal = new Window(system)
		{
			Title = "Normal", Left = 5, Top = 5, Width = 30, Height = 10
		};
		var onTop = new Window(system)
		{
			Title = "OnTop", Left = 5, Top = 5, Width = 30, Height = 10,
			AlwaysOnTop = true
		};

		system.WindowStateService.AddWindow(normal);
		system.WindowStateService.AddWindow(onTop);

		// Activate normal window and render
		system.WindowStateService.ActivateWindow(normal);
		system.Render.UpdateDisplay();

		// ZIndex invariant should hold after render cycle
		Assert.True(onTop.ZIndex > normal.ZIndex,
			$"AlwaysOnTop ZIndex ({onTop.ZIndex}) should be above normal ZIndex ({normal.ZIndex}) after render");

		// OnTop should still be first in z-order list
		var zOrder = system.WindowStateService.GetWindowsByZOrder();
		Assert.Same(onTop, zOrder.First());
	}
}
