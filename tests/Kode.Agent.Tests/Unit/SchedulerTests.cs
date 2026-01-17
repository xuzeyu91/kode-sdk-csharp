using Kode.Agent.Sdk.Core.Scheduling;
using Xunit;

namespace Kode.Agent.Tests.Unit;

public class SchedulerTests : IDisposable
{
    private readonly Scheduler _scheduler;

    public SchedulerTests()
    {
        _scheduler = new Scheduler();
    }

    public void Dispose()
    {
        _scheduler.Dispose();
    }

    [Fact]
    public void EverySteps_ReturnsValidHandle()
    {
        // Act
        var handle = _scheduler.EverySteps(5, ctx => Task.CompletedTask);
        
        // Assert
        Assert.NotNull(handle.Id);
    }

    [Fact]
    public void EverySteps_ThrowsForNonPositiveInterval()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            _scheduler.EverySteps(0, ctx => Task.CompletedTask));
        Assert.Throws<ArgumentException>(() => 
            _scheduler.EverySteps(-1, ctx => Task.CompletedTask));
    }

    [Fact]
    public async Task NotifyStep_ExecutesScheduledTasks()
    {
        // Arrange
        var executionCount = 0;
        _scheduler.EverySteps(2, ctx =>
        {
            Interlocked.Increment(ref executionCount);
            return Task.CompletedTask;
        });
        
        // Act
        _scheduler.NotifyStep(1);
        _scheduler.NotifyStep(2);
        _scheduler.NotifyStep(3);
        _scheduler.NotifyStep(4);
        
        // Wait for async callbacks
        await Task.Delay(100);
        
        // Assert
        Assert.Equal(2, executionCount); // Triggered at step 2 and 4
    }

    [Fact]
    public void OnStep_RegistersListener()
    {
        // Act
        var unsubscribe = _scheduler.OnStep(ctx => Task.CompletedTask);
        
        // Assert
        Assert.NotNull(unsubscribe);
    }

    [Fact]
    public async Task OnStep_ExecutesOnEachNotify()
    {
        // Arrange
        var executionCount = 0;
        _scheduler.OnStep(ctx =>
        {
            Interlocked.Increment(ref executionCount);
            return Task.CompletedTask;
        });
        
        // Act
        _scheduler.NotifyStep(1);
        _scheduler.NotifyStep(2);
        _scheduler.NotifyStep(3);
        
        // Wait for async callbacks
        await Task.Delay(100);
        
        // Assert
        Assert.Equal(3, executionCount);
    }

    [Fact]
    public async Task OnStep_Unsubscribe_StopsExecution()
    {
        // Arrange
        var executionCount = 0;
        var unsubscribe = _scheduler.OnStep(ctx =>
        {
            Interlocked.Increment(ref executionCount);
            return Task.CompletedTask;
        });
        
        // Act
        _scheduler.NotifyStep(1);
        await Task.Delay(50);
        unsubscribe();
        _scheduler.NotifyStep(2);
        _scheduler.NotifyStep(3);
        await Task.Delay(50);
        
        // Assert
        Assert.Equal(1, executionCount);
    }

    [Fact]
    public void Cancel_RemovesTask()
    {
        // Arrange
        var handle = _scheduler.EverySteps(5, ctx => Task.CompletedTask);
        
        // Act
        var result = _scheduler.Cancel(handle);
        
        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Cancel_ReturnsFalseForNonExistent()
    {
        // Act
        var result = _scheduler.Cancel(new SchedulerHandle("non-existent"));
        
        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Clear_RemovesAllTasks()
    {
        // Arrange
        _scheduler.EverySteps(5, ctx => Task.CompletedTask);
        _scheduler.EverySteps(10, ctx => Task.CompletedTask);
        _scheduler.OnStep(ctx => Task.CompletedTask);
        
        // Act
        _scheduler.Clear();
        
        // Verify no exception when using after clear
    }

    [Fact]
    public void SyncOnStep_Works()
    {
        // Arrange & Act
        var unsubscribe = _scheduler.OnStep((StepContext ctx) => { });
        
        // Assert
        Assert.NotNull(unsubscribe);
    }

    [Fact]
    public async Task EnqueueAsync_WaitsForCompletion()
    {
        // Arrange
        var executed = false;
        
        // Act
        await _scheduler.EnqueueAsync(() =>
        {
            executed = true;
            return Task.CompletedTask;
        });
        
        // Assert
        Assert.True(executed);
    }

    [Fact]
    public async Task Enqueue_RunsTasksInOrder()
    {
        // Arrange
        var order = new List<int>();
        
        // Act
        _scheduler.Enqueue(async () =>
        {
            await Task.Delay(10);
            order.Add(1);
        });
        _scheduler.Enqueue(() =>
        {
            order.Add(2);
            return Task.CompletedTask;
        });
        
        await Task.Delay(100);
        
        // Assert
        Assert.Equal([1, 2], order);
    }
}
