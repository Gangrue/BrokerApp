namespace BrokerApp.Api.Domain;

public static class ActionPriorities
{
    public const string High = "High";
    public const string Normal = "Normal";
}

public static class ActionSections
{
    public const string Borrower = "Borrower";
    public const string Title = "Title";
    public const string Realtor = "Realtor";
}

public static class ActionWorkflowStatuses
{
    public const string Open = "Open";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
}

public static class ActionEventTypes
{
    public const string Completed = "Completed";
    public const string Rescheduled = "Rescheduled";
    public const string CommentAdded = "Comment Added";
    public const string Created = "Created";
    public const string Cancelled = "Cancelled";
    public const string Reassigned = "Reassigned";
}

public static class UserRoles
{
    public const string LoanOfficer = "Loan Officer";
    public const string TeamLead = "Team Lead";
}

public static class AuditOperations
{
    public const string Created = "Created";
    public const string Updated = "Updated";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
    public const string Reassigned = "Reassigned";
    public const string CommentAdded = "Comment Added";
    public const string Generated = "Generated";
    public const string Deleted = "Deleted";
}
