using SharpConsoleUI.Core;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SharpConsoleUI.Tests.Core;

public class InputStateServiceWakeTests
{
    [Fact]
    public void EnqueueKey_CallsWakeCallback()
    {
        // Arrange
        using var service = new InputStateService();
        int callCount = 0;
        service.WakeCallback = () => callCount++;

        // Act
        service.EnqueueKey(new ConsoleKeyInfo('a', ConsoleKey.A, false, false, false));

        // Assert
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void EnqueueKey_WithoutWakeCallback_DoesNotThrow()
    {
        // Arrange
        using var service = new InputStateService();
        // No WakeCallback set

        // Act & Assert — should not throw
        service.EnqueueKey(new ConsoleKeyInfo('a', ConsoleKey.A, false, false, false));
    }

    [Fact]
    public void EnqueueKey_MultipleKeys_CallsWakeCallbackEachTime()
    {
        // Arrange
        using var service = new InputStateService();
        int callCount = 0;
        service.WakeCallback = () => callCount++;

        // Act
        service.EnqueueKey(new ConsoleKeyInfo('a', ConsoleKey.A, false, false, false));
        service.EnqueueKey(new ConsoleKeyInfo('b', ConsoleKey.B, false, false, false));
        service.EnqueueKey(new ConsoleKeyInfo('c', ConsoleKey.C, false, false, false));

        // Assert
        Assert.Equal(3, callCount);
    }

    [Fact]
    public void WakeSignal_IsSignaledByEnqueueOnUIThread()
    {
        // Arrange
        using var wakeSignal = new ManualResetEventSlim(false);
        Action wake = () => wakeSignal.Set();

        // Act
        wake();

        // Assert
        Assert.True(wakeSignal.IsSet);
    }

    [Fact]
    public void WakeSignal_WaitReturnsImmediatelyWhenSignaled()
    {
        // Arrange
        using var wakeSignal = new ManualResetEventSlim(false);

        // Act — signal from another thread, then wait
        Task.Run(() => wakeSignal.Set());
        bool signaled = wakeSignal.Wait(TimeSpan.FromSeconds(1));

        // Assert
        Assert.True(signaled);
    }

    [Fact]
    public void WakeSignal_WaitTimesOutWhenNotSignaled()
    {
        // Arrange
        using var wakeSignal = new ManualResetEventSlim(false);

        // Act
        bool signaled = wakeSignal.Wait(TimeSpan.FromMilliseconds(10));

        // Assert
        Assert.False(signaled);
    }
}
