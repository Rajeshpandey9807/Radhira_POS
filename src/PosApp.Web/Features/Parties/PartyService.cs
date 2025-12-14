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

    public async Task<IReadOnlyList<PartyTypeOption>> GetPartyTypesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"SELECT PartyTypeId, TypeName
                             FROM PartyTypes
                             ORDER BY TypeName;";
        var result = await connection.QueryAsync<PartyTypeOption>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return result.ToList();
    }

    public async Task<IReadOnlyList<PartyCategoryOption>> GetPartyCategoriesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        // Keep categories resilient across environments.
        // Preferred column name is CategoryName, but older schemas may use PartyCategoryName.
        return await QueryCategoryLookupAsync(connection, cancellationToken);
    }

    public async Task<IReadOnlyList<PartyListItem>> GetPartiesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        var categoryColumn = await ResolvePartyCategoryNameColumnAsync(connection, cancellationToken);

        var sql = $@"
SELECT
    p.PartyId,
    p.PartyName,
    p.MobileNumber,
    p.Email,
    COALESCE(pt.TypeName, '') AS PartyTypeName,
    COALESCE(pc.{categoryColumn}, '') AS PartyCategoryName,
    COALESCE(p.CreatedBy, 0) AS CreatedBy,
    COALESCE(p.CreatedOn, SYSUTCDATETIME()) AS CreatedOn
FROM Parties p
LEFT JOIN PartyTypes pt ON pt.PartyTypeId = p.PartyTypeId
LEFT JOIN PartyCategories pc ON pc.PartyCategoryId = p.PartyCategoryId
ORDER BY p.PartyId DESC;";

        var result = await connection.QueryAsync<PartyListItem>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return result.ToList();
    }

    public async Task<PartyEditRequest?> GetPartyForEditAsync(int partyId, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        const string partySql = @"
SELECT TOP 1
    PartyId,
    PartyName,
    MobileNumber,
    Email,
    OpeningBalance,
    GSTIN,
    PANNumber,
    PartyTypeId,
    PartyCategoryId
FROM Parties
WHERE PartyId = @PartyId;";

        var party = await connection.QuerySingleOrDefaultAsync<PartyEditRequest>(
            new CommandDefinition(partySql, new { PartyId = partyId }, cancellationToken: cancellationToken));

        if (party is null)
        {
            return null;
        }

        const string addressSql = @"
SELECT AddressType, Address, CreditPeriod, CreditLimit
FROM PartyAddresses
WHERE PartyId = @PartyId;";

        var addresses = (await connection.QueryAsync<(string AddressType, string? Address, int? CreditPeriod, decimal? CreditLimit)>(
            new CommandDefinition(addressSql, new { PartyId = partyId }, cancellationToken: cancellationToken))).ToList();

        var billing = addresses.FirstOrDefault(x => string.Equals(x.AddressType, "Billing", StringComparison.OrdinalIgnoreCase));
        var shipping = addresses.FirstOrDefault(x => string.Equals(x.AddressType, "Shipping", StringComparison.OrdinalIgnoreCase));

        party.BillingAddress = billing.Address;
        party.ShippingAddress = shipping.Address;
        party.CreditPeriod = billing.CreditPeriod ?? shipping.CreditPeriod;
        party.CreditLimit = billing.CreditLimit ?? shipping.CreditLimit;

        if (!string.IsNullOrWhiteSpace(party.BillingAddress)
            && string.Equals((party.ShippingAddress ?? string.Empty).Trim(), party.BillingAddress.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            party.SameAsBilling = true;
        }

        const string contactSql = @"
SELECT TOP 1 ContactPersonName, DateOfBirth
FROM PartyContacts
WHERE PartyId = @PartyId
ORDER BY ContactId DESC;";
        var contact = await connection.QuerySingleOrDefaultAsync<(string? ContactPersonName, DateTime? DateOfBirth)>(
            new CommandDefinition(contactSql, new { PartyId = partyId }, cancellationToken: cancellationToken));
        party.ContactPersonName = contact.ContactPersonName;
        party.DateOfBirth = contact.DateOfBirth;

        const string bankSql = @"
SELECT TOP 1 AccountNumber, IFSC, BranchName, AccountHolderName, UPI
FROM PartyBankDetails
WHERE PartyId = @PartyId
ORDER BY BankDetailId DESC;";
        var bank = await connection.QuerySingleOrDefaultAsync<(string? AccountNumber, string? IFSC, string? BranchName, string? AccountHolderName, string? UPI)>(
            new CommandDefinition(bankSql, new { PartyId = partyId }, cancellationToken: cancellationToken));

        party.AccountNumber = bank.AccountNumber;
        party.IFSC = bank.IFSC;
        party.BranchName = bank.BranchName;
        party.AccountHolderName = bank.AccountHolderName;
        party.UPI = bank.UPI;

        if (!string.IsNullOrWhiteSpace(party.AccountNumber))
        {
            party.ReEnterAccountNumber = party.AccountNumber;
        }

        return party;
    }

    public async Task<int> CreateAsync(PartyCreateRequest request, int createdBy = 0, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            const string insertPartySql = @"
INSERT INTO Parties (PartyName, MobileNumber, Email, OpeningBalance, GSTIN, PANNumber, PartyTypeId, PartyCategoryId, CreatedBy)
VALUES (@PartyName, @MobileNumber, @Email, @OpeningBalance, @GSTIN, @PANNumber, @PartyTypeId, @PartyCategoryId, @CreatedBy);
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
                PartyCategoryId = request.PartyCategoryId,
                CreatedBy = createdBy
            }, transaction: transaction, cancellationToken: cancellationToken));

            const string insertAddressSql = @"
