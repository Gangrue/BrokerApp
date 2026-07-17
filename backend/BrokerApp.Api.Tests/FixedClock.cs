using BrokerApp.Api.Features.Dashboard;

namespace BrokerApp.Api.Tests;

internal sealed class FixedClock : ISystemClock
{
    public FixedClock(DateOnly today)
    {
        Today = today;
    }

    public DateOnly Today { get; }
}
