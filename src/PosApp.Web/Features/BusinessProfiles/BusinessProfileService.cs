using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using PosApp.Web.Data;

namespace PosApp.Web.Features.BusinessProfiles;

public sealed class BusinessProfileService
{
    private readonly IDbConnectionFactory _connectionFactory;

    public BusinessProfileService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<BusinessProfileFormViewModel?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        var isSqlite = connection.GetType().Name.Contains("Sqlite", StringComparison.OrdinalIgnoreCase);

        var businessSql = isSqlite
            ? @"SELECT BusinessId, BusinessName, CompanyPhoneNumber, CompanyEmail,
                       IsGstRegistered, GstNumber, PanNumber,
                       IndustryTypeId, RegistrationTypeId,
                       MsmeNumber, Website, AdditionalInfo
                FROM Businesses
                ORDER BY BusinessId DESC
                LIMIT 1;"
            : @"SELECT TOP 1 BusinessId, BusinessName, CompanyPhoneNumber, CompanyEmail,
                       IsGstRegistered, GstNumber, PanNumber,
                       IndustryTypeId, RegistrationTypeId,
                       MsmeNumber, Website, AdditionalInfo
                FROM Businesses
                ORDER BY BusinessId DESC;";

        var business = await connection.QuerySingleOrDefaultAsync<BusinessRow>(
            new CommandDefinition(businessSql, cancellationToken: cancellationToken));

        if (business is null)
        {
            return null;
        }

        const string addressSql = @"SELECT BusinessAddressId, BusinessId, BillingAddress, City, Pincode, StateId
                                    FROM BusinessAddresses
                                    WHERE BusinessId = @BusinessId";
        var address = await connection.QuerySingleOrDefaultAsync<AddressRow>(
            new CommandDefinition(addressSql, new { business.BusinessId }, cancellationToken: cancellationToken));

        const string typesSql = @"SELECT BusinessTypeId
                                  FROM BusinessBusinessTypes
                                  WHERE BusinessId = @BusinessId
                                  ORDER BY BusinessTypeId";
        var businessTypeIds = (await connection.QueryAsync<int>(
            new CommandDefinition(typesSql, new { business.BusinessId }, cancellationToken: cancellationToken))).ToList();

