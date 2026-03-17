using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ShoppingWebApi.Contexts;
using ShoppingWebApi.Interfaces;
using ShoppingWebApi.Middleware;
using ShoppingWebApi.Models;
using ShoppingWebApi.Repositories;
using ShoppingWebApi.Services;
using ShoppingWebApi.Services.Logging;
using ShoppingWebApi.Services.Security;
using System.Reflection;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

#region AddDb
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer"));
});
#endregion

// Add services to the container.

builder.Services.AddControllers(options=>
{
    options.Filters.Add<ShoppingWebApi.Filters.ValidateModelAttribute>();
});
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();
//AutoMapper:scan current assembly for profiles.

builder.Services.AddAutoMapper(Assembly.GetExecutingAssembly());
#region Services&Repositories
//Services and Repositories
builder.Services.AddScoped<IRepository<int, User>, Repository<int, User>>();
builder.Services.AddScoped<IRepository<int, UserDetails>, Repository<int, UserDetails>>();
builder.Services.AddScoped<IRepository<int, Address>, Repository<int, Address>>();
builder.Services.AddScoped<IRepository<int, Category>, Repository<int, Category>>();
builder.Services.AddScoped<IRepository<int, Product>, Repository<int, Product>>();
builder.Services.AddScoped<IRepository<int, ProductImage>, Repository<int, ProductImage>>();
builder.Services.AddScoped<IRepository<int, Inventory>, Repository<int, Inventory>>();
builder.Services.AddScoped<IRepository<int, Cart>, Repository<int, Cart>>();
builder.Services.AddScoped<IRepository<int, CartItem>, Repository<int, CartItem>>();
builder.Services.AddScoped<IRepository<int, Order>, Repository<int, Order>>();
builder.Services.AddScoped<IRepository<int, OrderItem>, Repository<int, OrderItem>>();
builder.Services.AddScoped<IRepository<int, Review>, Repository<int, Review>>();
builder.Services.AddScoped<IRepository<int, Payment>, Repository<int, Payment>>();
builder.Services.AddScoped<IRepository<int, Refund>, Repository<int, Refund>>();

builder.Services.AddScoped(typeof(IRepository<,>), typeof(Repository<,>));
builder.Services.AddScoped<ICategoryService,CategoryService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IAddressService, AddressService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ILogWriter, DbLogWriter>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
#endregion

#region JWt Token
//JWt Authentication

var jwtKey = builder.Configuration["Jwt:Key"]!;
var jwtIssuer = builder.Configuration["Jwt:Issuer"]!;
var jwtAudience = builder.Configuration["Jwt:Audience"]!;

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })

.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false; // set true in production behind HTTPS
    options.SaveToken = true;
    options.TokenValidationParameters = new

    Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1)
    };
});
#endregion
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

var app = builder.Build();
//dataseeding

//middeleware order
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseGlobalExceptionHandling();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
