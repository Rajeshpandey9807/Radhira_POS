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
        const string sql = @"SELECT Id, RegistrationTypeName, IsActive
                             FROM RegistrationTypes
                             ORDER BY RegistrationTypeName";

        var result = await connection.QueryAsync<RegistrationTypeListItem>(sql);
        return result.ToList();
    }

    public async Task<RegistrationTypeDetails?> GetByIdAsync(Guid id)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"SELECT Id, RegistrationTypeName, IsActive
                             FROM RegistrationTypes
                             WHERE Id = @Id";

        return await connection.QuerySingleOrDefaultAsync<RegistrationTypeDetails>(sql, new { Id = id.ToString() });
    }

    public async Task CreateAsync(RegistrationTypeInput input, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"INSERT INTO RegistrationTypes (Id, RegistrationTypeName, IsActive)
                             VALUES (@Id, @RegistrationTypeName, @IsActive)";

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = Guid.NewGuid().ToString(),
            RegistrationTypeName = input.RegistrationTypeName.Trim(),
            IsActive = input.IsActive ? 1 : 0
        }, cancellationToken: cancellationToken));
    }

    public async Task UpdateAsync(Guid id, RegistrationTypeInput input, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"UPDATE RegistrationTypes
                             SET RegistrationTypeName = @RegistrationTypeName,
                                 IsActive = @IsActive,
                                 UpdatedAt = CURRENT_TIMESTAMP
                             WHERE Id = @Id";

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id.ToString(),
            RegistrationTypeName = input.RegistrationTypeName.Trim(),
            IsActive = input.IsActive ? 1 : 0
        }, cancellationToken: cancellationToken));
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = "DELETE FROM RegistrationTypes WHERE Id = @Id";
        var affected = await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id.ToString() }, cancellationToken: cancellationToken));
        return affected > 0;
    }
}
