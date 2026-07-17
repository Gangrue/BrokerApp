using BrokerApp.Api.Data;
using BrokerApp.Api.Domain;
using BrokerApp.Api.Features.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace BrokerApp.Api.Features.Intake;

public interface IIntakeService
{
    Task<CreateFileIntakeResponse> CreateFileAsync(
        CreateFileIntakeRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class IntakeService : IIntakeService
{
    private const int FirstGeneratedActionNumber = 1001;
    private readonly BrokerAppDbContext _dbContext;
    private readonly ISystemClock _clock;

    public IntakeService(BrokerAppDbContext dbContext, ISystemClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public async Task<CreateFileIntakeResponse> CreateFileAsync(
        CreateFileIntakeRequest request,
        CancellationToken cancellationToken = default)
    {
        var customerInput = ValidateCustomer(request.Customer);
        var loanInput = ValidateLoan(request.Loan);
        var actionInputs = ValidateActions(request.Actions);
        var normalizedEmail = NormalizeOptional(customerInput.Email)?.ToLowerInvariant();
        var now = _clock.UtcNow;

        if (await _dbContext.Loans.AnyAsync(
            loan => loan.OrganizationId == DevDataIds.OrganizationId && loan.LoanNumber == loanInput.LoanNumber,
            cancellationToken))
        {
            throw new IntakeValidationException($"Loan number {loanInput.LoanNumber} already exists.");
        }

        var customer = normalizedEmail is null
            ? null
            : await _dbContext.Customers.FirstOrDefaultAsync(
                item => item.OrganizationId == DevDataIds.OrganizationId
                    && item.Status == "Active"
                    && item.Email != null
                    && item.Email.ToLower() == normalizedEmail,
                cancellationToken);
        var customerMatched = customer is not null;

        if (customer is null)
        {
            customer = new Customer
            {
                Id = Guid.NewGuid(),
                OrganizationId = DevDataIds.OrganizationId,
                FirstName = customerInput.FirstName,
                LastName = customerInput.LastName,
                Email = customerInput.Email,
                Phone = customerInput.Phone,
                Status = "Active",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            _dbContext.Customers.Add(customer);
        }

        var loan = new Loan
        {
            Id = Guid.NewGuid(),
            OrganizationId = DevDataIds.OrganizationId,
            CustomerId = customer.Id,
            OwnerUserId = DevDataIds.LoanOfficerId,
            LoanNumber = loanInput.LoanNumber,
            Type = loanInput.Type,
            Stage = loanInput.Stage,
            Status = "Active",
            Amount = loanInput.Amount,
            TargetCloseDate = loanInput.TargetCloseDate,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        _dbContext.Loans.Add(loan);

        var actionIds = await GenerateActionIdsAsync(actionInputs.Count, cancellationToken);

        foreach (var actionInput in actionInputs.Zip(actionIds))
        {
            var action = new LoanAction
            {
                Id = Guid.NewGuid(),
                OrganizationId = DevDataIds.OrganizationId,
                LoanId = loan.Id,
                AssignedUserId = DevDataIds.LoanOfficerId,
                PublicId = actionInput.Second,
                Type = "Condition",
                Section = actionInput.First.Section,
                Title = actionInput.First.Title,
                Description = actionInput.First.Description,
                WorkflowStatus = ActionWorkflowStatuses.Open,
                Priority = actionInput.First.Priority,
                DueDate = actionInput.First.DueDate,
                CreatedAtUtc = now
            };
            _dbContext.LoanActions.Add(action);
            _dbContext.ActionEvents.Add(new ActionEvent
            {
                Id = Guid.NewGuid(),
                LoanActionId = action.Id,
                EventType = ActionEventTypes.Created,
                ActorUserId = DevDataIds.LoanOfficerId,
                Reason = "Created during intake.",
                OccurredAtUtc = now
            });
        }

        var initialNote = NormalizeOptional(request.InitialNote);
        if (initialNote is not null)
        {
            _dbContext.LoanNotes.Add(new LoanNote
            {
                Id = Guid.NewGuid(),
                OrganizationId = DevDataIds.OrganizationId,
                LoanId = loan.Id,
                CreatedByUserId = DevDataIds.LoanOfficerId,
                Body = initialNote,
                CreatedAtUtc = now
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CreateFileIntakeResponse(
            loan.LoanNumber,
            $"{customer.LastName}, {customer.FirstName}",
            customerMatched,
            actionIds);
    }

    private async Task<IReadOnlyCollection<string>> GenerateActionIdsAsync(
        int count,
        CancellationToken cancellationToken)
    {
        var publicIds = await _dbContext.LoanActions
            .AsNoTracking()
            .Where(action => action.OrganizationId == DevDataIds.OrganizationId && action.PublicId.StartsWith("ACT-"))
            .Select(action => action.PublicId)
            .ToArrayAsync(cancellationToken);

        var maxActionNumber = publicIds
            .Select(ParseActionNumber)
            .Where(number => number > 0)
            .DefaultIfEmpty(FirstGeneratedActionNumber - 1)
            .Max();

        return Enumerable.Range(maxActionNumber + 1, count)
            .Select(number => $"ACT-{number:0000}")
            .ToArray();
    }

    private static int ParseActionNumber(string publicId)
    {
        return int.TryParse(publicId[4..], out var number) ? number : 0;
    }

    private static ValidCustomerInput ValidateCustomer(IntakeCustomerRequest? customer)
    {
        if (customer is null)
        {
            throw new IntakeValidationException("Borrower information is required.");
        }

        return new ValidCustomerInput(
            Require(customer.FirstName, "Borrower first name"),
            Require(customer.LastName, "Borrower last name"),
            NormalizeOptional(customer.Email),
            NormalizeOptional(customer.Phone));
    }

    private static ValidLoanInput ValidateLoan(IntakeLoanRequest? loan)
    {
        if (loan is null)
        {
            throw new IntakeValidationException("Loan information is required.");
        }

        return new ValidLoanInput(
            Require(loan.LoanNumber, "Loan number"),
            Require(loan.Type, "Loan type"),
            Require(loan.Stage, "Loan stage"),
            loan.Amount,
            loan.TargetCloseDate);
    }

    private static IReadOnlyCollection<ValidActionInput> ValidateActions(
        IReadOnlyCollection<IntakeActionRequest>? actions)
    {
        if (actions is null || actions.Count == 0)
        {
            throw new IntakeValidationException("At least one initial action is required.");
        }

        if (actions.Count > 3)
        {
            throw new IntakeValidationException("No more than three initial actions can be created.");
        }

        return actions.Select((action, index) =>
        {
            var actionNumber = index + 1;
            var section = Require(action.Section, $"Action {actionNumber} section");
            var priority = Require(action.Priority, $"Action {actionNumber} priority");

            if (section is not (ActionSections.Borrower or ActionSections.Title or ActionSections.Realtor))
            {
                throw new IntakeValidationException($"Action {actionNumber} section is invalid.");
            }

            if (priority is not (ActionPriorities.Normal or ActionPriorities.High))
            {
                throw new IntakeValidationException($"Action {actionNumber} priority is invalid.");
            }

            if (action.DueDate == default)
            {
                throw new IntakeValidationException($"Action {actionNumber} due date is required.");
            }

            return new ValidActionInput(
                Require(action.Title, $"Action {actionNumber} title"),
                section,
                priority,
                action.DueDate,
                NormalizeOptional(action.Description));
        }).ToArray();
    }

    private static string Require(string? value, string name)
    {
        var trimmed = NormalizeOptional(value);

        if (trimmed is null)
        {
            throw new IntakeValidationException($"{name} is required.");
        }

        return trimmed;
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();

        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private sealed record ValidCustomerInput(
        string FirstName,
        string LastName,
        string? Email,
        string? Phone);

    private sealed record ValidLoanInput(
        string LoanNumber,
        string Type,
        string Stage,
        decimal? Amount,
        DateOnly? TargetCloseDate);

    private sealed record ValidActionInput(
        string Title,
        string Section,
        string Priority,
        DateOnly DueDate,
        string? Description);
}
