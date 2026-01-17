namespace Kode.Agent.WebApiAssistant.Tools.Calendar;

/// <summary>
/// 日历事件
/// </summary>
public class CalendarEvent
{
    public string EventId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? Notes { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsAllDay { get; set; }
}

/// <summary>
/// 日历工具接口
/// </summary>
public interface ICalendarTool
{
    /// <summary>
    /// 列出日历事件
    /// </summary>
    Task<IReadOnlyList<CalendarEvent>> ListEventsAsync(
        DateTime startDate,
        DateTime endDate,
        string? keyword = null);

    /// <summary>
    /// 创建日历事件
    /// </summary>
    Task<CalendarEvent> CreateEventAsync(
        string title,
        DateTime startDate,
        DateTime endDate,
        string? location = null,
        string? notes = null,
        bool allDay = false);

    /// <summary>
    /// 更新日历事件
    /// </summary>
    Task<CalendarEvent?> UpdateEventAsync(
        string eventId,
        string? title = null,
        DateTime? startDate = null,
        DateTime? endDate = null);

    /// <summary>
    /// 删除日历事件
    /// </summary>
    Task<bool> DeleteEventAsync(string eventId);
}
