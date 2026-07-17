namespace BrokerApp.Api.Features.Dashboard;

public interface ISystemClock
{
    DateOnly Today { get; }
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : ISystemClock
{
    public DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
