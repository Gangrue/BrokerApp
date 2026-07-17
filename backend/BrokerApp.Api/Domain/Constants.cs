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

public static class UserRoles
{
    public const string LoanOfficer = "Loan Officer";
}
