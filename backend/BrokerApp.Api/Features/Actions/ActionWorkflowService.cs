using BrokerApp.Api.Data;
using BrokerApp.Api.Domain;
using BrokerApp.Api.Features.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace BrokerApp.Api.Features.Actions;

public interface IActionWorkflowService
{
    Task<ActionWorkflowResultDto?> CompleteAsync(string publicId, CompleteActionRequest request, CancellationToken cancellationToken = default);
    Task<ActionWorkflowResultDto?> RescheduleAsync(string publicId, RescheduleActionRequest request, CancellationToken cancellationToken = default);
    Task<ActionWorkflowResultDto?> AddCommentAsync(string publicId, AddActionCommentRequest request, CancellationToken cancellationToken = default);
}

public sealed class ActionWorkflowService : IActionWorkflowService
{
    private readonly BrokerAppDbContext _dbContext;
    private readonly ISystemClock _clock;

    public ActionWorkflowService(BrokerAppDbContext dbContext, ISystemClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public async Task<ActionWorkflowResultDto?> CompleteAsync(
        string publicId,
        CompleteActionRequest request,
        CancellationToken cancellationToken = default)
    {
        var action = await FindActionAsync(publicId, cancellationToken);

        if (action is null)
        {
            return null;
        }

        if (action.WorkflowStatus != ActionWorkflowStatuses.Completed)
        {
            action.WorkflowStatus = ActionWorkflowStatuses.Completed;
            action.CompletedAtUtc = _clock.UtcNow;
            _dbContext.ActionEvents.Add(new ActionEvent
            {
                Id = Guid.NewGuid(),
                LoanActionId = action.Id,
                EventType = ActionEventTypes.Completed,
                ActorUserId = DevDataIds.LoanOfficerId,
                Reason = string.IsNullOrWhiteSpace(request.Reason) ? "Completed from dashboard." : request.Reason.Trim(),
                OldValue = ActionWorkflowStatuses.Open,
                NewValue = ActionWorkflowStatuses.Completed,
                OccurredAtUtc = _clock.UtcNow
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return ToResult(action);
    }

    public async Task<ActionWorkflowResultDto?> RescheduleAsync(
        string publicId,
        RescheduleActionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new ArgumentException("A reschedule reason is required.", nameof(request));
        }

        var action = await FindActionAsync(publicId, cancellationToken);

        if (action is null)
        {
            return null;
        }

        var oldValue = action.DueDate;
        action.DueDate = request.DueDate;
        _dbContext.ActionEvents.Add(new ActionEvent
        {
            Id = Guid.NewGuid(),
            LoanActionId = action.Id,
            EventType = ActionEventTypes.Rescheduled,
            ActorUserId = DevDataIds.LoanOfficerId,
            Reason = request.Reason.Trim(),
            OldValue = oldValue.ToString("O"),
            NewValue = request.DueDate.ToString("O"),
            OccurredAtUtc = _clock.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToResult(action);
    }

    public async Task<ActionWorkflowResultDto?> AddCommentAsync(
        string publicId,
        AddActionCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Body))
        {
            throw new ArgumentException("A comment body is required.", nameof(request));
        }

        var action = await FindActionAsync(publicId, cancellationToken);

        if (action is null)
        {
            return null;
        }

        var body = request.Body.Trim();
        _dbContext.LoanNotes.Add(new LoanNote
        {
            Id = Guid.NewGuid(),
            OrganizationId = action.OrganizationId,
            LoanId = action.LoanId,
            LoanActionId = action.Id,
            CreatedByUserId = DevDataIds.LoanOfficerId,
            Body = body,
            CreatedAtUtc = _clock.UtcNow
        });
        _dbContext.ActionEvents.Add(new ActionEvent
        {
            Id = Guid.NewGuid(),
            LoanActionId = action.Id,
            EventType = ActionEventTypes.CommentAdded,
            ActorUserId = DevDataIds.LoanOfficerId,
            NewValue = body,
            OccurredAtUtc = _clock.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToResult(action);
    }

    private Task<LoanAction?> FindActionAsync(string publicId, CancellationToken cancellationToken)
    {
        return _dbContext.LoanActions
            .Include(action => action.Events)
            .Include(action => action.Notes)
            .SingleOrDefaultAsync(
                action => action.OrganizationId == DevDataIds.OrganizationId && action.PublicId == publicId,
                cancellationToken);
    }

    private static ActionWorkflowResultDto ToResult(LoanAction action)
    {
        return new ActionWorkflowResultDto(
            action.PublicId,
            action.WorkflowStatus,
            action.DueDate,
            action.CompletedAtUtc);
    }
}
