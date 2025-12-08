using Microsoft.EntityFrameworkCore;
using Store.Data.Model;

namespace Store.Data
{
    public class StoreDbContext : DbContext
    {
        public StoreDbContext(DbContextOptions<StoreDbContext> options) : base(options) { }

        // existing DbSets...
        public DbSet<Product> Products { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Order> Orders { get; set; } = null!;
        public DbSet<OrderItem> OrderItems { get; set; } = null!;
        public DbSet<Cart> Carts { get; set; } = null!;
        public DbSet<CartItem> CartItems { get; set; } = null!;
        public DbSet<AdminNotification> AdminNotifications { get; set; } = null!;
        public DbSet<OrderStatistic> OrderStatistics { get; set; } = null!;
        public DbSet<UserNotification> UserNotifications { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // -------------------------
            // Map Order -> sales.Orders
            // -------------------------
            builder.Entity<Order>(b =>
            {
                b.ToTable("Orders", "sales");

                b.HasKey(x => x.OrderId);
                b.Property(x => x.OrderId).HasColumnName("OrderId");

                b.Property(x => x.CustomerId).HasColumnName("CustomerId").IsRequired(false);
                b.Property(x => x.OrderDate).HasColumnName("OrderDate").HasDefaultValueSql("SYSUTCDATETIME()");
                b.Property(x => x.TotalAmount).HasColumnType("decimal(18,2)").HasColumnName("TotalAmount");
                b.Property(x => x.Status).HasMaxLength(50).HasColumnName("Status").IsRequired(false);
                b.Property(x => x.Note).HasMaxLength(1000).HasColumnName("Note").IsRequired(false);

                b.Property(x => x.CustomerName).HasColumnName("CustomerName").HasMaxLength(250).IsRequired(false);
                b.Property(x => x.CustomerEmail).HasColumnName("CustomerEmail").HasMaxLength(200).IsRequired(false);
                b.Property(x => x.CustomerPhone).HasColumnName("CustomerPhone").HasMaxLength(50).IsRequired(false);

                b.Property(x => x.ShippingAddress).HasColumnName("ShippingAddress").HasMaxLength(500).IsRequired(false);
                b.Property(x => x.PaymentMethod).HasColumnName("PaymentMethod").HasMaxLength(100).IsRequired(false);

                b.HasMany(o => o.Items)
                 .WithOne(i => i.Order)
                 .HasForeignKey(i => i.OrderId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // -------------------------
            // Map OrderItem -> sales.OrderItems
            // -------------------------
            builder.Entity<OrderItem>(b =>
            {
                b.ToTable("OrderItems", "sales");

                b.HasKey(x => x.OrderItemId);
                b.Property(x => x.OrderItemId).HasColumnName("OrderItemId");

                b.Property(x => x.OrderId).HasColumnName("OrderId");
                b.Property(x => x.ProductId).HasColumnName("ProductId");
                b.Property(x => x.Quantity).HasColumnName("Quantity");
                b.Property(x => x.UnitPrice).HasColumnName("UnitPrice").HasColumnType("decimal(18,2)");

                b.HasOne(x => x.Order)
                 .WithMany(o => o.Items)
                 .HasForeignKey(x => x.OrderId)
                 .HasConstraintName("FK_OrderItems_Orders");
            });

            // -------------------------------------------
            // ✅ Map AdminNotification → common.AdminNotifications
            // -------------------------------------------
            builder.Entity<AdminNotification>(b =>
            {
                b.ToTable("AdminNotifications", "common");  // << CHÍNH XÁC Ở ĐÂY

                b.HasKey(x => x.Id);

                b.Property(x => x.Id)
                    .HasColumnName("Id")
                    .ValueGeneratedOnAdd();

                b.Property(x => x.OrderId)
                    .HasColumnName("OrderId")
                    .IsRequired();

                b.Property(x => x.Message)
                    .HasColumnName("Message")
                    .HasMaxLength(500)
                    .IsRequired(false);

                b.Property(x => x.CreatedAt)
                    .HasColumnName("CreatedAt")
                    .HasDefaultValueSql("SYSUTCDATETIME()");

                b.Property(x => x.IsRead)
                    .HasColumnName("IsRead")
                    .HasDefaultValue(false);
            });
            builder.Entity<OrderStatistic>(b =>
            {
                b.ToTable("OrderStatistics", "analytics");
                b.HasKey(x => x.StatDate);
                b.Property(x => x.Revenue).HasColumnType("decimal(18,2)");
            });
            builder.Entity<UserNotification>(b =>
            {
                b.ToTable("UserNotifications", "common");

                b.HasKey(x => x.Id);
                b.Property(x => x.Id).HasColumnName("Id").ValueGeneratedOnAdd();

                b.Property(x => x.UserId).HasColumnName("UserId").IsRequired(false);
                b.Property(x => x.OrderId).HasColumnName("OrderId").IsRequired();
                b.Property(x => x.Message).HasColumnName("Message").HasMaxLength(500);
                b.Property(x => x.IsRead).HasColumnName("IsRead").HasDefaultValue(false);
                b.Property(x => x.CreatedAt).HasColumnName("CreatedAt").HasDefaultValueSql("SYSUTCDATETIME()");
            });

        }
    }
}
