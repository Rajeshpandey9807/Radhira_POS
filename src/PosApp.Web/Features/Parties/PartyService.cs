using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using PosApp.Web.Data;

namespace PosApp.Web.Features.Parties;

public sealed class PartyService
{
    private readonly IDbConnectionFactory _connectionFactory;

    public PartyService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<PartyLookupOption>> GetPartyTypesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        return await QueryLookupAsync(
            connection,
            tableName: "PartyTypes",
            idColumn: "PartyTypeId",
            nameColumnCandidates: new[] { "TypeName", "PartyTypeName", "Name" },
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<PartyLookupOption>> GetPartyCategoriesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        return await QueryLookupAsync(
            connection,
            tableName: "PartyCategories",
            idColumn: "PartyCategoryId",
            nameColumnCandidates: new[] { "CategoryName", "PartyCategoryName", "Name" },
            cancellationToken: cancellationToken);
    }

    public async Task<int> CreateAsync(PartyCreateRequest request, int createdBy = 0, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            const string insertPartySql = @"
INSERT INTO Parties (PartyName, MobileNumber, Email, OpeningBalance, GSTIN, PANNumber, PartyTypeId, PartyCategoryId)
VALUES (@PartyName, @MobileNumber, @Email, @OpeningBalance, @GSTIN, @PANNumber, @PartyTypeId, @PartyCategoryId);
SELECT CAST(SCOPE_IDENTITY() AS int);";

            var partyId = await connection.ExecuteScalarAsync<int>(new CommandDefinition(insertPartySql, new
            {
                PartyName = request.PartyName.Trim(),
                MobileNumber = request.MobileNumber?.Trim(),
                Email = request.Email?.Trim(),
                OpeningBalance = request.OpeningBalance,
                GSTIN = request.GSTIN?.Trim(),
                PANNumber = request.PANNumber?.Trim(),
                PartyTypeId = request.PartyTypeId,
                PartyCategoryId = request.PartyCategoryId
            }, transaction: transaction, cancellationToken: cancellationToken));

            const string insertAddressSql = @"
INSERT INTO PartyAddresses (PartyId, AddressType, Address, CreditPeriod, CreditLimit)
VALUES (@PartyId, @AddressType, @Address, @CreditPeriod, @CreditLimit);";

            // Billing
            if (!string.IsNullOrWhiteSpace(request.BillingAddress) || request.CreditPeriod.HasValue || request.CreditLimit.HasValue)
            {
                await connection.ExecuteAsync(new CommandDefinition(insertAddressSql, new
                {
                    PartyId = partyId,
                    AddressType = "Billing",
                    Address = request.BillingAddress?.Trim(),
                    CreditPeriod = request.CreditPeriod,
                    CreditLimit = request.CreditLimit
                }, transaction: transaction, cancellationToken: cancellationToken));
            }

            // Shipping
            if (!string.IsNullOrWhiteSpace(request.ShippingAddress))
            {
                await connection.ExecuteAsync(new CommandDefinition(insertAddressSql, new
                {
                    PartyId = partyId,
                    AddressType = "Shipping",
                    Address = request.ShippingAddress?.Trim(),
                    CreditPeriod = request.CreditPeriod,
                    CreditLimit = request.CreditLimit
                }, transaction: transaction, cancellationToken: cancellationToken));
            }

            const string insertContactSql = @"
INSERT INTO PartyContacts (PartyId, ContactPersonName, DateOfBirth)
VALUES (@PartyId, @ContactPersonName, @DateOfBirth);";

            if (!string.IsNullOrWhiteSpace(request.ContactPersonName) || request.DateOfBirth.HasValue)
            {
                await connection.ExecuteAsync(new CommandDefinition(insertContactSql, new
                {
                    PartyId = partyId,
                    ContactPersonName = request.ContactPersonName?.Trim(),
                    DateOfBirth = request.DateOfBirth
                }, transaction: transaction, cancellationToken: cancellationToken));
            }

            const string insertBankSql = @"
INSERT INTO PartyBankDetails (PartyId, AccountNumber, IFSC, BranchName, AccountHolderName, UPI)
VALUES (@PartyId, @AccountNumber, @IFSC, @BranchName, @AccountHolderName, @UPI);";

            if (!string.IsNullOrWhiteSpace(request.AccountNumber)
                || !string.IsNullOrWhiteSpace(request.IFSC)
                || !string.IsNullOrWhiteSpace(request.BranchName)
                || !string.IsNullOrWhiteSpace(request.AccountHolderName)
                || !string.IsNullOrWhiteSpace(request.UPI))
            {
                await connection.ExecuteAsync(new CommandDefinition(insertBankSql, new
                {
                    PartyId = partyId,
                    AccountNumber = request.AccountNumber?.Trim(),
                    IFSC = request.IFSC?.Trim(),
                    BranchName = request.BranchName?.Trim(),
                    AccountHolderName = request.AccountHolderName?.Trim(),
                    UPI = request.UPI?.Trim()
                }, transaction: transaction, cancellationToken: cancellationToken));
            }

            transaction.Commit();
            return partyId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static async Task<IReadOnlyList<PartyLookupOption>> QueryLookupAsync(
        IDbConnection connection,
        string tableName,
        string idColumn,
        IReadOnlyList<string> nameColumnCandidates,
        CancellationToken cancellationToken)
    {
        // We can't parameterize identifiers; keep this safe by only allowing known column names.
        string PickCandidate(IEnumerable<string> existing)
        {
            var set = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var candidate in nameColumnCandidates)
            {
                if (set.Contains(candidate))
                {
                    return candidate;
                }
            }
            return string.Empty;
        }

        var providerName = connection.GetType().Name;
        var isSqlite = providerName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase);

        string nameColumn;
        if (isSqlite)
        {
            // SQLite: use PRAGMA table_info to discover columns.
            var pragmaSql = "SELECT name FROM pragma_table_info(@TableName);";
            var columns = await connection.QueryAsync<string>(new CommandDefinition(pragmaSql, new { TableName = tableName }, cancellationToken: cancellationToken));
            nameColumn = PickCandidate(columns);
        }
        else
        {
            // SQL Server: INFORMATION_SCHEMA
            const string columnsSql = @"
SELECT COLUMN_NAME
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'dbo'
  AND TABLE_NAME = @TableName;";
            var columns = await connection.QueryAsync<string>(new CommandDefinition(columnsSql, new { TableName = tableName }, cancellationToken: cancellationToken));
            nameColumn = PickCandidate(columns);
        }

        if (string.IsNullOrWhiteSpace(nameColumn))
        {
            throw new InvalidOperationException($"Could not locate a name column for {tableName}. Tried: {string.Join(", ", nameColumnCandidates)}");
        }

        var sql = $@"SELECT {idColumn} AS Id, {nameColumn} AS Name
                     FROM {tableName}
                     ORDER BY {nameColumn};";
        var result = await connection.QueryAsync<PartyLookupOption>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return result.ToList();
    }
}

