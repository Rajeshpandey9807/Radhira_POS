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
        var sqlServerSchema = isSqlite ? (SqlServerBusinessSchema?)null : await GetSqlServerBusinessSchemaAsync(connection, cancellationToken);

        var businessSql = isSqlite
            ? @"SELECT BusinessId, BusinessName, CompanyPhoneNumber, CompanyEmail,
                       IsGstRegistered, GstNumber, PanNumber,
                       IndustryTypeId, RegistrationTypeId,
                       MsmeNumber, Website, AdditionalInfo
                FROM Businesses
                ORDER BY BusinessId DESC
                LIMIT 1;"
            : BuildSqlServerLatestBusinessSelect(sqlServerSchema ?? SqlServerBusinessSchema.Unknown, top: 1);

        var business = await connection.QuerySingleOrDefaultAsync<BusinessRow>(
            new CommandDefinition(businessSql, cancellationToken: cancellationToken));

        if (business is null)
        {
            return null;
        }

        AddressRow? address = null;
        if (isSqlite)
        {
            const string addressSql = @"SELECT BusinessAddressId, BusinessId, BillingAddress, City, Pincode, StateId
                                        FROM BusinessAddresses
                                        WHERE BusinessId = @BusinessId";
            address = await connection.QuerySingleOrDefaultAsync<AddressRow>(
                new CommandDefinition(addressSql, new { business.BusinessId }, cancellationToken: cancellationToken));
        }
        else
        {
            if (sqlServerSchema?.HasBusinessAddressesTable == true)
            {
                const string addressSql = @"SELECT BusinessAddressId, BusinessId, BillingAddress, City, Pincode, StateId
                                            FROM BusinessAddresses
                                            WHERE BusinessId = @BusinessId";
                address = await connection.QuerySingleOrDefaultAsync<AddressRow>(
                    new CommandDefinition(addressSql, new { business.BusinessId }, cancellationToken: cancellationToken));
            }
        }

        var businessTypeIds = new List<int>();
        if (isSqlite)
        {
            const string typesSql = @"SELECT BusinessTypeId
                                      FROM BusinessBusinessTypes
                                      WHERE BusinessId = @BusinessId
                                      ORDER BY BusinessTypeId";
            businessTypeIds = (await connection.QueryAsync<int>(
                new CommandDefinition(typesSql, new { business.BusinessId }, cancellationToken: cancellationToken))).ToList();
        }
        else
        {
            if (sqlServerSchema?.HasBusinessBusinessTypesTable == true)
            {
                const string typesSql = @"SELECT BusinessTypeId
                                          FROM BusinessBusinessTypes
                                          WHERE BusinessId = @BusinessId
                                          ORDER BY BusinessTypeId";
                businessTypeIds = (await connection.QueryAsync<int>(
                    new CommandDefinition(typesSql, new { business.BusinessId }, cancellationToken: cancellationToken))).ToList();
            }
            else if (sqlServerSchema?.HasBusinessTypeIdColumn == true && business.BusinessTypeId.HasValue && business.BusinessTypeId.Value > 0)
            {
                businessTypeIds.Add(business.BusinessTypeId.Value);
            }
        }

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
            Pincode = address?.Pincode ?? business.Pincode,
            StateId = address?.StateId ?? business.StateId,
            SelectedBusinessTypeIds = businessTypeIds
        };
    }

    public async Task<int> SaveAsync(BusinessProfileFormViewModel model, int actorId, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        var isSqlite = connection.GetType().Name.Contains("Sqlite", StringComparison.OrdinalIgnoreCase);
        var sqlServerSchema = isSqlite ? null : await GetSqlServerBusinessSchemaAsync(connection, cancellationToken);
        using var transaction = connection.BeginTransaction();

        try
        {
            var businessId = model.BusinessId;
            if (businessId is null)
            {
                businessId = await InsertBusinessAsync(connection, transaction, isSqlite, sqlServerSchema, model, actorId, cancellationToken);
            }
            else
            {
                await UpdateBusinessAsync(connection, transaction, isSqlite, sqlServerSchema, businessId.Value, model, actorId, cancellationToken);
            }

            if (isSqlite || sqlServerSchema?.HasBusinessAddressesTable == true)
            {
                await UpsertAddressAsync(connection, transaction, isSqlite, businessId.Value, model, actorId, cancellationToken);
            }

            if (isSqlite || sqlServerSchema?.HasBusinessBusinessTypesTable == true)
            {
                await ReplaceBusinessTypesAsync(connection, transaction, isSqlite, businessId.Value, model.SelectedBusinessTypeIds, cancellationToken);
            }

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
        SqlServerBusinessSchema? sqlServerSchema,
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
            : BuildSqlServerInsertBusiness(sqlServerSchema ?? SqlServerBusinessSchema.Unknown, model, hasNewLogo: logoBytes is not null, hasNewSignature: signatureBytes is not null);

        var parameters = new DynamicParameters();
        parameters.Add("BusinessName", model.BusinessName);
        parameters.Add("CompanyPhoneNumber", model.CompanyPhoneNumber);
        parameters.Add("CompanyEmail", model.CompanyEmail);
        parameters.Add("IsGstRegistered", model.IsGstRegistered == true ? 1 : 0);
        parameters.Add("GstNumber", model.GstNumber);
        parameters.Add("PanNumber", model.PanNumber);
        parameters.Add("IndustryTypeId", model.IndustryTypeId);
        parameters.Add("RegistrationTypeId", model.RegistrationTypeId);
        parameters.Add("MsmeNumber", model.MsmeNumber);
        parameters.Add("Website", model.Website);
        parameters.Add("AdditionalInfo", model.AdditionalInfo);
        parameters.Add("StateId", model.StateId);
        parameters.Add("Pincode", model.Pincode);
        parameters.Add("BusinessTypeId", FirstOrNull(model.SelectedBusinessTypeIds));
        parameters.Add("LogoFileName", model.BusinessLogoFile?.FileName);
        parameters.Add("LogoContentType", model.BusinessLogoFile?.ContentType);
        parameters.Add("LogoData", logoBytes);
        parameters.Add("BusinessLogo", logoBytes);
        parameters.Add("SignatureFileName", model.SignatureFile?.FileName);
        parameters.Add("SignatureContentType", model.SignatureFile?.ContentType);
        parameters.Add("SignatureData", signatureBytes);
        parameters.Add("Signature", signatureBytes);
        parameters.Add("ActorId", actorId);

        var newId = await connection.ExecuteScalarAsync<long>(new CommandDefinition(insertSql, parameters, transaction: transaction, cancellationToken: cancellationToken));

        return checked((int)newId);
    }

    private static async Task UpdateBusinessAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        bool isSqlite,
        SqlServerBusinessSchema? sqlServerSchema,
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
            : BuildSqlServerUpdateBusiness(sqlServerSchema ?? SqlServerBusinessSchema.Unknown, hasNewLogo: model.BusinessLogoFile is not null && logoBytes is not null, hasNewSignature: model.SignatureFile is not null && signatureBytes is not null);

        var parameters = new DynamicParameters();
        parameters.Add("BusinessId", businessId);
        parameters.Add("BusinessName", model.BusinessName);
        parameters.Add("CompanyPhoneNumber", model.CompanyPhoneNumber);
        parameters.Add("CompanyEmail", model.CompanyEmail);
        parameters.Add("IsGstRegistered", model.IsGstRegistered == true ? 1 : 0);
        parameters.Add("GstNumber", model.GstNumber);
        parameters.Add("PanNumber", model.PanNumber);
        parameters.Add("IndustryTypeId", model.IndustryTypeId);
        parameters.Add("RegistrationTypeId", model.RegistrationTypeId);
        parameters.Add("MsmeNumber", model.MsmeNumber);
        parameters.Add("Website", model.Website);
        parameters.Add("AdditionalInfo", model.AdditionalInfo);
        parameters.Add("StateId", model.StateId);
        parameters.Add("Pincode", model.Pincode);
        parameters.Add("BusinessTypeId", FirstOrNull(model.SelectedBusinessTypeIds));
        parameters.Add("LogoFileName", model.BusinessLogoFile?.FileName);
        parameters.Add("LogoContentType", model.BusinessLogoFile?.ContentType);
        parameters.Add("LogoData", logoBytes);
        parameters.Add("BusinessLogo", logoBytes);
        parameters.Add("SignatureFileName", model.SignatureFile?.FileName);
        parameters.Add("SignatureContentType", model.SignatureFile?.ContentType);
        parameters.Add("SignatureData", signatureBytes);
        parameters.Add("Signature", signatureBytes);
        parameters.Add("ActorId", actorId);

        await connection.ExecuteAsync(new CommandDefinition(sql, parameters, transaction: transaction, cancellationToken: cancellationToken));

        if (isSqlite && model.BusinessLogoFile is not null && logoBytes is not null)
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

        if (isSqlite && model.SignatureFile is not null && signatureBytes is not null)
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
        public bool IsGstRegistered { get; set; }
        public string? GstNumber { get; set; }
        public string? PanNumber { get; set; }
        public int? IndustryTypeId { get; set; }
        public int? RegistrationTypeId { get; set; }
        public string? MsmeNumber { get; set; }
        public string? Website { get; set; }
        public string? AdditionalInfo { get; set; }
        public int? BusinessTypeId { get; set; }
        public int? StateId { get; set; }
        public string? Pincode { get; set; }
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

    private static int? FirstOrNull(IEnumerable<int>? values)
    {
        if (values is null)
        {
            return null;
        }

        foreach (var value in values)
        {
            if (value > 0)
            {
                return value;
            }
        }

        return null;
    }

    private static string BuildSqlServerLatestBusinessSelect(SqlServerBusinessSchema schema, int top)
    {
        // Only select columns that exist to avoid "Invalid column name" in externally managed schemas.
        var columns = new List<string>
        {
            "BusinessId",
            "BusinessName",
            "CompanyPhoneNumber",
            "CompanyEmail",
            "IsGstRegistered",
            "GstNumber",
            "PanNumber",
            "IndustryTypeId",
            "RegistrationTypeId",
            "MsmeNumber",
            "Website",
            "AdditionalInfo"
        };

        if (schema.HasBusinessTypeIdColumn)
        {
            columns.Add("BusinessTypeId");
        }

        if (schema.HasStateIdColumn)
        {
            columns.Add("StateId");
        }

        if (schema.HasPincodeColumn)
        {
            columns.Add("Pincode");
        }

        return $@"SELECT TOP {top} {string.Join(", ", columns)}
                FROM Businesses
                ORDER BY BusinessId DESC;";
    }

    private static string BuildSqlServerInsertBusiness(SqlServerBusinessSchema schema, BusinessProfileFormViewModel model, bool hasNewLogo, bool hasNewSignature)
    {
        // Insert into externally managed SQL Server schemas by only using columns that exist.
        var columns = new List<string>
        {
            "BusinessName",
            "CompanyPhoneNumber",
            "CompanyEmail",
            "IsGstRegistered",
            "GstNumber",
            "PanNumber",
            "IndustryTypeId",
            "RegistrationTypeId",
            "MsmeNumber",
            "Website",
            "AdditionalInfo"
        };
        var values = new List<string>
        {
            "@BusinessName",
            "@CompanyPhoneNumber",
            "@CompanyEmail",
            "@IsGstRegistered",
            "@GstNumber",
            "@PanNumber",
            "@IndustryTypeId",
            "@RegistrationTypeId",
            "@MsmeNumber",
            "@Website",
            "@AdditionalInfo"
        };

        if (schema.HasBusinessTypeIdColumn)
        {
            columns.Add("BusinessTypeId");
            values.Add("@BusinessTypeId");
        }

        if (schema.HasStateIdColumn)
        {
            columns.Add("StateId");
            values.Add("@StateId");
        }

        if (schema.HasPincodeColumn)
        {
            columns.Add("Pincode");
            values.Add("@Pincode");
        }

        if (schema.HasDetailedLogoColumns)
        {
            columns.AddRange(new[] { "LogoFileName", "LogoContentType", "LogoData" });
            values.AddRange(new[] { "@LogoFileName", "@LogoContentType", "@LogoData" });
        }
        else if (schema.HasBusinessLogoColumn)
        {
            columns.Add("BusinessLogo");
            values.Add("@BusinessLogo");
        }

        if (schema.HasDetailedSignatureColumns)
        {
            columns.AddRange(new[] { "SignatureFileName", "SignatureContentType", "SignatureData" });
            values.AddRange(new[] { "@SignatureFileName", "@SignatureContentType", "@SignatureData" });
        }
        else if (schema.HasSignatureColumn)
        {
            columns.Add("Signature");
            values.Add("@Signature");
        }

        columns.AddRange(new[] { "CreatedBy", "CreatedOn", "UpdatedBy", "UpdatedOn" });
        values.AddRange(new[] { "@ActorId", "SYSUTCDATETIME()", "@ActorId", "SYSUTCDATETIME()" });

        // If the schema supports logo/signature columns but the user didn't upload files, we still insert NULLs; that's OK.
        _ = hasNewLogo;
        _ = hasNewSignature;
        _ = model;

        return $@"INSERT INTO Businesses ({string.Join(", ", columns)})
                VALUES ({string.Join(", ", values)});
                SELECT CAST(SCOPE_IDENTITY() AS int);";
    }

    private static string BuildSqlServerUpdateBusiness(SqlServerBusinessSchema schema, bool hasNewLogo, bool hasNewSignature)
    {
        // Update externally managed SQL Server schemas by only touching columns that exist.
        var sets = new List<string>
        {
            "BusinessName = @BusinessName",
            "CompanyPhoneNumber = @CompanyPhoneNumber",
            "CompanyEmail = @CompanyEmail",
            "IsGstRegistered = @IsGstRegistered",
            "GstNumber = @GstNumber",
            "PanNumber = @PanNumber",
            "IndustryTypeId = @IndustryTypeId",
            "RegistrationTypeId = @RegistrationTypeId",
            "MsmeNumber = @MsmeNumber",
            "Website = @Website",
            "AdditionalInfo = @AdditionalInfo"
        };

        if (schema.HasBusinessTypeIdColumn)
        {
            sets.Add("BusinessTypeId = @BusinessTypeId");
        }

        if (schema.HasStateIdColumn)
        {
            sets.Add("StateId = @StateId");
        }

        if (schema.HasPincodeColumn)
        {
            sets.Add("Pincode = @Pincode");
        }

        if (hasNewLogo)
        {
            if (schema.HasDetailedLogoColumns)
            {
                sets.Add("LogoFileName = @LogoFileName");
                sets.Add("LogoContentType = @LogoContentType");
                sets.Add("LogoData = @LogoData");
            }
            else if (schema.HasBusinessLogoColumn)
            {
                sets.Add("BusinessLogo = @BusinessLogo");
            }
        }

        if (hasNewSignature)
        {
            if (schema.HasDetailedSignatureColumns)
            {
                sets.Add("SignatureFileName = @SignatureFileName");
                sets.Add("SignatureContentType = @SignatureContentType");
                sets.Add("SignatureData = @SignatureData");
            }
            else if (schema.HasSignatureColumn)
            {
                sets.Add("Signature = @Signature");
            }
        }

        sets.Add("UpdatedBy = @ActorId");
        sets.Add("UpdatedOn = SYSUTCDATETIME()");

        return $@"UPDATE Businesses
                SET {string.Join(",\n                    ", sets)}
                WHERE BusinessId = @BusinessId;";
    }

    private readonly record struct SqlServerBusinessSchema(
        bool HasDetailedLogoColumns,
        bool HasDetailedSignatureColumns,
        bool HasBusinessLogoColumn,
        bool HasSignatureColumn,
        bool HasBusinessTypeIdColumn,
        bool HasStateIdColumn,
        bool HasPincodeColumn,
        bool HasBusinessAddressesTable,
        bool HasBusinessBusinessTypesTable)
    {
        public static SqlServerBusinessSchema Unknown => new(
            HasDetailedLogoColumns: true,
            HasDetailedSignatureColumns: true,
            HasBusinessLogoColumn: false,
            HasSignatureColumn: false,
            HasBusinessTypeIdColumn: false,
            HasStateIdColumn: false,
            HasPincodeColumn: false,
            HasBusinessAddressesTable: true,
            HasBusinessBusinessTypesTable: true);
    }

    private static async Task<SqlServerBusinessSchema> GetSqlServerBusinessSchemaAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        // Cache is intentionally omitted (schema checks are cheap vs requests volume in this app).
        const string columnsSql = @"
SELECT COLUMN_NAME
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'Businesses';";

        var columnNames = (await connection.QueryAsync<string>(
            new CommandDefinition(columnsSql, cancellationToken: cancellationToken))).ToHashSet(StringComparer.OrdinalIgnoreCase);

        const string tablesSql = @"
SELECT
    CASE WHEN OBJECT_ID('dbo.BusinessAddresses', 'U') IS NULL THEN 0 ELSE 1 END AS HasBusinessAddresses,
    CASE WHEN OBJECT_ID('dbo.BusinessBusinessTypes', 'U') IS NULL THEN 0 ELSE 1 END AS HasBusinessBusinessTypes;";

        var tables = await connection.QuerySingleAsync<(int HasBusinessAddresses, int HasBusinessBusinessTypes)>(
            new CommandDefinition(tablesSql, cancellationToken: cancellationToken));

        var hasDetailedLogo = columnNames.Contains("LogoFileName")
                             && columnNames.Contains("LogoContentType")
                             && columnNames.Contains("LogoData");
        var hasDetailedSignature = columnNames.Contains("SignatureFileName")
                                  && columnNames.Contains("SignatureContentType")
                                  && columnNames.Contains("SignatureData");

        return new SqlServerBusinessSchema(
            HasDetailedLogoColumns: hasDetailedLogo,
            HasDetailedSignatureColumns: hasDetailedSignature,
            HasBusinessLogoColumn: columnNames.Contains("BusinessLogo"),
            HasSignatureColumn: columnNames.Contains("Signature"),
            HasBusinessTypeIdColumn: columnNames.Contains("BusinessTypeId"),
            HasStateIdColumn: columnNames.Contains("StateId"),
            HasPincodeColumn: columnNames.Contains("Pincode"),
            HasBusinessAddressesTable: tables.HasBusinessAddresses == 1,
            HasBusinessBusinessTypesTable: tables.HasBusinessBusinessTypes == 1);
    }
}

