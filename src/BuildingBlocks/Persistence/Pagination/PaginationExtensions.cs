using FSH.Framework.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace FSH.Framework.Persistence;

public static class PaginationExtensions
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public static Task<PagedResponse<T>> ToPagedResponseAsync<T>(
        this IQueryable<T> source,
        IPagedQuery pagination,
        CancellationToken cancellationToken = default)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(pagination);

        var pageNumber = pagination.PageNumber is null or <= 0
            ? 1
            : pagination.PageNumber.Value;

        var pageSize = pagination.PageSize is null or <= 0
            ? DefaultPageSize
            : pagination.PageSize.Value;

        if (pageSize > MaxPageSize)
        {
            pageSize = MaxPageSize;
        }

        // Pagination is intentionally decoupled from specifications; the incoming
        // source is expected to already have any required ordering applied via
        // specifications or explicit ordering at call sites.
        return ToPagedResponseInternalAsync(source, pageNumber, pageSize, cancellationToken);
    }

    private static async Task<PagedResponse<T>> ToPagedResponseInternalAsync<T>(
        IQueryable<T> source,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
        where T : class
    {
        var totalCount = await source.LongCountAsync(cancellationToken).ConfigureAwait(false);

        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)pageSize);

        if (pageNumber > totalPages && totalPages > 0)
        {
            pageNumber = totalPages;
        }

        var skip = (pageNumber - 1) * pageSize;

        var items = await source
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResponse<T>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };
    }
}
