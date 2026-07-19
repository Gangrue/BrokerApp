using BrokerApp.Api.Data;
using BrokerApp.Api.Domain;
using BrokerApp.Api.Features.Auth;
using BrokerApp.Api.Features.Audit;
using BrokerApp.Api.Features.Dashboard;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace BrokerApp.Api.Features.Actions;

public interface IActionWorkflowService
{
    Task<ActionEmailDraftDto?> CreateEmailDraftAsync(string publicId, CancellationToken cancellationToken = default);
    Task<ActionWorkflowResultDto?> CompleteAsync(string publicId, CompleteActionRequest request, CancellationToken cancellationToken = default);
    Task<ActionWorkflowResultDto?> RescheduleAsync(string publicId, RescheduleActionRequest request, CancellationToken cancellationToken = default);
    Task<ActionWorkflowResultDto?> AddCommentAsync(string publicId, AddActionCommentRequest request, CancellationToken cancellationToken = default);
    Task<ActionWorkflowResultDto?> CancelAsync(string publicId, CancelActionRequest request, CancellationToken cancellationToken = default);
    Task<ActionWorkflowResultDto?> ReassignAsync(string publicId, ReassignActionRequest request, CancellationToken cancellationToken = default);
}

public sealed class ActionWorkflowService : IActionWorkflowService
{
    private readonly BrokerAppDbContext _dbContext;
    private readonly ISystemClock _clock;
    private readonly IAuditWriter _auditWriter;
    private readonly ICurrentUserContext _currentUser;

    public ActionWorkflowService(BrokerAppDbContext dbContext, ISystemClock clock, IAuditWriter auditWriter, ICurrentUserContext currentUser)
    {
        _dbContext = dbContext;
        _clock = clock;
        _auditWriter = auditWriter;
        _currentUser = currentUser;
    }

    public async Task<ActionEmailDraftDto?> CreateEmailDraftAsync(
        string publicId,
        CancellationToken cancellationToken = default)
    {
        var action = await _dbContext.LoanActions
            .AsNoTracking()
            .Include(item => item.Loan)
                .ThenInclude(loan => loan.Customer)
            .SingleOrDefaultAsync(
                item => item.OrganizationId == _currentUser.OrganizationId && item.PublicId == publicId,
                cancellationToken);

        if (action is null)
        {
            return null;
        }

        var customer = action.Loan.Customer;
        var dueDate = action.DueDate.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture);
        var subject = $"{action.Loan.LoanNumber}: {action.Title}";
        var body = $"""
Hi {customer.FirstName},

I am following up on your loan file {action.Loan.LoanNumber}. We need your help with the following item:

{action.Title}

Please send this over or let me know if you have questions. The current target date for this item is {dueDate}.

Thank you,
Demo Loan Officer
""";

        if (!string.IsNullOrWhiteSpace(action.Description))
        {
            body = $"""
Hi {customer.FirstName},

I am following up on your loan file {action.Loan.LoanNumber}. We need your help with the following item:

{action.Title}

{action.Description}

Please send this over or let me know if you have questions. The current target date for this item is {dueDate}.

Thank you,
Demo Loan Officer
""";
        }

