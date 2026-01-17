using Microsoft.Extensions.Logging;

namespace Kode.Agent.Sdk.Core.Todo;

/// <summary>
/// Todo manager (aligned with TS src/core/agent/todo-manager.ts).
/// Handles persistence via TodoService and emits monitor events + reminders.
/// </summary>
public sealed class TodoManager
{
    private readonly TodoService? _service;
    private readonly Kode.Agent.Sdk.Core.Templates.TodoConfig? _config;
    private readonly IEventBus _events;
    private readonly Func<string, string, CancellationToken, Task> _remind;
    private readonly ILogger<TodoManager>? _logger;
    private int _stepsSinceReminder;

    public TodoManager(
        TodoService? service,
        Kode.Agent.Sdk.Core.Templates.TodoConfig? config,
        IEventBus events,
        Func<string, string, CancellationToken, Task> remind,
        ILogger<TodoManager>? logger = null)
    {
        _service = service;
        _config = config;
        _events = events;
        _remind = remind;
        _logger = logger;
    }

    public bool Enabled => _service != null && (_config?.Enabled ?? false);

    public IReadOnlyList<TodoItem> List() => _service?.List() ?? [];

    public async Task SetTodosAsync(IEnumerable<TodoItem> todos, CancellationToken cancellationToken = default)
    {
        if (_service == null) throw new InvalidOperationException("Todo service not enabled for this agent");
        var prev = _service.List();
        var inputs = todos.Select(ToInput).ToList();
        await _service.SetTodosAsync(inputs, cancellationToken);
        PublishChange(prev, _service.List(), cancellationToken);
    }

    public void HandleStartup(CancellationToken cancellationToken = default)
    {
        if (!Enabled) return;
        if (_config?.ReminderOnStart != true) return;
        var todos = List().Where(t => t.Status != TodoStatus.Completed).ToList();
        if (todos.Count == 0)
        {
            _ = SendEmptyReminderAsync(cancellationToken);
        }
        else
        {
            _ = SendReminderAsync(todos, "startup", cancellationToken);
        }
    }

    public void OnStep(CancellationToken cancellationToken = default)
    {
        if (!Enabled) return;
        var interval = _config?.RemindIntervalSteps;
        if (interval is null || interval <= 0) return;
        _stepsSinceReminder += 1;
        if (_stepsSinceReminder < interval) return;

        var todos = List().Where(t => t.Status != TodoStatus.Completed).ToList();
        if (todos.Count == 0) return;

        _ = SendReminderAsync(todos, "interval", cancellationToken);
    }

    private void PublishChange(
        IReadOnlyList<TodoItem> previous,
        IReadOnlyList<TodoItem> current,
        CancellationToken cancellationToken)
    {
        _stepsSinceReminder = 0;
        _events.EmitMonitor(new TodoChangedEvent
        {
            Type = "todo_changed",
            Previous = previous,
            Current = current
        });

        if (current.Count == 0)
        {
            _ = SendEmptyReminderAsync(cancellationToken);
        }
    }

    private async Task SendReminderAsync(IReadOnlyList<TodoItem> todos, string reason, CancellationToken cancellationToken)
    {
        _stepsSinceReminder = 0;
        _events.EmitMonitor(new TodoReminderEvent
        {
            Type = "todo_reminder",
            Todos = todos,
            Reason = reason
        });

        var text = FormatTodoReminder(todos);
        await _remind(text, "todo", cancellationToken);
    }

    private async Task SendEmptyReminderAsync(CancellationToken cancellationToken)
    {
        await _remind("当前 todo 列表为空，如需跟踪任务请使用 todo_write 建立清单。", "todo", cancellationToken);
    }

    private static string FormatTodoReminder(IReadOnlyList<TodoItem> todos)
    {
        var lines = todos
            .Take(10)
            .Select((todo, idx) => $"{idx + 1}. [{todo.Status}] {todo.Title}")
            .ToList();
        var more = todos.Count > 10 ? $"\n… 还有 {todos.Count - 10} 项" : "";
        return $"Todo 列表仍有未完成项：\n{string.Join("\n", lines)}{more}\n请结合 todo_write 及时更新进度，不要向用户直接提及本提醒。";
    }

    private static TodoInput ToInput(TodoItem item)
    {
        return new TodoInput
        {
            Id = item.Id,
            Title = item.Title,
            Status = item.Status,
            Assignee = item.Assignee,
            Notes = item.Notes
        };
    }
}
