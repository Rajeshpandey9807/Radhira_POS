using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using PosApp.Web.Data;

namespace PosApp.Web.Features.Categories;

public sealed class CategoryService
{
    private readonly IDbConnectionFactory _connectionFactory;

    public CategoryService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<CategoryListItem>> GetAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"SELECT CategoryId, CategoryName, Color, IsActive, CreatedBy, CreatedOn, UpdatedBy, UpdatedOn
                             FROM Categories
                             ORDER BY CategoryName";

        var result = await connection.QueryAsync<CategoryListItem>(sql);
        return result.ToList();
    }

    public async Task<CategoryDetails?> GetByIdAsync(Guid id)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"SELECT CategoryId, CategoryName, Color, IsActive
                             FROM Categories
                             WHERE CategoryId = @Id";

        return await connection.QuerySingleOrDefaultAsync<CategoryDetails>(sql, new { Id = id });
    }

    public async Task CreateAsync(CategoryInput input, int createdBy, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        var categoryId = Guid.NewGuid();
        const string sql = @"INSERT INTO Categories (CategoryId, CategoryName, Color, IsActive, CreatedBy, CreatedOn)
                             VALUES (@CategoryId, @CategoryName, @Color, 1, @CreatedBy, CURRENT_TIMESTAMP)";

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            CategoryId = categoryId,
            CategoryName = input.CategoryName.Trim(),
            Color = string.IsNullOrWhiteSpace(input.Color) ? null : input.Color.Trim(),
            CreatedBy = createdBy
        }, cancellationToken: cancellationToken));
    }

    public async Task UpdateAsync(Guid id, CategoryInput input, int updatedBy, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"UPDATE Categories
                             SET CategoryName = @CategoryName,
                                 Color = @Color,
                                 UpdatedBy = @UpdatedBy,
                                 UpdatedOn = CURRENT_TIMESTAMP
                             WHERE CategoryId = @Id";

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id,
            CategoryName = input.CategoryName.Trim(),
            Color = string.IsNullOrWhiteSpace(input.Color) ? null : input.Color.Trim(),
            UpdatedBy = updatedBy
        }, cancellationToken: cancellationToken));
    }

    public async Task<bool> SetStatusAsync(Guid id, bool isActive, int updatedBy, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"UPDATE Categories
                             SET IsActive = @IsActive,
                                 UpdatedBy = @UpdatedBy,
                                 UpdatedOn = CURRENT_TIMESTAMP
                             WHERE CategoryId = @Id";
        var affected = await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id,
            IsActive = isActive ? 1 : 0,
            UpdatedBy = updatedBy
        }, cancellationToken: cancellationToken));
        return affected > 0;
    }
}

