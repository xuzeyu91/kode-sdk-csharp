using Kode.Agent.Sdk.Core.Scheduling;
using Xunit;

namespace Kode.Agent.Tests.Unit;

public class TimeBridgeTests : IAsyncLifetime
{
    private Scheduler _scheduler = null!;
    private TimeBridge _timeBridge = null!;

    public Task InitializeAsync()
    {
        _scheduler = new Scheduler();
        _timeBridge = new TimeBridge(new TimeBridgeOptions
        {
            Scheduler = _scheduler
        });
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _timeBridge.DisposeAsync();
        _scheduler.Dispose();
    }

    [Fact]
    public void EveryMinutes_ReturnsTimerId()
    {
        // Act
        var id = _timeBridge.EveryMinutes(1, () => Task.CompletedTask);
        
        // Assert
        Assert.NotNull(id);
        Assert.Contains("minutes", id);
    }

    [Fact]
    public void EveryMinutes_ThrowsForNonPositive()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            _timeBridge.EveryMinutes(0, () => Task.CompletedTask));
        Assert.Throws<ArgumentException>(() => 
            _timeBridge.EveryMinutes(-1, () => Task.CompletedTask));
    }

    [Fact]
    public void Cron_ReturnsTimerId()
    {
        // Act
        var id = _timeBridge.Cron("30 9 * * *", () => Task.CompletedTask);
        
        // Assert
        Assert.NotNull(id);
        Assert.Contains("cron", id);
    }

    [Fact]
    public void Cron_ThrowsForInvalidExpression()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            _timeBridge.Cron("invalid", () => Task.CompletedTask));
        Assert.Throws<ArgumentException>(() => 
            _timeBridge.Cron("* *", () => Task.CompletedTask));
    }

    [Fact]
    public void After_ReturnsTimerId()
    {
        // Act
        var id = _timeBridge.After(TimeSpan.FromSeconds(30), () => Task.CompletedTask);
        
        // Assert
        Assert.NotNull(id);
        Assert.Contains("after", id);
    }

    [Fact]
    public void After_ThrowsForNegativeDelay()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            _timeBridge.After(TimeSpan.FromSeconds(-1), () => Task.CompletedTask));
    }

    [Fact]
    public void Cancel_ReturnsTrueForExisting()
    {
        // Arrange
        var id = _timeBridge.EveryMinutes(5, () => Task.CompletedTask);
        
        // Act
        var result = _timeBridge.Cancel(id);
        
        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Cancel_ReturnsFalseForNonExisting()
    {
        // Act
        var result = _timeBridge.Cancel("non-existing");
        
        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ActiveTimerCount_ReflectsState()
    {
        // Assert initial
        Assert.Equal(0, _timeBridge.ActiveTimerCount);
        
        // Add timers
        _timeBridge.EveryMinutes(1, () => Task.CompletedTask);
        _timeBridge.EveryMinutes(5, () => Task.CompletedTask);
        Assert.Equal(2, _timeBridge.ActiveTimerCount);
        
        // Cancel all
        _timeBridge.CancelAll();
        Assert.Equal(0, _timeBridge.ActiveTimerCount);
    }

    [Fact]
    public void CancelAll_RemovesAllTimers()
    {
        // Arrange
        _timeBridge.EveryMinutes(1, () => Task.CompletedTask);
        _timeBridge.EveryMinutes(5, () => Task.CompletedTask);
        _timeBridge.After(TimeSpan.FromMinutes(10), () => Task.CompletedTask);
        
        // Act
        _timeBridge.CancelAll();
        
        // Assert
        Assert.Equal(0, _timeBridge.ActiveTimerCount);
    }
}
