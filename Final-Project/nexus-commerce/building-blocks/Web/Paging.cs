using Microsoft.EntityFrameworkCore;

namespace Nexus.BuildingBlocks.Web;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public PagedResult<TOut> Map<TOut>(Func<T, TOut> map) =>
        new(Items.Select(map).ToList(), Page, PageSize, TotalCount);
}

public static class Paging
{
    public static (int Page, int Size) Normalize(int? page, int? size, int defaultSize = 12, int maxSize = 50) =>
        (Math.Max(1, page ?? 1), Math.Clamp(size ?? defaultSize, 1, maxSize));

    public static async Task<PagedResult<T>> ToPagedAsync<T>(this IQueryable<T> query, int? page, int? size,
        CancellationToken ct, int defaultSize = 12, int maxSize = 50)
    {
        var (p, s) = Normalize(page, size, defaultSize, maxSize);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((p - 1) * s).Take(s).ToListAsync(ct);
        return new PagedResult<T>(items, p, s, total);
    }
}
