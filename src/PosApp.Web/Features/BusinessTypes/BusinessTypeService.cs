using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using PosApp.Web.Data;

namespace PosApp.Web.Features.BusinessTypes;

public sealed class BusinessTypeService
{
    private readonly IDbConnectionFactory _connectionFactory;

    public BusinessTypeService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<BusinessTypeListItem>> GetAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"SELECT BusinessTypeId, BusinessTypeName, IsActive, CreatedBy, CreatedOn, UpdatedBy, UpdatedOn
                             FROM BusinessTypes
                             ORDER BY BusinessTypeName";

        var result = await connection.QueryAsync<BusinessTypeListItem>(sql);
        return result.ToList();
    }

    public async Task<BusinessTypeDetails?> GetByIdAsync(Guid id)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"SELECT BusinessTypeId, BusinessTypeName, IsActive
                             FROM BusinessTypes
                             WHERE BusinessTypeId = @Id";

        return await connection.QuerySingleOrDefaultAsync<BusinessTypeDetails>(sql, new { Id = id.ToString() });
    }

    public async Task CreateAsync(BusinessTypeInput input, string createdBy, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"INSERT INTO BusinessTypes (BusinessTypeId, BusinessTypeName, IsActive, CreatedBy, CreatedOn)
                             VALUES (@Id, @BusinessTypeName, 1, @CreatedBy, CURRENT_TIMESTAMP)";

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = Guid.NewGuid().ToString(),
            BusinessTypeName = input.BusinessTypeName.Trim(),
            CreatedBy = createdBy
        }, cancellationToken: cancellationToken));
    }

    public async Task UpdateAsync(Guid id, BusinessTypeInput input, string updatedBy, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"UPDATE BusinessTypes
                             SET BusinessTypeName = @BusinessTypeName,
                                 UpdatedBy = @UpdatedBy,
                                 UpdatedOn = CURRENT_TIMESTAMP
                             WHERE BusinessTypeId = @Id";

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id.ToString(),
            BusinessTypeName = input.BusinessTypeName.Trim(),
            UpdatedBy = updatedBy
        }, cancellationToken: cancellationToken));
    }

    public async Task<bool> DeactivateAsync(Guid id, string updatedBy, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"UPDATE BusinessTypes
                             SET IsActive = 0,
                                 UpdatedBy = @UpdatedBy,
                                 UpdatedOn = CURRENT_TIMESTAMP
                             WHERE BusinessTypeId = @Id";
        var affected = await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id.ToString(),
            UpdatedBy = updatedBy
        }, cancellationToken: cancellationToken));
        return affected > 0;
    }
}