        return new BusinessProfileFormViewModel
        {
            BusinessId = business.BusinessId,
            BusinessName = business.BusinessName ?? string.Empty,
            CompanyPhoneNumber = business.CompanyPhoneNumber,
            CompanyEmail = business.CompanyEmail,
            IsGstRegistered = business.IsGstRegistered,
            GstNumber = business.GstNumber,
            PanNumber = business.PanNumber,
            IndustryTypeId = business.IndustryTypeId,
            RegistrationTypeId = business.RegistrationTypeId,
            MsmeNumber = business.MsmeNumber,
            Website = business.Website,
            AdditionalInfo = business.AdditionalInfo,
            BillingAddress = address?.BillingAddress,
            City = address?.City,
            Pincode = address?.Pincode,
            StateId = address?.StateId,
            SelectedBusinessTypeIds = businessTypeIds
        };
    }

    public async Task<int> SaveAsync(BusinessProfileFormViewModel model, int actorId, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        var isSqlite = connection.GetType().Name.Contains("Sqlite", StringComparison.OrdinalIgnoreCase);
        using var transaction = connection.BeginTransaction();

        try
        {
            var businessId = model.BusinessId;
            if (businessId is null)
            {
                businessId = await InsertBusinessAsync(connection, transaction, isSqlite, model, actorId, cancellationToken);
            }
            else
            {
                await UpdateBusinessAsync(connection, transaction, isSqlite, businessId.Value, model, actorId, cancellationToken);
            }

            await UpsertAddressAsync(connection, transaction, isSqlite, businessId.Value, model, actorId, cancellationToken);
            await ReplaceBusinessTypesAsync(connection, transaction, isSqlite, businessId.Value, model.SelectedBusinessTypeIds, cancellationToken);

            transaction.Commit();
            return businessId.Value;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static async Task<int> InsertBusinessAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        bool isSqlite,
        BusinessProfileFormViewModel model,
        int actorId,
        CancellationToken cancellationToken)
    {
        var logoBytes = await ToBytesAsync(model.BusinessLogoFile, cancellationToken);
        var signatureBytes = await ToBytesAsync(model.SignatureFile, cancellationToken);

        var insertSql = isSqlite
            ? @"INSERT INTO Businesses
                    (BusinessName, CompanyPhoneNumber, CompanyEmail,
                     IsGstRegistered, GstNumber, PanNumber,
                     IndustryTypeId, RegistrationTypeId,
                     MsmeNumber, Website, AdditionalInfo,
                     LogoFileName, LogoContentType, LogoData,
                     SignatureFileName, SignatureContentType, SignatureData,
                     CreatedBy, CreatedOn, UpdatedBy, UpdatedOn)
                VALUES
                    (@BusinessName, @CompanyPhoneNumber, @CompanyEmail,
                     @IsGstRegistered, @GstNumber, @PanNumber,
                     @IndustryTypeId, @RegistrationTypeId,
                     @MsmeNumber, @Website, @AdditionalInfo,
                     @LogoFileName, @LogoContentType, @LogoData,
                     @SignatureFileName, @SignatureContentType, @SignatureData,
                     @ActorId, CURRENT_TIMESTAMP, @ActorId, CURRENT_TIMESTAMP);
                SELECT last_insert_rowid();"
            : @"INSERT INTO Businesses
                    (BusinessName, CompanyPhoneNumber, CompanyEmail,
                     IsGstRegistered, GstNumber, PanNumber,
                     IndustryTypeId, RegistrationTypeId,
                     MsmeNumber, Website, AdditionalInfo,
                     LogoFileName, LogoContentType, LogoData,
                     SignatureFileName, SignatureContentType, SignatureData,
                     CreatedBy, CreatedOn, UpdatedBy, UpdatedOn)
                VALUES
                    (@BusinessName, @CompanyPhoneNumber, @CompanyEmail,
                     @IsGstRegistered, @GstNumber, @PanNumber,
                     @IndustryTypeId, @RegistrationTypeId,
                     @MsmeNumber, @Website, @AdditionalInfo,
                     @LogoFileName, @LogoContentType, @LogoData,
                     @SignatureFileName, @SignatureContentType, @SignatureData,
                     @ActorId, SYSUTCDATETIME(), @ActorId, SYSUTCDATETIME());
                SELECT CAST(SCOPE_IDENTITY() AS int);";

        var newId = await connection.ExecuteScalarAsync<long>(new CommandDefinition(insertSql, new
        {
            model.BusinessName,
            model.CompanyPhoneNumber,
            model.CompanyEmail,
            IsGstRegistered = model.IsGstRegistered == true ? 1 : 0,
            model.GstNumber,
            model.PanNumber,
            model.IndustryTypeId,
            model.RegistrationTypeId,
            model.MsmeNumber,
            model.Website,
            model.AdditionalInfo,
            LogoFileName = model.BusinessLogoFile?.FileName,
            LogoContentType = model.BusinessLogoFile?.ContentType,
            LogoData = logoBytes,
            SignatureFileName = model.SignatureFile?.FileName,
            SignatureContentType = model.SignatureFile?.ContentType,
            SignatureData = signatureBytes,
            ActorId = actorId
        }, transaction: transaction, cancellationToken: cancellationToken));

        return checked((int)newId);
    }

    private static async Task UpdateBusinessAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        bool isSqlite,
        int businessId,
        BusinessProfileFormViewModel model,
        int actorId,
        CancellationToken cancellationToken)
    {
        var logoBytes = await ToBytesAsync(model.BusinessLogoFile, cancellationToken);
        var signatureBytes = await ToBytesAsync(model.SignatureFile, cancellationToken);

        // Update core fields. Only update logo/signature if a new file was uploaded.
        var sql = isSqlite
            ? @"UPDATE Businesses
                SET BusinessName = @BusinessName,
                    CompanyPhoneNumber = @CompanyPhoneNumber,
                    CompanyEmail = @CompanyEmail,
                    IsGstRegistered = @IsGstRegistered,
                    GstNumber = @GstNumber,
                    PanNumber = @PanNumber,
                    IndustryTypeId = @IndustryTypeId,
                    RegistrationTypeId = @RegistrationTypeId,
                    MsmeNumber = @MsmeNumber,
                    Website = @Website,
                    AdditionalInfo = @AdditionalInfo,
                    UpdatedBy = @ActorId,
                    UpdatedOn = CURRENT_TIMESTAMP
                WHERE BusinessId = @BusinessId;"
            : @"UPDATE Businesses
                SET BusinessName = @BusinessName,
                    CompanyPhoneNumber = @CompanyPhoneNumber,
                    CompanyEmail = @CompanyEmail,
                    IsGstRegistered = @IsGstRegistered,
                    GstNumber = @GstNumber,
                    PanNumber = @PanNumber,
                    IndustryTypeId = @IndustryTypeId,
                    RegistrationTypeId = @RegistrationTypeId,
                    MsmeNumber = @MsmeNumber,
                    Website = @Website,
                    AdditionalInfo = @AdditionalInfo,
                    UpdatedBy = @ActorId,
                    UpdatedOn = SYSUTCDATETIME()
                WHERE BusinessId = @BusinessId;";

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            BusinessId = businessId,
            model.BusinessName,
            model.CompanyPhoneNumber,
            model.CompanyEmail,
            IsGstRegistered = model.IsGstRegistered == true ? 1 : 0,
            model.GstNumber,
            model.PanNumber,
            model.IndustryTypeId,
            model.RegistrationTypeId,
            model.MsmeNumber,
            model.Website,
            model.AdditionalInfo,
            ActorId = actorId
        }, transaction: transaction, cancellationToken: cancellationToken));

        if (model.BusinessLogoFile is not null && logoBytes is not null)
        {
            var logoSql = isSqlite
                ? @"UPDATE Businesses
                    SET LogoFileName = @FileName,
                        LogoContentType = @ContentType,
                        LogoData = @Data,
                        UpdatedBy = @ActorId,
                        UpdatedOn = CURRENT_TIMESTAMP
                    WHERE BusinessId = @BusinessId;"
                : @"UPDATE Businesses
                    SET LogoFileName = @FileName,
                        LogoContentType = @ContentType,
                        LogoData = @Data,
                        UpdatedBy = @ActorId,
                        UpdatedOn = SYSUTCDATETIME()
                    WHERE BusinessId = @BusinessId;";

            await connection.ExecuteAsync(new CommandDefinition(logoSql, new
            {
                BusinessId = businessId,
                FileName = model.BusinessLogoFile.FileName,
                ContentType = model.BusinessLogoFile.ContentType,
                Data = logoBytes,
                ActorId = actorId
            }, transaction: transaction, cancellationToken: cancellationToken));
        }

        if (model.SignatureFile is not null && signatureBytes is not null)
        {
            var signatureSql = isSqlite
                ? @"UPDATE Businesses
                    SET SignatureFileName = @FileName,
                        SignatureContentType = @ContentType,
                        SignatureData = @Data,
                        UpdatedBy = @ActorId,
                        UpdatedOn = CURRENT_TIMESTAMP
                    WHERE BusinessId = @BusinessId;"
                : @"UPDATE Businesses
                    SET SignatureFileName = @FileName,
                        SignatureContentType = @ContentType,
                        SignatureData = @Data,
                        UpdatedBy = @ActorId,
                        UpdatedOn = SYSUTCDATETIME()
                    WHERE BusinessId = @BusinessId;";

            await connection.ExecuteAsync(new CommandDefinition(signatureSql, new
            {
                BusinessId = businessId,
                FileName = model.SignatureFile.FileName,
                ContentType = model.SignatureFile.ContentType,
                Data = signatureBytes,
                ActorId = actorId
            }, transaction: transaction, cancellationToken: cancellationToken));
        }
    }

    private static async Task UpsertAddressAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        bool isSqlite,
        int businessId,
        BusinessProfileFormViewModel model,
        int actorId,
        CancellationToken cancellationToken)
    {
        const string existsSql = @"SELECT COUNT(1) FROM BusinessAddresses WHERE BusinessId = @BusinessId;";
        var exists = await connection.ExecuteScalarAsync<long>(new CommandDefinition(existsSql, new { BusinessId = businessId }, transaction: transaction, cancellationToken: cancellationToken));

        if (exists > 0)
        {
            var updateSql = isSqlite
                ? @"UPDATE BusinessAddresses
                    SET BillingAddress = @BillingAddress,
                        City = @City,
                        Pincode = @Pincode,
                        StateId = @StateId,
                        UpdatedBy = @ActorId,
                        UpdatedOn = CURRENT_TIMESTAMP
                    WHERE BusinessId = @BusinessId;"
                : @"UPDATE BusinessAddresses
                    SET BillingAddress = @BillingAddress,
                        City = @City,
                        Pincode = @Pincode,
                        StateId = @StateId,
                        UpdatedBy = @ActorId,
                        UpdatedOn = SYSUTCDATETIME()
                    WHERE BusinessId = @BusinessId;";

            await connection.ExecuteAsync(new CommandDefinition(updateSql, new
            {
                BusinessId = businessId,
                model.BillingAddress,
                model.City,
                model.Pincode,
                model.StateId,
                ActorId = actorId
            }, transaction: transaction, cancellationToken: cancellationToken));
        }
        else
        {
            var insertSql = isSqlite
                ? @"INSERT INTO BusinessAddresses
                        (BusinessId, BillingAddress, City, Pincode, StateId, CreatedBy, CreatedOn, UpdatedBy, UpdatedOn)
                    VALUES
                        (@BusinessId, @BillingAddress, @City, @Pincode, @StateId, @ActorId, CURRENT_TIMESTAMP, @ActorId, CURRENT_TIMESTAMP);"
                : @"INSERT INTO BusinessAddresses
                        (BusinessId, BillingAddress, City, Pincode, StateId, CreatedBy, CreatedOn, UpdatedBy, UpdatedOn)
                    VALUES
                        (@BusinessId, @BillingAddress, @City, @Pincode, @StateId, @ActorId, SYSUTCDATETIME(), @ActorId, SYSUTCDATETIME());";

            await connection.ExecuteAsync(new CommandDefinition(insertSql, new
            {
                BusinessId = businessId,
                model.BillingAddress,
                model.City,
                model.Pincode,
                model.StateId,
                ActorId = actorId
            }, transaction: transaction, cancellationToken: cancellationToken));
        }
    }

    private static async Task ReplaceBusinessTypesAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        bool isSqlite,
        int businessId,
        IEnumerable<int> businessTypeIds,
        CancellationToken cancellationToken)
    {
        const string deleteSql = @"DELETE FROM BusinessBusinessTypes WHERE BusinessId = @BusinessId;";
        await connection.ExecuteAsync(new CommandDefinition(deleteSql, new { BusinessId = businessId }, transaction: transaction, cancellationToken: cancellationToken));

        var ids = businessTypeIds?.Distinct().Where(x => x > 0).ToList() ?? new List<int>();
        if (ids.Count == 0)
        {
            return;
        }

        var insertSql = isSqlite
            ? @"INSERT OR IGNORE INTO BusinessBusinessTypes (BusinessId, BusinessTypeId)
                VALUES (@BusinessId, @BusinessTypeId);"
            : @"INSERT INTO BusinessBusinessTypes (BusinessId, BusinessTypeId)
                VALUES (@BusinessId, @BusinessTypeId);";

        foreach (var id in ids)
        {
            await connection.ExecuteAsync(new CommandDefinition(insertSql, new { BusinessId = businessId, BusinessTypeId = id }, transaction: transaction, cancellationToken: cancellationToken));
        }
    }

    private static async Task<byte[]?> ToBytesAsync(Microsoft.AspNetCore.Http.IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return null;
        }

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        return stream.ToArray();
    }

    private sealed class BusinessRow
    {
        public int BusinessId { get; set; }
        public string? BusinessName { get; set; }
        public string? CompanyPhoneNumber { get; set; }
        public string? CompanyEmail { get; set; }
        public int IsGstRegistered { get; set; }
        public string? GstNumber { get; set; }
        public string? PanNumber { get; set; }
        public int? IndustryTypeId { get; set; }
        public int? RegistrationTypeId { get; set; }
        public string? MsmeNumber { get; set; }
        public string? Website { get; set; }
        public string? AdditionalInfo { get; set; }
    }

    private sealed class AddressRow
    {
        public int BusinessAddressId { get; set; }
        public int BusinessId { get; set; }
        public string? BillingAddress { get; set; }
        public string? City { get; set; }
        public string? Pincode { get; set; }
        public int? StateId { get; set; }
    }
}

