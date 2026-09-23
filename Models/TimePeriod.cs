namespace DeskLofi.Models;

public enum TimePeriod { Morning, Day, Evening, Night }

public sealed record TimePeriodDefinition(TimePeriod Period, int StartHour, int EndHour)
{
    public bool Contains(TimeOnly time) => StartHour < EndHour
        ? time.Hour >= StartHour && time.Hour < EndHour
        : time.Hour >= StartHour || time.Hour < EndHour;
}
