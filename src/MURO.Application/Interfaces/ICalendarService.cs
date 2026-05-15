using MURO.Application.DTOs;
using MURO.Application.DTOs.Calendar;

namespace MURO.Application.Interfaces;

public interface ICalendarService
{
    Task<List<CalendarEventDto>> GetEventsAsync(Guid tenantId, DateTime from, DateTime to, Guid? groupId, Guid? instructorId = null);
    Task<CalendarEventDto> GetEventByIdAsync(Guid tenantId, Guid eventId);
    Task<CalendarEventDto> CreateEventAsync(Guid tenantId, CreateCalendarEventRequest request, Guid? instructorId = null);
    Task<CalendarEventDto> UpdateEventAsync(Guid tenantId, Guid eventId, UpdateCalendarEventRequest request, Guid? instructorId = null);
    Task DeleteEventAsync(Guid tenantId, Guid eventId, Guid? instructorId = null);
}