INSERT INTO PartyAddresses (PartyId, AddressType, Address, CreditPeriod, CreditLimit, CreatedBy)
VALUES (@PartyId, @AddressType, @Address, @CreditPeriod, @CreditLimit, @CreatedBy);";

            // Billing
            if (!string.IsNullOrWhiteSpace(request.BillingAddress) || request.CreditPeriod.HasValue || request.CreditLimit.HasValue)
            {
                await connection.ExecuteAsync(new CommandDefinition(insertAddressSql, new
                {
                    PartyId = partyId,
                    AddressType = "Billing",
                    Address = request.BillingAddress?.Trim(),
                    CreditPeriod = request.CreditPeriod,
                    CreditLimit = request.CreditLimit,
                    CreatedBy = createdBy
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
                    CreditLimit = request.CreditLimit,
                    CreatedBy = createdBy
                }, transaction: transaction, cancellationToken: cancellationToken));
            }

            const string insertContactSql = @"
INSERT INTO PartyContacts (PartyId, ContactPersonName, DateOfBirth, CreatedBy)
VALUES (@PartyId, @ContactPersonName, @DateOfBirth, @CreatedBy);";

            if (!string.IsNullOrWhiteSpace(request.ContactPersonName) || request.DateOfBirth.HasValue)
            {
                await connection.ExecuteAsync(new CommandDefinition(insertContactSql, new
                {
                    PartyId = partyId,
                    ContactPersonName = request.ContactPersonName?.Trim(),
                    DateOfBirth = request.DateOfBirth,
                    CreatedBy = createdBy
                }, transaction: transaction, cancellationToken: cancellationToken));
            }

            const string insertBankSql = @"
