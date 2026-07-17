using BrokerApp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace BrokerApp.Api.Features.Actions;

public interface IActionPublicIdGenerator
{
    Task<IReadOnlyCollection<string>> GenerateAsync(int count, CancellationToken cancellationToken = default);
}

public sealed class ActionPublicIdGenerator : IActionPublicIdGenerator
{
    private const int FirstGeneratedActionNumber = 1001;
    private readonly BrokerAppDbContext _dbContext;

    public ActionPublicIdGenerator(BrokerAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<string>> GenerateAsync(int count, CancellationToken cancellationToken = default)
    {
        if (count <= 0)
        {
            return [];
        }

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
        return publicId.Length > 4 && int.TryParse(publicId[4..], out var number) ? number : 0;
    }
}
