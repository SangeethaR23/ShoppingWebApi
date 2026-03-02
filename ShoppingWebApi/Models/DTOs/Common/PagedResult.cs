namespace ShoppingWebApi.Models.DTOs.Common
{
    public class PagedResult<T>
    {

        public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
        public int TotalCount { get; init; }
        public int PageNumber { get; init; }
        public int PageSize { get; init; }

    }
}
