using System.Text.RegularExpressions;

namespace BrokerApp.Api.Features.Imports;

public interface IImportColumnMapper
{
    ImportColumnMapping Map(RawImportSheet sheet);
}

public sealed record ImportColumnMapping(
    int HeaderRowNumber,
    IReadOnlyCollection<ImportMappedColumnDto> MappedColumns,
    IReadOnlyDictionary<string, int> FieldIndexes,
    IReadOnlyCollection<RawImportRow> DataRows);

public static class ImportFields
{
    public const string BorrowerFirstName = "borrowerFirstName";
    public const string BorrowerLastName = "borrowerLastName";
    public const string BorrowerFullName = "borrowerFullName";
    public const string BorrowerEmail = "borrowerEmail";
    public const string BorrowerPhone = "borrowerPhone";
    public const string LoanNumber = "loanNumber";
    public const string LoanType = "loanType";
    public const string LoanStage = "loanStage";
    public const string LoanAmount = "loanAmount";
    public const string TargetCloseDate = "targetCloseDate";
    public const string CoBorrowerEmail = "coBorrowerEmail";
    public const string TitleContactName = "titleContactName";
    public const string TitleContactEmail = "titleContactEmail";
    public const string RealtorName = "realtorName";
    public const string RealtorEmail = "realtorEmail";
    public const string IcdSent = "icdSent";
    public const string IcdSigned = "icdSigned";
    public const string LastContactDate = "lastContactDate";
    public const string InitialNote = "initialNote";
}

public sealed class ImportColumnMapper : IImportColumnMapper
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyCollection<string>> Aliases =
        new Dictionary<string, IReadOnlyCollection<string>>
        {
            [ImportFields.BorrowerFirstName] = ["first name", "firstname", "borrower first", "borrower first name", "customer first", "customer first name"],
            [ImportFields.BorrowerLastName] = ["last name", "lastname", "borrower last", "borrower last name", "customer last", "customer last name"],
            [ImportFields.BorrowerFullName] = ["borrower", "borrower name", "customer", "customer name", "client", "client name"],
            [ImportFields.BorrowerEmail] = ["email", "borrower email", "customer email", "client email"],
            [ImportFields.BorrowerPhone] = ["phone", "borrower phone", "customer phone", "client phone", "mobile"],
            [ImportFields.LoanNumber] = ["loan number", "loannumber", "loan #", "loan no", "loan id", "file number", "file #"],
            [ImportFields.LoanType] = ["loan type", "type", "product", "program"],
            [ImportFields.LoanStage] = ["stage", "loan stage", "status", "milestone", "pipeline stage"],
            [ImportFields.LoanAmount] = ["amount", "loan amount", "loan value", "mortgage amount"],
            [ImportFields.TargetCloseDate] = ["target close", "target close date", "closing date", "close date", "estimated close", "est close date"],
            [ImportFields.CoBorrowerEmail] = ["coborrower email", "co borrower email", "co-borrower email", "co borrower"],
            [ImportFields.TitleContactName] = ["title contact", "title contact name", "title name", "title company contact"],
            [ImportFields.TitleContactEmail] = ["title email", "title contact email"],
            [ImportFields.RealtorName] = ["realtor", "realtor name", "agent", "agent name", "buyers agent"],
            [ImportFields.RealtorEmail] = ["realtor email", "agent email", "buyers agent email"],
            [ImportFields.IcdSent] = ["icd sent", "initial cd sent", "initial closing disclosure sent"],
            [ImportFields.IcdSigned] = ["icd signed", "initial cd signed", "initial closing disclosure signed"],
            [ImportFields.LastContactDate] = ["last contact", "last contact date", "last touched", "last update"],
            [ImportFields.InitialNote] = ["note", "notes", "file note", "comments", "processor notes"]
        };

    private static readonly IReadOnlyDictionary<string, string> AliasLookup = Aliases
        .SelectMany(pair => pair.Value.Select(alias => new KeyValuePair<string, string>(Normalize(alias), pair.Key)))
        .GroupBy(pair => pair.Key)
        .ToDictionary(group => group.Key, group => group.First().Value);

    public ImportColumnMapping Map(RawImportSheet sheet)
    {
        var candidateRows = sheet.Rows.Take(10).ToArray();
        var best = candidateRows
            .Select(row => new
            {
                Row = row,
                Matches = CreateFieldIndexes(row.Values).Count
            })
            .OrderByDescending(candidate => candidate.Matches)
            .ThenBy(candidate => candidate.Row.RowNumber)
            .FirstOrDefault();

        if (best is null || best.Matches == 0)
        {
            throw new ImportValidationException("No recognizable header row was found.");
        }

        var fieldIndexes = CreateFieldIndexes(best.Row.Values);
        var mappedColumns = fieldIndexes
            .Select(pair => new ImportMappedColumnDto(pair.Key, best.Row.Values.ElementAt(pair.Value) ?? string.Empty, pair.Value))
            .OrderBy(item => item.ColumnIndex)
            .ToArray();
        var dataRows = sheet.Rows
            .Where(row => row.RowNumber > best.Row.RowNumber)
            .ToArray();

        return new ImportColumnMapping(best.Row.RowNumber, mappedColumns, fieldIndexes, dataRows);
    }

    private static Dictionary<string, int> CreateFieldIndexes(IReadOnlyCollection<string?> headers)
    {
        var fieldIndexes = new Dictionary<string, int>();
        var index = 0;

        foreach (var header in headers)
        {
            if (header is not null && AliasLookup.TryGetValue(Normalize(header), out var field) && !fieldIndexes.ContainsKey(field))
            {
                fieldIndexes[field] = index;
            }

            index++;
        }

        return fieldIndexes;
    }

    private static string Normalize(string value)
    {
        return Regex.Replace(value.ToLowerInvariant(), "[^a-z0-9]", string.Empty);
    }
}
