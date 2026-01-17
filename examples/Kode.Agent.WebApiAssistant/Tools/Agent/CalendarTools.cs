using Kode.Agent.Sdk.Core.Abstractions;
using Kode.Agent.Sdk.Tools;
using Kode.Agent.WebApiAssistant.Tools.Calendar;

namespace Kode.Agent.WebApiAssistant.Tools.Agent;

/// <summary>
/// Tool for listing calendar events.
/// </summary>
[Tool("calendar_list")]
public sealed class CalendarListTool : ToolBase<CalendarListArgs>
{
    private readonly ICalendarTool _calendarTool;

    public CalendarListTool(ICalendarTool calendarTool)
    {
        _calendarTool = calendarTool;
    }

    public override string Name => "calendar_list";

    public override string Description =>
        "List calendar events within a date range. " +
        "Returns events with title, location, start time, and end time.";

    public override object InputSchema => JsonSchemaBuilder.BuildSchema<CalendarListArgs>();

    public override ToolAttributes Attributes => new()
    {
        ReadOnly = true,
        NoEffect = true
    };

    protected override async Task<ToolResult> ExecuteAsync(
        CalendarListArgs args,
        ToolContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var startDate = DateTime.Parse(args.StartDate);
            var endDate = DateTime.Parse(args.EndDate);

            var events = await _calendarTool.ListEventsAsync(
                startDate,
                endDate,
                args.Keyword);

            return ToolResult.Ok(new
            {
                events = events.Select(e => new
                {
                    eventId = e.EventId,
                    title = e.Title,
                    location = e.Location,
                    notes = e.Notes,
                    startDate = e.StartDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    endDate = e.EndDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    isAllDay = e.IsAllDay
                }),
                count = events.Count
            });
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"Failed to list calendar events: {ex.Message}");
        }
    }
}

/// <summary>
/// Arguments for calendar_list tool.
/// </summary>
[GenerateToolSchema]
public class CalendarListArgs
{
    [ToolParameter(Description = "Start date (format: yyyy-MM-dd)")]
    public required string StartDate { get; init; }

    [ToolParameter(Description = "End date (format: yyyy-MM-dd)")]
    public required string EndDate { get; init; }

    [ToolParameter(Description = "Optional keyword to filter events", Required = false)]
    public string? Keyword { get; init; }
}

/// <summary>
/// Tool for creating calendar events.
/// </summary>
[Tool("calendar_create")]
public sealed class CalendarCreateTool : ToolBase<CalendarCreateArgs>
{
    private readonly ICalendarTool _calendarTool;

    public CalendarCreateTool(ICalendarTool calendarTool)
    {
        _calendarTool = calendarTool;
    }

    public override string Name => "calendar_create";

    public override string Description =>
        "Create a new calendar event. " +
        "Returns the created event with its ID.";

    public override object InputSchema => JsonSchemaBuilder.BuildSchema<CalendarCreateArgs>();

    public override ToolAttributes Attributes => new()
    {
        ReadOnly = false,
        RequiresApproval = false
    };

    protected override async Task<ToolResult> ExecuteAsync(
        CalendarCreateArgs args,
        ToolContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var startDate = DateTime.Parse(args.StartDate);
            var endDate = DateTime.Parse(args.EndDate);

            var newEvent = await _calendarTool.CreateEventAsync(
                args.Title,
                startDate,
                endDate,
                args.Location,
                args.Notes,
                args.AllDay ?? false);

            return ToolResult.Ok(new
            {
                eventId = newEvent.EventId,
                title = newEvent.Title,
                startDate = newEvent.StartDate.ToString("yyyy-MM-dd HH:mm:ss"),
                endDate = newEvent.EndDate.ToString("yyyy-MM-dd HH:mm:ss"),
                message = "Calendar event created successfully"
            });
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"Failed to create calendar event: {ex.Message}");
        }
    }
}

/// <summary>
/// Arguments for calendar_create tool.
/// </summary>
[GenerateToolSchema]
public class CalendarCreateArgs
{
    [ToolParameter(Description = "Event title")]
    public required string Title { get; init; }

    [ToolParameter(Description = "Start date and time (format: yyyy-MM-dd HH:mm)")]
    public required string StartDate { get; init; }

    [ToolParameter(Description = "End date and time (format: yyyy-MM-dd HH:mm)")]
    public required string EndDate { get; init; }

    [ToolParameter(Description = "Event location", Required = false)]
    public string? Location { get; init; }

    [ToolParameter(Description = "Event notes/description", Required = false)]
    public string? Notes { get; init; }

    [ToolParameter(Description = "Is all-day event", Required = false)]
    public bool? AllDay { get; init; }
}

/// <summary>
/// Tool for updating calendar events.
/// </summary>
[Tool("calendar_update")]
public sealed class CalendarUpdateTool : ToolBase<CalendarUpdateArgs>
{
    private readonly ICalendarTool _calendarTool;

    public CalendarUpdateTool(ICalendarTool calendarTool)
    {
        _calendarTool = calendarTool;
    }

    public override string Name => "calendar_update";

    public override string Description =>
        "Update an existing calendar event. " +
        "Only provide the fields that need to be changed.";

    public override object InputSchema => JsonSchemaBuilder.BuildSchema<CalendarUpdateArgs>();

    public override ToolAttributes Attributes => new()
    {
        ReadOnly = false,
        RequiresApproval = false
    };

    protected override async Task<ToolResult> ExecuteAsync(
        CalendarUpdateArgs args,
        ToolContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var updatedEvent = await _calendarTool.UpdateEventAsync(
                args.EventId,
                args.Title,
                !string.IsNullOrEmpty(args.StartDate) ? DateTime.Parse(args.StartDate) : null,
                !string.IsNullOrEmpty(args.EndDate) ? DateTime.Parse(args.EndDate) : null);

            if (updatedEvent == null)
            {
                return ToolResult.Fail($"Event not found: {args.EventId}");
            }

            return ToolResult.Ok(new
            {
                eventId = updatedEvent.EventId,
                title = updatedEvent.Title,
                startDate = updatedEvent.StartDate.ToString("yyyy-MM-dd HH:mm:ss"),
                endDate = updatedEvent.EndDate.ToString("yyyy-MM-dd HH:mm:ss"),
                message = "Calendar event updated successfully"
            });
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"Failed to update calendar event: {ex.Message}");
        }
    }
}

/// <summary>
/// Arguments for calendar_update tool.
/// </summary>
[GenerateToolSchema]
public class CalendarUpdateArgs
{
    [ToolParameter(Description = "Event ID to update")]
    public required string EventId { get; init; }

    [ToolParameter(Description = "New event title", Required = false)]
    public string? Title { get; init; }

    [ToolParameter(Description = "New start date and time (format: yyyy-MM-dd HH:mm)", Required = false)]
    public string? StartDate { get; init; }

    [ToolParameter(Description = "New end date and time (format: yyyy-MM-dd HH:mm)", Required = false)]
    public string? EndDate { get; init; }
}
