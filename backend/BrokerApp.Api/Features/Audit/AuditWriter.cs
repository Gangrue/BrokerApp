using BrokerApp.Api.Data;
using BrokerApp.Api.Domain;
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

    public AuditWriter(BrokerAppDbContext dbContext, ISystemClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
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
            OrganizationId = DevDataIds.OrganizationId,
            ActorUserId = actorUserId ?? DevDataIds.LoanOfficerId,
            EntityType = entityType,
            EntityId = entityId,
            Operation = operation,
            ChangedFields = changedFields,
            OccurredAtUtc = _clock.UtcNow,
            CorrelationId = correlationId ?? Guid.NewGuid()
        });
    }
}
