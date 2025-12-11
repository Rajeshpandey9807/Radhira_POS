using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using PosApp.Web.Data;

namespace PosApp.Web.Features.IndustryTypes;

public sealed class IndustryTypeService
{
    private readonly IDbConnectionFactory _connectionFactory;

    public IndustryTypeService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<IndustryTypeListItem>> GetAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"SELECT IndustryTypeId, IndustryTypeName, IsActive, CreatedBy, CreatedOn, UpdatedBy, UpdatedOn
                             FROM IndustryTypes
                             ORDER BY IndustryTypeName";

        var result = await connection.QueryAsync<IndustryTypeListItem>(sql);
        return result.ToList();
    }

    public async Task<IndustryTypeDetails?> GetByIdAsync(int id)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"SELECT IndustryTypeId, IndustryTypeName, IsActive
                             FROM IndustryTypes
                             WHERE IndustryTypeId = @Id";

        return await connection.QuerySingleOrDefaultAsync<IndustryTypeDetails>(sql, new { Id = id });
    }

    public async Task CreateAsync(IndustryTypeInput input, int createdBy, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"INSERT INTO IndustryTypes (IndustryTypeName, IsActive, CreatedBy, CreatedOn)
                             VALUES (@IndustryTypeName, 1, @CreatedBy, CURRENT_TIMESTAMP)";

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            IndustryTypeName = input.IndustryTypeName.Trim(),
            CreatedBy = createdBy
        }, cancellationToken: cancellationToken));
    }

    public async Task UpdateAsync(int id, IndustryTypeInput input, int updatedBy, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"UPDATE IndustryTypes
                             SET IndustryTypeName = @IndustryTypeName,
                                 UpdatedBy = @UpdatedBy,
                                 UpdatedOn = CURRENT_TIMESTAMP
                             WHERE IndustryTypeId = @Id";

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id,
            IndustryTypeName = input.IndustryTypeName.Trim(),
            UpdatedBy = updatedBy
        }, cancellationToken: cancellationToken));
    }

    public async Task<bool> DeactivateAsync(int id, int updatedBy, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"UPDATE IndustryTypes
                             SET IsActive = 0,
                                 UpdatedBy = @UpdatedBy,
                                 UpdatedOn = CURRENT_TIMESTAMP
                             WHERE IndustryTypeId = @Id";

        var affected = await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id,
            UpdatedBy = updatedBy
        }, cancellationToken: cancellationToken));

        return affected > 0;
    }
}
