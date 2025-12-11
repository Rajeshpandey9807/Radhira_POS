using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using PosApp.Web.Data;

namespace PosApp.Web.Features.RegistrationTypes;

public sealed class RegistrationTypeService
{
    private readonly IDbConnectionFactory _connectionFactory;

    public RegistrationTypeService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<RegistrationTypeListItem>> GetAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"SELECT RegistrationTypeId, RegistrationTypeName, IsActive, CreatedBy, CreatedOn, UpdatedBy, UpdatedOn
                             FROM RegistrationTypes
                             ORDER BY RegistrationTypeName";

        var result = await connection.QueryAsync<RegistrationTypeListItem>(sql);
        return result.ToList();
    }

    public async Task<RegistrationTypeDetails?> GetByIdAsync(int id)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"SELECT RegistrationTypeId, RegistrationTypeName, IsActive
                             FROM RegistrationTypes
                             WHERE RegistrationTypeId = @Id";

        return await connection.QuerySingleOrDefaultAsync<RegistrationTypeDetails>(sql, new { Id = id });
    }

    public async Task CreateAsync(RegistrationTypeInput input, int createdBy, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"INSERT INTO RegistrationTypes (RegistrationTypeName, IsActive, CreatedBy, CreatedOn)
                             VALUES (@RegistrationTypeName, 1, @CreatedBy, CURRENT_TIMESTAMP)";

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            RegistrationTypeName = input.RegistrationTypeName.Trim(),
            CreatedBy = createdBy
        }, cancellationToken: cancellationToken));
    }

    public async Task UpdateAsync(int id, RegistrationTypeInput input, int updatedBy, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"UPDATE RegistrationTypes
                             SET RegistrationTypeName = @RegistrationTypeName,
                                 UpdatedBy = @UpdatedBy,
                                 UpdatedOn = CURRENT_TIMESTAMP
                             WHERE RegistrationTypeId = @Id";

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id,
            RegistrationTypeName = input.RegistrationTypeName.Trim(),
            UpdatedBy = updatedBy
        }, cancellationToken: cancellationToken));
    }

    public async Task<bool> SetStatusAsync(int id, bool isActive, int updatedBy, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"UPDATE RegistrationTypes
                             SET IsActive = @IsActive,
                                 UpdatedBy = @UpdatedBy,
                                 UpdatedOn = CURRENT_TIMESTAMP
                             WHERE RegistrationTypeId = @Id";

        var affected = await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id,
            IsActive = isActive ? 1 : 0,
            UpdatedBy = updatedBy
        }, cancellationToken: cancellationToken));

        return affected > 0;
    }
}
