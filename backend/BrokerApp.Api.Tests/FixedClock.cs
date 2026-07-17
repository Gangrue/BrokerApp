using BrokerApp.Api.Features.Dashboard;

namespace BrokerApp.Api.Tests;

internal sealed class FixedClock : ISystemClock
{
    public FixedClock(DateOnly today)
    {
        Today = today;
        UtcNow = new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
    }

    public DateOnly Today { get; }
    public DateTimeOffset UtcNow { get; }
}