        return new ActionEmailDraftDto(customer.Email ?? string.Empty, subject, body);
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
            var oldStatus = action.WorkflowStatus;
            action.WorkflowStatus = ActionWorkflowStatuses.Completed;
            action.CompletedAtUtc = _clock.UtcNow;
            _dbContext.ActionEvents.Add(new ActionEvent
            {
                Id = Guid.NewGuid(),
                LoanActionId = action.Id,
                EventType = ActionEventTypes.Completed,
                ActorUserId = _currentUser.UserId,
                Reason = string.IsNullOrWhiteSpace(request.Reason) ? "Completed from dashboard." : request.Reason.Trim(),
                OldValue = oldStatus,
                NewValue = ActionWorkflowStatuses.Completed,
                OccurredAtUtc = _clock.UtcNow
            });
            _auditWriter.Record(
                "LoanAction",
                action.PublicId,
                AuditOperations.Completed,
                $"WorkflowStatus: {oldStatus} -> {ActionWorkflowStatuses.Completed}");

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
            ActorUserId = _currentUser.UserId,
            Reason = request.Reason.Trim(),
            OldValue = oldValue.ToString("O"),
            NewValue = request.DueDate.ToString("O"),
            OccurredAtUtc = _clock.UtcNow
        });
        _auditWriter.Record(
            "LoanAction",
            action.PublicId,
            AuditOperations.Updated,
            $"DueDate: {oldValue:O} -> {request.DueDate:O}");

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
            CreatedByUserId = _currentUser.UserId,
            Body = body,
            CreatedAtUtc = _clock.UtcNow
        });
        _dbContext.ActionEvents.Add(new ActionEvent
        {
            Id = Guid.NewGuid(),
            LoanActionId = action.Id,
            EventType = ActionEventTypes.CommentAdded,
            ActorUserId = _currentUser.UserId,
            NewValue = body,
            OccurredAtUtc = _clock.UtcNow
        });
        _auditWriter.Record("LoanAction", action.PublicId, AuditOperations.CommentAdded, "Comment added.");

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToResult(action);
    }

    public async Task<ActionWorkflowResultDto?> CancelAsync(
        string publicId,
        CancelActionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new ArgumentException("A cancellation reason is required.", nameof(request));
        }

        var action = await FindActionAsync(publicId, cancellationToken);

        if (action is null)
        {
            return null;
        }

        var oldStatus = action.WorkflowStatus;
        action.WorkflowStatus = ActionWorkflowStatuses.Cancelled;
        action.CompletedAtUtc = null;
        _dbContext.ActionEvents.Add(new ActionEvent
        {
            Id = Guid.NewGuid(),
            LoanActionId = action.Id,
            EventType = ActionEventTypes.Cancelled,
            ActorUserId = _currentUser.UserId,
            Reason = request.Reason.Trim(),
            OldValue = oldStatus,
            NewValue = ActionWorkflowStatuses.Cancelled,
            OccurredAtUtc = _clock.UtcNow
        });
        _auditWriter.Record(
            "LoanAction",
            action.PublicId,
            AuditOperations.Cancelled,
            $"WorkflowStatus: {oldStatus} -> {ActionWorkflowStatuses.Cancelled}");

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToResult(action);
    }

    public async Task<ActionWorkflowResultDto?> ReassignAsync(
        string publicId,
        ReassignActionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.AssignedUserId == Guid.Empty)
        {
            throw new ArgumentException("An assignee is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new ArgumentException("A reassignment reason is required.", nameof(request));
        }

        var action = await FindActionAsync(publicId, cancellationToken);

        if (action is null)
        {
            return null;
        }

        var assignee = await _dbContext.Users.SingleOrDefaultAsync(
            user => user.OrganizationId == _currentUser.OrganizationId
                && user.Id == request.AssignedUserId
                && user.IsActive,
            cancellationToken);

        if (assignee is null)
        {
            throw new ArgumentException("The assignee was not found.", nameof(request));
        }

        var oldAssignedUserId = action.AssignedUserId;
        action.AssignedUserId = assignee.Id;
        action.AssignedUser = assignee;
        _dbContext.ActionEvents.Add(new ActionEvent
        {
            Id = Guid.NewGuid(),
            LoanActionId = action.Id,
            EventType = ActionEventTypes.Reassigned,
            ActorUserId = _currentUser.UserId,
            Reason = request.Reason.Trim(),
            OldValue = oldAssignedUserId?.ToString(),
            NewValue = assignee.Id.ToString(),
            OccurredAtUtc = _clock.UtcNow
        });
        _auditWriter.Record(
            "LoanAction",
            action.PublicId,
            AuditOperations.Reassigned,
            $"AssignedUserId: {oldAssignedUserId?.ToString() ?? "Unassigned"} -> {assignee.Id}");

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToResult(action);
    }

    private Task<LoanAction?> FindActionAsync(string publicId, CancellationToken cancellationToken)
    {
        return _dbContext.LoanActions
            .Include(action => action.Events)
            .Include(action => action.Notes)
            .Include(action => action.AssignedUser)
            .SingleOrDefaultAsync(
                action => action.OrganizationId == _currentUser.OrganizationId && action.PublicId == publicId,
                cancellationToken);
    }

    private static ActionWorkflowResultDto ToResult(LoanAction action)
    {
        return new ActionWorkflowResultDto(
            action.PublicId,
            action.WorkflowStatus,
            action.DueDate,
            action.CompletedAtUtc,
            action.AssignedUserId,
            action.AssignedUser?.DisplayName);
    }
}
