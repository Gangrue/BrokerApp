namespace BrokerApp.Api.Features.Dashboard;

public static class DashboardBucketClassifier
{
    public const string Overdue = "Overdue";
    public const string DueToday = "Due Today";
    public const string Upcoming = "Upcoming";

    public static string Classify(DateOnly dueDate, DateOnly today)
    {
        if (dueDate < today)
        {
            return Overdue;
        }

        return dueDate == today ? DueToday : Upcoming;
    }

    public static int SortRank(string bucket)
    {
        return bucket switch
        {
            Overdue => 0,
            DueToday => 1,
            Upcoming => 2,
            _ => 3
        };
    }
}
