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
        const string sql = @"SELECT Id, IndustryTypeName, IsActive
                             FROM BusinessTypes
                             ORDER BY IndustryTypeName";

        var result = await connection.QueryAsync<BusinessTypeListItem>(sql);
        return result.ToList();
    }

    public async Task<BusinessTypeDetails?> GetByIdAsync(Guid id)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"SELECT Id, IndustryTypeName, IsActive
                             FROM BusinessTypes
                             WHERE Id = @Id";

        return await connection.QuerySingleOrDefaultAsync<BusinessTypeDetails>(sql, new { Id = id.ToString() });
    }

    public async Task CreateAsync(BusinessTypeInput input, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"INSERT INTO BusinessTypes (Id, IndustryTypeName, IsActive)
                             VALUES (@Id, @IndustryTypeName, @IsActive)";

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = Guid.NewGuid().ToString(),
            IndustryTypeName = input.IndustryTypeName.Trim(),
            IsActive = input.IsActive ? 1 : 0
        }, cancellationToken: cancellationToken));
    }

    public async Task UpdateAsync(Guid id, BusinessTypeInput input, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"UPDATE BusinessTypes
                             SET IndustryTypeName = @IndustryTypeName,
                                 IsActive = @IsActive,
                                 UpdatedAt = CURRENT_TIMESTAMP
                             WHERE Id = @Id";

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id.ToString(),
            IndustryTypeName = input.IndustryTypeName.Trim(),
            IsActive = input.IsActive ? 1 : 0
        }, cancellationToken: cancellationToken));
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = "DELETE FROM BusinessTypes WHERE Id = @Id";
        var affected = await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id.ToString() }, cancellationToken: cancellationToken));
        return affected > 0;
    }
}
