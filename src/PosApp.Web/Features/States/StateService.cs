using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using PosApp.Web.Data;

namespace PosApp.Web.Features.States;

public sealed class StateService
{
    private readonly IDbConnectionFactory _connectionFactory;

    public StateService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<StateListItem>> GetAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"SELECT StateId, StateName, IsActive, CreatedBy, CreatedOn, UpdatedBy, UpdatedOn
                             FROM States
                             ORDER BY StateName";

        var result = await connection.QueryAsync<StateListItem>(sql);
        return result.ToList();
    }

    public async Task<StateDetails?> GetByIdAsync(int id)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"SELECT StateId, StateName, IsActive
                             FROM States
                             WHERE StateId = @Id";

        return await connection.QuerySingleOrDefaultAsync<StateDetails>(sql, new { Id = id });
    }

    public async Task CreateAsync(StateInput input, int createdBy, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"INSERT INTO States (StateName, IsActive, CreatedBy, CreatedOn)
                             VALUES (@StateName, 1, @CreatedBy, CURRENT_TIMESTAMP)";

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            StateName = input.StateName.Trim(),
            CreatedBy = createdBy
        }, cancellationToken: cancellationToken));
    }

    public async Task UpdateAsync(int id, StateInput input, int updatedBy, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"UPDATE States
                             SET StateName = @StateName,
                                 UpdatedBy = @UpdatedBy,
                                 UpdatedOn = CURRENT_TIMESTAMP
                             WHERE StateId = @Id";

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id,
            StateName = input.StateName.Trim(),
            UpdatedBy = updatedBy
        }, cancellationToken: cancellationToken));
    }

    public async Task<bool> SetStatusAsync(int id, bool isActive, int updatedBy, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"UPDATE States
                             SET IsActive = @IsActive,
                                 UpdatedBy = @UpdatedBy,
                                 UpdatedOn = CURRENT_TIMESTAMP
                             WHERE StateId = @Id";

        var affected = await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id,
            IsActive = isActive ? 1 : 0,
            UpdatedBy = updatedBy
        }, cancellationToken: cancellationToken));

        return affected > 0;
    }
}
