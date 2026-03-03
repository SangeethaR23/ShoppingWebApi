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

builder.Services.AddScoped(typeof(IRepository<,>), typeof(Repository<,>));

builder.Services.AddScoped<ICategoryService,CategoryService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IAddressService, AddressService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IUserService, UserService>();

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

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    // ---------- helpers (idempotent) ----------
    async Task<User> EnsureUserAsync(string email, string role, string plainPassword)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null)
        {
            user = new User
            {
                Email = email,
                PasswordHash = PasswordHasher.Hash(plainPassword),
                Role = role
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }
        return user;
    }

    async Task EnsureUserDetailsAsync(User user, string firstName, string lastName, string? phone = null)
    {
        var d = await db.UserDetails.FirstOrDefaultAsync(x => x.UserId == user.Id);
        if (d == null)
        {
            db.UserDetails.Add(new UserDetails
            {
                UserId = user.Id,
                FirstName = firstName,
                LastName = lastName,
                Phone = phone
            });
            await db.SaveChangesAsync();
        }
    }

    async Task<Address> EnsureAddressAsync(int userId, string fullName, string line1, string city, string state, string postal, string? label = null, string? phone = null, string country = "India")
    {
        var addr = await db.Addresses.FirstOrDefaultAsync(a =>
            a.UserId == userId && a.FullName == fullName && a.Line1 == line1 && a.PostalCode == postal);
        if (addr == null)
        {
            addr = new Address
            {
                UserId = userId,
                Label = label,
                FullName = fullName,
                Phone = phone,
                Line1 = line1,
                City = city,
                State = state,
                PostalCode = postal,
                Country = country
            };
            db.Addresses.Add(addr);
            await db.SaveChangesAsync();
        }
        return addr;
    }

    async Task<Cart> EnsureCartAsync(int userId)
    {
        var cart = await db.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.UserId == userId);
        if (cart == null)
        {
            cart = new Cart { UserId = userId };
            db.Carts.Add(cart);
            await db.SaveChangesAsync();
            cart = await db.Carts.Include(c => c.Items).FirstAsync(c => c.UserId == userId);
        }
        return cart;
    }

    async Task<Category> EnsureCategoryAsync(string name, string? desc = null)
    {
        var c = await db.Categories.FirstOrDefaultAsync(x => x.Name == name);
        if (c == null)
        {
            c = new Category { Name = name, Description = desc };
            db.Categories.Add(c);
            await db.SaveChangesAsync();
        }
        return c;
    }

    async Task<Product> EnsureProductAsync(string name, string sku, int categoryId, decimal price, string? desc = null, bool isActive = true)
    {
        var p = await db.Products.FirstOrDefaultAsync(x => x.SKU == sku);
        if (p == null)
        {
            p = new Product
            {
                Name = name,
                SKU = sku,
                CategoryId = categoryId,
                Price = price,
                Description = desc,
                IsActive = isActive
            };
            db.Products.Add(p);
            await db.SaveChangesAsync();
        }
        else
        {
            bool changed = false;
            if (p.Name != name) { p.Name = name; changed = true; }
            if (p.CategoryId != categoryId) { p.CategoryId = categoryId; changed = true; }
            if (p.Price != price) { p.Price = price; changed = true; }
            if (p.Description != desc) { p.Description = desc; changed = true; }
            if (p.IsActive != isActive) { p.IsActive = isActive; changed = true; }
            if (changed) await db.SaveChangesAsync();
        }
        return p;
    }

    async Task EnsureImageAsync(int productId, string url)
    {
        if (!await db.ProductImages.AnyAsync(i => i.ProductId == productId && i.Url == url))
        {
            db.ProductImages.Add(new ProductImage { ProductId = productId, Url = url });
            await db.SaveChangesAsync();
        }
    }

    async Task EnsureInventoryAsync(int productId, int qty, int reorderLevel)
    {
        var inv = await db.Inventories.FirstOrDefaultAsync(i => i.ProductId == productId);
        if (inv == null)
        {
            inv = new Inventory { ProductId = productId, Quantity = qty, ReorderLevel = reorderLevel };
            db.Inventories.Add(inv);
            await db.SaveChangesAsync();
        }
        else
        {
            // Do not force quantity if you want to keep runtime changes; but ensure reorder level
            if (inv.ReorderLevel != reorderLevel) { inv.ReorderLevel = reorderLevel; await db.SaveChangesAsync(); }
        }
    }

    async Task EnsureCartHasItemAsync(int userId, string productSku, int qty)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.SKU == productSku);
        if (product == null) return; // safe guard

        var cart = await EnsureCartAsync(userId);
        if (!cart.Items.Any(i => i.ProductId == product.Id))
        {
            cart.Items.Add(new CartItem
            {
                ProductId = product.Id,
                Quantity = qty,
                UnitPrice = product.Price
            });
            await db.SaveChangesAsync();
        }
    }

    // ---------- actual seeding ----------

    // Admin + User
    var admin = await EnsureUserAsync("admin@shop.local", "Admin", "Admin@12345");
    var user = await EnsureUserAsync("user1@example.com", "User", "P@ssw0rd!");

    await EnsureUserDetailsAsync(user, "User", "One", "9876543210");
    await EnsureAddressAsync(user.Id, "User One", "No.123, Gandhi Street", "Chennai", "TN", "600001", label: "Home", phone: "9876543210");
    await EnsureCartAsync(user.Id);

    // Categories
    var electronics = await EnsureCategoryAsync("Electronics", "Devices & gadgets");
    var books = await EnsureCategoryAsync("Books", "Printed and e-books");

    // Products
    var p1 = await EnsureProductAsync("Wireless Mouse", "MSE-1001", electronics.Id, 799.00m, "Ergonomic 2.4GHz wireless mouse");
    var p2 = await EnsureProductAsync("Mechanical Keyboard", "KEY-2002", electronics.Id, 3299.00m, "Blue-switch 87-key compact keyboard");
    var p3 = await EnsureProductAsync("Clean Code", "BK-CLNCODE", books.Id, 499.00m, "By Robert C. Martin");

    // Images
    await EnsureImageAsync(p1.Id, "https://picsum.photos/seed/mouse/600/400");
    await EnsureImageAsync(p2.Id, "https://picsum.photos/seed/keyboard/600/400");
    await EnsureImageAsync(p3.Id, "https://picsum.photos/seed/book/600/400");

    // Inventory
    await EnsureInventoryAsync(p1.Id, 50, 5);
    await EnsureInventoryAsync(p2.Id, 30, 5);
    await EnsureInventoryAsync(p3.Id, 100, 10);

    // Put 1 item in user's cart (mouse x2)
    await EnsureCartHasItemAsync(user.Id, "MSE-1001", 2);
}
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
