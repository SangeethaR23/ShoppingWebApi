namespace ShoppingWebApi.Models.DTOs.Products
{
    public class ProductQuery
    {

        
            public int? CategoryId { get; set; }
            public bool IncludeChildren { get; set; } = false;

            public string? NameContains { get; set; }
            public decimal? PriceMin { get; set; }
            public decimal? PriceMax { get; set; }

            public int? RatingMin { get; set; } // 1..5

            // price|rating|newest|name
            public string? SortBy { get; set; } = "newest";
            public string? SortDir { get; set; } = "desc"; // asc|desc

            public int Page { get; set; } = 1;
            public int Size { get; set; } = 20;
        public bool InStockOnly { get; internal set; }
    }


    
}