INSERT INTO PartyBankDetails (PartyId, AccountNumber, IFSC, BranchName, AccountHolderName, UPI, CreatedBy)
VALUES (@PartyId, @AccountNumber, @IFSC, @BranchName, @AccountHolderName, @UPI, @CreatedBy);";

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
                    UPI = request.UPI?.Trim(),
                    CreatedBy = createdBy
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

    public async Task UpdateAsync(int partyId, PartyCreateRequest request, int updatedBy = 0, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            const string updatePartySql = @"
UPDATE Parties
SET PartyName = @PartyName,
    MobileNumber = @MobileNumber,
    Email = @Email,
    OpeningBalance = @OpeningBalance,
    GSTIN = @GSTIN,
    PANNumber = @PANNumber,
    PartyTypeId = @PartyTypeId,
    PartyCategoryId = @PartyCategoryId,
    UpdatedBy = @UpdatedBy,
    UpdatedOn = SYSUTCDATETIME()
WHERE PartyId = @PartyId;";

            await connection.ExecuteAsync(new CommandDefinition(updatePartySql, new
            {
                PartyId = partyId,
                PartyName = request.PartyName.Trim(),
                MobileNumber = request.MobileNumber?.Trim(),
                Email = request.Email?.Trim(),
                OpeningBalance = request.OpeningBalance,
                GSTIN = request.GSTIN?.Trim(),
                PANNumber = request.PANNumber?.Trim(),
                PartyTypeId = request.PartyTypeId,
                PartyCategoryId = request.PartyCategoryId,
                UpdatedBy = updatedBy
            }, transaction: transaction, cancellationToken: cancellationToken));

            const string deleteAddressesSql = "DELETE FROM PartyAddresses WHERE PartyId = @PartyId;";
            const string deleteContactsSql = "DELETE FROM PartyContacts WHERE PartyId = @PartyId;";
            const string deleteBankSql = "DELETE FROM PartyBankDetails WHERE PartyId = @PartyId;";

            await connection.ExecuteAsync(new CommandDefinition(deleteAddressesSql, new { PartyId = partyId }, transaction: transaction, cancellationToken: cancellationToken));
            await connection.ExecuteAsync(new CommandDefinition(deleteContactsSql, new { PartyId = partyId }, transaction: transaction, cancellationToken: cancellationToken));
            await connection.ExecuteAsync(new CommandDefinition(deleteBankSql, new { PartyId = partyId }, transaction: transaction, cancellationToken: cancellationToken));

            const string insertAddressSql = @"
INSERT INTO PartyAddresses (PartyId, AddressType, Address, CreditPeriod, CreditLimit, CreatedBy)
VALUES (@PartyId, @AddressType, @Address, @CreditPeriod, @CreditLimit, @CreatedBy);";

            if (!string.IsNullOrWhiteSpace(request.BillingAddress) || request.CreditPeriod.HasValue || request.CreditLimit.HasValue)
            {
                await connection.ExecuteAsync(new CommandDefinition(insertAddressSql, new
                {
                    PartyId = partyId,
                    AddressType = "Billing",
                    Address = request.BillingAddress?.Trim(),
                    CreditPeriod = request.CreditPeriod,
                    CreditLimit = request.CreditLimit,
                    CreatedBy = updatedBy
                }, transaction: transaction, cancellationToken: cancellationToken));
            }

            if (!string.IsNullOrWhiteSpace(request.ShippingAddress))
            {
                await connection.ExecuteAsync(new CommandDefinition(insertAddressSql, new
                {
                    PartyId = partyId,
                    AddressType = "Shipping",
                    Address = request.ShippingAddress?.Trim(),
                    CreditPeriod = request.CreditPeriod,
                    CreditLimit = request.CreditLimit,
                    CreatedBy = updatedBy
                }, transaction: transaction, cancellationToken: cancellationToken));
            }

            const string insertContactSql = @"
INSERT INTO PartyContacts (PartyId, ContactPersonName, DateOfBirth, CreatedBy)
VALUES (@PartyId, @ContactPersonName, @DateOfBirth, @CreatedBy);";

            if (!string.IsNullOrWhiteSpace(request.ContactPersonName) || request.DateOfBirth.HasValue)
            {
                await connection.ExecuteAsync(new CommandDefinition(insertContactSql, new
                {
                    PartyId = partyId,
                    ContactPersonName = request.ContactPersonName?.Trim(),
                    DateOfBirth = request.DateOfBirth,
                    CreatedBy = updatedBy
                }, transaction: transaction, cancellationToken: cancellationToken));
            }

            const string insertBankSql = @"
INSERT INTO PartyBankDetails (PartyId, AccountNumber, IFSC, BranchName, AccountHolderName, UPI, CreatedBy)
VALUES (@PartyId, @AccountNumber, @IFSC, @BranchName, @AccountHolderName, @UPI, @CreatedBy);";

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
                    UPI = request.UPI?.Trim(),
                    CreatedBy = updatedBy
                }, transaction: transaction, cancellationToken: cancellationToken));
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static async Task<IReadOnlyList<PartyCategoryOption>> QueryCategoryLookupAsync(
        IDbConnection connection,
        CancellationToken cancellationToken)
    {
        var providerName = connection.GetType().Name;
        var isSqlite = providerName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase);

        string nameColumn = string.Empty;
        if (isSqlite)
        {
            // SQLite: use PRAGMA table_info to discover columns.
            var pragmaSql = "SELECT name FROM pragma_table_info(@TableName);";
            var columns = (await connection.QueryAsync<string>(new CommandDefinition(pragmaSql, new { TableName = "PartyCategories" }, cancellationToken: cancellationToken)))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (columns.Contains("CategoryName")) nameColumn = "CategoryName";
            else if (columns.Contains("PartyCategoryName")) nameColumn = "PartyCategoryName";
        }
        else
        {
            // SQL Server: INFORMATION_SCHEMA
            const string columnsSql = @"
SELECT COLUMN_NAME
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'dbo'
  AND TABLE_NAME = @TableName;";
            var columns = (await connection.QueryAsync<string>(new CommandDefinition(columnsSql, new { TableName = "PartyCategories" }, cancellationToken: cancellationToken)))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (columns.Contains("CategoryName")) nameColumn = "CategoryName";
            else if (columns.Contains("PartyCategoryName")) nameColumn = "PartyCategoryName";
        }

        if (string.IsNullOrWhiteSpace(nameColumn))
        {
            throw new InvalidOperationException("Could not locate a name column for PartyCategories. Tried: CategoryName, PartyCategoryName");
        }

        var sql = $@"SELECT PartyCategoryId, {nameColumn} AS CategoryName
                     FROM PartyCategories
                     ORDER BY {nameColumn};";
        var result = await connection.QueryAsync<PartyCategoryOption>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return result.ToList();
    }

    private static async Task<string> ResolvePartyCategoryNameColumnAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        var providerName = connection.GetType().Name;
        var isSqlite = providerName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase);

        if (isSqlite)
        {
            var pragmaSql = "SELECT name FROM pragma_table_info(@TableName);";
            var columns = (await connection.QueryAsync<string>(new CommandDefinition(pragmaSql, new { TableName = "PartyCategories" }, cancellationToken: cancellationToken)))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (columns.Contains("CategoryName")) return "CategoryName";
            if (columns.Contains("PartyCategoryName")) return "PartyCategoryName";
            throw new InvalidOperationException("PartyCategories name column not found (CategoryName / PartyCategoryName).");
        }

        const string columnsSql = @"
SELECT COLUMN_NAME
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'dbo'
  AND TABLE_NAME = @TableName;";
        var sqlColumns = (await connection.QueryAsync<string>(new CommandDefinition(columnsSql, new { TableName = "PartyCategories" }, cancellationToken: cancellationToken)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (sqlColumns.Contains("CategoryName")) return "CategoryName";
        if (sqlColumns.Contains("PartyCategoryName")) return "PartyCategoryName";
        throw new InvalidOperationException("PartyCategories name column not found (CategoryName / PartyCategoryName).");
    }
}

