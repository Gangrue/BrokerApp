using BrokerApp.Api.Data;
using BrokerApp.Api.Domain;
using BrokerApp.Api.Features.Auth;
using BrokerApp.Api.Features.Dashboard;

namespace BrokerApp.Api.Features.Audit;

public interface IAuditWriter
{
    void Record(
        string entityType,
        string entityId,
        string operation,
        string changedFields,
        Guid? actorUserId = null,
        Guid? correlationId = null);
}

public sealed class AuditWriter : IAuditWriter
{
    private readonly BrokerAppDbContext _dbContext;
    private readonly ISystemClock _clock;
    private readonly ICurrentUserContext _currentUser;

    public AuditWriter(BrokerAppDbContext dbContext, ISystemClock clock, ICurrentUserContext currentUser)
    {
        _dbContext = dbContext;
        _clock = clock;
        _currentUser = currentUser;
    }

    public void Record(
        string entityType,
        string entityId,
        string operation,
        string changedFields,
        Guid? actorUserId = null,
        Guid? correlationId = null)
    {
        _dbContext.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganizationId = _currentUser.OrganizationId,
            ActorUserId = actorUserId ?? _currentUser.UserId,
            EntityType = entityType,
            EntityId = entityId,
            Operation = operation,
            ChangedFields = changedFields,
            OccurredAtUtc = _clock.UtcNow,
            CorrelationId = correlationId ?? Guid.NewGuid()
        });
    }
}
