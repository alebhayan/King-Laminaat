using Microsoft.EntityFrameworkCore;

namespace FSH.Framework.Persistence;

/// <summary>
/// Internal evaluator that turns specifications into executable <see cref="IQueryable{T}"/> queries.
/// </summary>
internal static class SpecificationEvaluator
{
    public static IQueryable<T> GetQuery<T>(
        IQueryable<T> inputQuery,
        ISpecification<T> specification)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(inputQuery);
        ArgumentNullException.ThrowIfNull(specification);

        IQueryable<T> query = inputQuery;

        if (specification.IgnoreQueryFilters)
        {
            query = query.IgnoreQueryFilters();
        }

        if (specification.AsNoTracking)
        {
            query = query.AsNoTracking();
        }

        if (specification.AsSplitQuery)
        {
            query = query.AsSplitQuery();
        }

        if (specification.Criteria is not null)
        {
            query = query.Where(specification.Criteria);
        }

        foreach (var include in specification.Includes)
        {
            query = query.Include(include);
        }

        foreach (var includeString in specification.IncludeStrings)
        {
            query = query.Include(includeString);
        }

        if (specification.OrderExpressions.Count > 0)
        {
            IOrderedQueryable<T>? ordered = null;

            foreach (var order in specification.OrderExpressions)
            {
                if (ordered is null)
                {
                    ordered = order.Descending
                        ? query.OrderByDescending(order.KeySelector)
                        : query.OrderBy(order.KeySelector);
                }
                else
                {
                    ordered = order.Descending
                        ? ordered.ThenByDescending(order.KeySelector)
                        : ordered.ThenBy(order.KeySelector);
                }
            }

            if (ordered is not null)
            {
                query = ordered;
            }
        }

        return query;
    }

    public static IQueryable<TResult> GetQuery<T, TResult>(
        IQueryable<T> inputQuery,
        ISpecification<T, TResult> specification)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(inputQuery);
        ArgumentNullException.ThrowIfNull(specification);

        var query = GetQuery(inputQuery, (ISpecification<T>)specification);

        // When a selector is configured, includes may be ignored at the EF level,
        // but behavior is consistently applied by always projecting at the end.
        return query.Select(specification.Selector);
    }
}

