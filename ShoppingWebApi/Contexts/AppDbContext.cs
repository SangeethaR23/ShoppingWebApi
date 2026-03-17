using Microsoft.EntityFrameworkCore;
using ShoppingWebApi.Models;

namespace ShoppingWebApi.Contexts
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<UserDetails> UserDetails => Set<UserDetails>();
        public DbSet<Address> Addresses => Set<Address>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<ProductImage> ProductImages => Set<ProductImage>();
        public DbSet<Inventory> Inventories => Set<Inventory>();
        public DbSet<Cart> Carts => Set<Cart>();
        public DbSet<CartItem> CartItems => Set<CartItem>();
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderItem> OrderItems => Set<OrderItem>();
        public DbSet<Review> Reviews => Set<Review>();

        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<Refund> Refunds => Set<Refund>();
        public DbSet<LogEntry> Logs => Set<LogEntry>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email).IsUnique();

            modelBuilder.Entity<User>()
                .HasOne(u => u.UserDetails)
                .WithOne(d => d.User)
                .HasForeignKey<UserDetails>(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<User>()
                .HasMany(u => u.Addresses)
                .WithOne(a => a.User)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<User>()
                .HasOne(u => u.Cart)
                .WithOne(c => c.User)
                .HasForeignKey<Cart>(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<User>()
                .HasMany(u => u.Orders)
                .WithOne(o => o.User)
                .HasForeignKey(o => o.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<User>()
                .HasMany(u => u.Reviews)
                .WithOne(r => r.User)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Category self reference
            modelBuilder.Entity<Category>()
                .HasOne(c => c.ParentCategory)
                .WithMany(p => p.Children)
                .HasForeignKey(c => c.ParentCategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // Product
            modelBuilder.Entity<Product>()
                .HasIndex(p => p.SKU).IsUnique();

            modelBuilder.Entity<Product>()
                .Property(p => p.Price)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Product>()
                .HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // ProductImage
            modelBuilder.Entity<ProductImage>()
                .HasOne(i => i.Product)
                .WithMany(p => p.Images)
                .HasForeignKey(i => i.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            // Inventory 1:1
            modelBuilder.Entity<Inventory>()
                .HasIndex(i => i.ProductId).IsUnique();

            modelBuilder.Entity<Inventory>()
                .HasOne(i => i.Product)
                .WithOne(p => p.Inventory)
                .HasForeignKey<Inventory>(i => i.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            // Cart & CartItem
            modelBuilder.Entity<Cart>()
                .HasIndex(c => c.UserId).IsUnique();

            modelBuilder.Entity<CartItem>()
                .Property(ci => ci.UnitPrice).HasPrecision(18, 2);

            modelBuilder.Entity<CartItem>()
                .HasOne(ci => ci.Cart)
                .WithMany(c => c.Items)
                .HasForeignKey(ci => ci.CartId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CartItem>()
                .HasOne(ci => ci.Product)
                .WithMany()
                .HasForeignKey(ci => ci.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            // Ensure no duplicate product rows in same cart
            modelBuilder.Entity<CartItem>()
                .HasIndex(ci => new { ci.CartId, ci.ProductId })
                .IsUnique();

            // Order & OrderItem
            modelBuilder.Entity<Order>()
                .HasIndex(o => o.OrderNumber).IsUnique();

            modelBuilder.Entity<Order>()
                .Property(o => o.SubTotal).HasPrecision(18, 2);
            modelBuilder.Entity<Order>()
                .Property(o => o.ShippingFee).HasPrecision(18, 2);
            modelBuilder.Entity<Order>()
                .Property(o => o.Discount).HasPrecision(18, 2);
            modelBuilder.Entity<Order>()
                .Property(o => o.Total).HasPrecision(18, 2);

            modelBuilder.Entity<Order>()
                .Property(o => o.Status).HasConversion<int>();
            modelBuilder.Entity<Order>()
                .Property(o => o.PaymentStatus).HasConversion<int>();

            modelBuilder.Entity<OrderItem>()
                .Property(oi => oi.UnitPrice).HasPrecision(18, 2);
            modelBuilder.Entity<OrderItem>()
                .Property(oi => oi.LineTotal).HasPrecision(18, 2);

            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Order)
                .WithMany(o => o.Items)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Product)
                .WithMany(p => p.OrderItems)
                .HasForeignKey(oi => oi.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            // Review: 1 per user per product
            modelBuilder.Entity<Review>()
                .HasIndex(r => new { r.ProductId, r.UserId })
                .IsUnique();

            //order:payment 1 to 1
            modelBuilder.Entity<Order>()
                .HasOne(o => o.Payment)
                .WithOne(p => p.Order)
                .HasForeignKey<Payment>(p => p.OrderId);

            modelBuilder.Entity<Payment>()
                .HasIndex(p => p.OrderId)
                .IsUnique();

            // PAYMENT → REFUND (1 : 1)
            modelBuilder.Entity<Payment>()
                .HasOne(p => p.Refund)
                .WithOne(r => r.Payment)
                .HasForeignKey<Refund>(r => r.PaymentId);
        }
    }
}