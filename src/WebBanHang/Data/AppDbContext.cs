using Microsoft.EntityFrameworkCore;
using WebBanHang.Models;

namespace WebBanHang.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // A category with products, or a product that was ordered, cannot be deleted.
        modelBuilder.Entity<Product>()
            .HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<OrderItem>()
            .HasOne(i => i.Product)
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        // Demo data, inserted once by the InitialCreate migration.
        modelBuilder.Entity<Category>().HasData(
            new Category { Id = 1, Name = "Điện thoại", Description = "Điện thoại thông minh các hãng" },
            new Category { Id = 2, Name = "Laptop", Description = "Máy tính xách tay văn phòng và gaming" },
            new Category { Id = 3, Name = "Phụ kiện", Description = "Tai nghe, chuột, cáp sạc và các phụ kiện khác" });

        modelBuilder.Entity<Product>().HasData(
            new Product { Id = 1, Name = "Điện thoại thông minh A1 128GB", Description = "Màn hình 6.1 inch, pin 4000mAh", Price = 8_990_000m, Stock = 30, CategoryId = 1 },
            new Product { Id = 2, Name = "Điện thoại thông minh B2 Pro 256GB", Description = "Camera 50MP, sạc nhanh 67W", Price = 14_490_000m, Stock = 20, CategoryId = 1 },
            new Product { Id = 3, Name = "Laptop văn phòng 14 inch", Description = "Core i5, RAM 16GB, SSD 512GB", Price = 16_900_000m, Stock = 12, CategoryId = 2 },
            new Product { Id = 4, Name = "Laptop gaming 15.6 inch", Description = "Core i7, RAM 16GB, RTX 4050", Price = 27_500_000m, Stock = 6, CategoryId = 2 },
            new Product { Id = 5, Name = "Tai nghe Bluetooth", Description = "Chống ồn chủ động, pin 30 giờ", Price = 890_000m, Stock = 100, CategoryId = 3 },
            new Product { Id = 6, Name = "Chuột không dây", Description = "Kết nối 2.4GHz, pin AA", Price = 250_000m, Stock = 150, CategoryId = 3 },
            new Product { Id = 7, Name = "Cáp sạc USB-C 1m", Description = "Hỗ trợ sạc nhanh 60W", Price = 120_000m, Stock = 200, CategoryId = 3 });
    }
}
