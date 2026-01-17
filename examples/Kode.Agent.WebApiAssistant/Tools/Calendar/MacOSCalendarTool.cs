namespace Kode.Agent.WebApiAssistant.Tools.Calendar;

/// <summary>
/// macOS 日历工具实现（使用 AppleScript）
/// </summary>
public class MacOSCalendarTool : ICalendarTool
{
    private readonly ILogger<MacOSCalendarTool> _logger;

    public MacOSCalendarTool(ILogger<MacOSCalendarTool> logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<CalendarEvent>> ListEventsAsync(
        DateTime startDate,
        DateTime endDate,
        string? keyword = null)
    {
        // TODO: 实现 AppleScript 调用
        _logger.LogInformation("Listing events from {Start} to {End} with keyword: {Keyword}",
            startDate, endDate, keyword);

        return Task.FromResult<IReadOnlyList<CalendarEvent>>(Array.Empty<CalendarEvent>());
    }

    public Task<CalendarEvent> CreateEventAsync(
        string title,
        DateTime startDate,
        DateTime endDate,
        string? location = null,
        string? notes = null,
        bool allDay = false)
    {
        // TODO: 实现 AppleScript 调用
        var @event = new CalendarEvent
        {
            EventId = Guid.NewGuid().ToString("N"),
            Title = title,
            StartDate = startDate,
            EndDate = endDate,
            Location = location,
            Notes = notes,
            IsAllDay = allDay
        };

        _logger.LogInformation("Created event: {Title} at {Start}", title, startDate);
        return Task.FromResult(@event);
    }

    public Task<CalendarEvent?> UpdateEventAsync(
        string eventId,
        string? title = null,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        // TODO: 实现 AppleScript 调用
        _logger.LogInformation("Updating event: {EventId}", eventId);
        return Task.FromResult<CalendarEvent?>(null);
    }

    public Task<bool> DeleteEventAsync(string eventId)
    {
        // TODO: 实现 AppleScript 调用
        _logger.LogInformation("Deleting event: {EventId}", eventId);
        return Task.FromResult(true);
    }
}
