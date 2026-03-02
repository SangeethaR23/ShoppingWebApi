using AutoMapper;
using ShoppingWebApi.Models;
using ShoppingWebApi.Models.DTOs.Addresses;
using ShoppingWebApi.Models.DTOs.Cart;
using ShoppingWebApi.Models.DTOs.Categories;
using ShoppingWebApi.Models.DTOs.Common;
using ShoppingWebApi.Models.DTOs.Orders;
using ShoppingWebApi.Models.DTOs.Products;
using ShoppingWebApi.Models.DTOs.Reviews;

namespace ShoppingWebApi.Mappings
{
    public class AppMappingProfile : Profile
    {
        public AppMappingProfile()
        {
            // Category
            CreateMap<Category, CategoryReadDto>();

            // Product & Images
            CreateMap<ProductImage, ProductImageReadDto>();
            CreateMap<Product, ProductReadDto>()
                .ForMember(d => d.Images, opt => opt.MapFrom(s => s.Images))
                // Avg rating & count will be filled in service (after aggregation)
                .ForMember(d => d.AverageRating, opt => opt.Ignore())
                .ForMember(d => d.ReviewsCount, opt => opt.Ignore());

            // Address
            CreateMap<Address, AddressReadDto>();

            // Cart & CartItem
            CreateMap<CartItem, CartItemReadDto>()
                .ForMember(d => d.ProductName, opt => opt.MapFrom(s => s.Product.Name))
                .ForMember(d => d.SKU, opt => opt.MapFrom(s => s.Product.SKU))
                .ForMember(d => d.LineTotal, opt => opt.MapFrom(s => s.UnitPrice * s.Quantity))
                // rating summary populated in service
                .ForMember(d => d.AverageRating, opt => opt.Ignore())
                .ForMember(d => d.ReviewsCount, opt => opt.Ignore());

            CreateMap<Cart, CartReadDto>()
                .ForMember(d => d.SubTotal, opt => opt.MapFrom(s => s.Items.Sum(i => i.UnitPrice * i.Quantity)));

            // Orders
            //CreateMap<OrderItem, OrderItemReadDto>();
            CreateMap<Order, OrderReadDto>();
            //Review
            CreateMap<Review, ReviewReadDto>()
                .ForMember(d => d.UserName, opt => opt.Ignore());
        }
    }
}