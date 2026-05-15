using MURO.Application.DTOs.Calendar;

namespace MURO.Application.Interfaces;

public interface IStudentService
{
    /// <summary>
    /// Gets the upcoming and past live sessions for the student within a date range as calendar events.
    /// </summary>
    Task<List<CalendarEventDto>> GetCalendarEventsAsync(Guid tenantId, Guid userId, DateTime from, DateTime to);
}
