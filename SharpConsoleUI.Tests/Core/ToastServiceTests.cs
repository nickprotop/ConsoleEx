using System;
using System.Collections.Generic;
using System.Linq;
using SharpConsoleUI.Core;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Core;

internal sealed class ManualToastScheduler : IToastScheduler
{
	public List<(int Delay, Action Callback)> Scheduled { get; } = new();
	public void Schedule(int delayMs, Action callback) => Scheduled.Add((delayMs, callback));
	public void FireAll() { foreach (var (_, cb) in Scheduled.ToArray()) cb(); }
}

public class ToastStateTests
{
	[Fact]
	public void Empty_HasNoToasts()
	{
		var s = ToastState.Empty;
		Assert.False(s.HasToasts);
		Assert.Equal(0, s.ActiveCount);
		Assert.Empty(s.ActiveToasts);
	}

	[Fact]
	public void WithToast_ReportsCount()
	{
		var t = new ToastInfo("id1", "hello", NotificationSeverity.Info, ToastPosition.BottomRight, false);
		var s = new ToastState(new[] { t }, TotalShown: 1, TotalDismissed: 0);
		Assert.True(s.HasToasts);
		Assert.Equal(1, s.ActiveCount);
	}
}

public class ToastSchedulerTests
{
	[Fact]
	public void TaskScheduler_RunsCallback()
	{
		var sched = new TaskToastScheduler(a => a());  // synchronous post (test-only)
		var done = new System.Threading.ManualResetEventSlim(false);
		sched.Schedule(1, () => done.Set());
		// Condition-based wait — Task.Delay's continuation runs on the thread pool and may be starved well
		// past the requested 1ms when the pool is saturated (e.g. a full parallel test run), so wait with a
		// generous timeout rather than a fixed sleep. 30s tolerates heavy pool pressure while still failing
		// fast if the callback genuinely never runs.
		bool ran = done.Wait(System.TimeSpan.FromSeconds(30));
		Assert.True(ran);
	}
}

public class ToastServiceTests
{
	private static ToastService NewService(out ManualToastScheduler sched)
	{
		var ws = TestWindowSystemBuilder.CreateTestSystem();
		sched = new ManualToastScheduler();
		return new ToastService(ws, scheduler: sched);
	}

	[Fact]
	public void Show_AddsToast_FiresEvents_ReturnsId()
	{
		var svc = NewService(out _);
		ToastEventArgs? shown = null; ToastState? stateArg = null;
		svc.ToastShown += (_, e) => shown = e;
		svc.StateChanged += (_, s) => stateArg = s;
		var id = svc.Show("hi", NotificationSeverity.Success);
		Assert.False(string.IsNullOrEmpty(id));
		Assert.True(svc.HasToasts);
		Assert.Equal(1, svc.ActiveCount);
		Assert.NotNull(shown);
		Assert.Equal(id, shown!.Toast.Id);
		Assert.NotNull(stateArg);
		Assert.Equal(1, svc.CurrentState.TotalShown);
	}

	[Fact]
	public void Dismiss_RemovesToast_FiresEvent()
	{
		var svc = NewService(out _);
		var id = svc.Show("hi", NotificationSeverity.Info);
		ToastEventArgs? dismissed = null;
		svc.ToastDismissed += (_, e) => dismissed = e;
		Assert.True(svc.Dismiss(id));
		Assert.False(svc.HasToasts);
		Assert.NotNull(dismissed);
		Assert.Equal(1, svc.CurrentState.TotalDismissed);
		Assert.False(svc.Dismiss("nope"));
	}

	[Fact]
	public void DismissAll_ClearsAndFires()
	{
		var svc = NewService(out _);
		svc.Show("a", NotificationSeverity.Info);
		svc.Show("b", NotificationSeverity.Warning);
		bool allFired = false;
		svc.AllToastsDismissed += (_, _) => allFired = true;
		svc.DismissAll();
		Assert.False(svc.HasToasts);
		Assert.True(allFired);
	}

	[Fact]
	public void NonStickyWithTimeout_SchedulesAutoDismiss()
	{
		var svc = NewService(out var sched);
		svc.Show("x", NotificationSeverity.Info, new ToastOptions(Timeout: 1234));
		Assert.Single(sched.Scheduled);
		Assert.Equal(1234, sched.Scheduled[0].Delay);
	}

	[Fact]
	public void Sticky_DoesNotSchedule()
	{
		var svc = NewService(out var sched);
		svc.Show("x", NotificationSeverity.Danger, new ToastOptions(Sticky: true));
		Assert.Empty(sched.Scheduled);
	}

	[Fact]
	public void AutoDismiss_FiringScheduler_RemovesToast()
	{
		var svc = NewService(out var sched);
		var id = svc.Show("x", NotificationSeverity.Info, new ToastOptions(Timeout: 10));
		Assert.True(svc.HasToasts);
		sched.FireAll();
		Assert.False(svc.HasToasts);
	}

	[Fact]
	public void DefaultPosition_Setter_RaisesPropertyChanged_OnceForChange()
	{
		var svc = NewService(out _);
		var names = new List<string?>();
		svc.PropertyChanged += (_, e) => names.Add(e.PropertyName);
		svc.DefaultPosition = ToastPosition.TopRight;
		svc.DefaultPosition = ToastPosition.TopRight;
		Assert.Equal(new[] { nameof(ToastService.DefaultPosition) }, names.Where(n => n == nameof(ToastService.DefaultPosition)).ToArray());
	}

	[Fact]
	public void Show_RaisesPropertyChanged_ForDerivedProps()
	{
		var svc = NewService(out _);
		var names = new List<string?>();
		svc.PropertyChanged += (_, e) => names.Add(e.PropertyName);
		svc.Show("x", NotificationSeverity.Info, new ToastOptions(Sticky: true));
		Assert.Contains(nameof(ToastService.HasToasts), names);
		Assert.Contains(nameof(ToastService.ActiveCount), names);
	}

	[Fact]
	public void ActiveToasts_IsObservable_RaisesCollectionChanged()
	{
		var svc = NewService(out _);
		var actions = new List<System.Collections.Specialized.NotifyCollectionChangedAction>();
		svc.ActiveToasts.CollectionChanged += (_, e) => actions.Add(e.Action);
		var id = svc.Show("x", NotificationSeverity.Info, new ToastOptions(Sticky: true));
		svc.Dismiss(id);
		Assert.Contains(System.Collections.Specialized.NotifyCollectionChangedAction.Add, actions);
		Assert.Contains(System.Collections.Specialized.NotifyCollectionChangedAction.Remove, actions);
	}
}
