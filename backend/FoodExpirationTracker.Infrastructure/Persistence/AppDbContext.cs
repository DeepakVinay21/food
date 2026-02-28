using FoodExpirationTracker.Domain.Common;
using FoodExpirationTracker.Domain.Entities;
using FoodExpirationTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FoodExpirationTracker.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductBatch> ProductBatches => Set<ProductBatch>();
    public DbSet<NotificationLog> Notifications => Set<NotificationLog>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<OcrCorrectionLog> OcrCorrectionLogs => Set<OcrCorrectionLog>();
    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();
    public DbSet<EmailVerification> EmailVerifications => Set<EmailVerification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Global soft-delete query filter for all BaseEntity types
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(AppDbContext)
                    .GetMethod(nameof(ApplySoftDeleteFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                    .MakeGenericMethod(entityType.ClrType);
                method.Invoke(null, [modelBuilder]);
            }
        }

        ConfigureUser(modelBuilder);
        ConfigureCategory(modelBuilder);
        ConfigureProduct(modelBuilder);
        ConfigureProductBatch(modelBuilder);
        ConfigureNotificationLog(modelBuilder);
        ConfigureRecipe(modelBuilder);
        ConfigureRecipeIngredient(modelBuilder);
        ConfigureOcrCorrectionLog(modelBuilder);
        ConfigureDeviceToken(modelBuilder);
        ConfigureEmailVerification(modelBuilder);

        SeedCategories(modelBuilder);
    }

    private static void ApplySoftDeleteFilter<T>(ModelBuilder modelBuilder) where T : BaseEntity
    {
        modelBuilder.Entity<T>().HasQueryFilter(e => e.DeletedAtUtc == null);
    }

    public override int SaveChanges()
    {
        SetTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SetTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void SetTimestamps()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = now;
                entry.Entity.UpdatedAtUtc = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = now;
            }
        }
    }

    private static void ConfigureUser(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).HasMaxLength(320).IsRequired();
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.LastName).HasMaxLength(100);
            entity.Property(e => e.Role).HasConversion<string>().HasMaxLength(20).HasDefaultValue(UserRole.User);

            entity.HasIndex(e => e.Email).IsUnique().HasFilter("\"DeletedAtUtc\" IS NULL");
        });
    }

    private static void ConfigureCategory(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("Categories");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.NormalizedName).HasMaxLength(100).IsRequired();

            entity.HasIndex(e => e.NormalizedName).IsUnique().HasFilter("\"DeletedAtUtc\" IS NULL");
        });
    }

    private static void ConfigureProduct(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("Products");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(150).IsRequired();
            entity.Property(e => e.NormalizedName).HasMaxLength(150).IsRequired();

            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Category).WithMany(c => c.Products).HasForeignKey(e => e.CategoryId).OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.UserId, e.NormalizedName }).IsUnique().HasFilter("\"DeletedAtUtc\" IS NULL");
            entity.HasIndex(e => e.UserId);
        });
    }

    private static void ConfigureProductBatch(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProductBatch>(entity =>
        {
            entity.ToTable("ProductBatches");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ExpiryDate).IsRequired();
            entity.Property(e => e.Quantity).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

            entity.HasOne(e => e.Product).WithMany(p => p.Batches).HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.ProductId, e.ExpiryDate, e.Status });
            entity.HasIndex(e => e.ExpiryDate).HasFilter("\"Status\" = 'Active' AND \"DeletedAtUtc\" IS NULL");
        });
    }

    private static void ConfigureNotificationLog(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NotificationLog>(entity =>
        {
            entity.ToTable("Notifications");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.NotificationType).HasMaxLength(30).IsRequired();

            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ProductBatch).WithMany().HasForeignKey(e => e.ProductBatchId).OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.ProductBatchId, e.NotificationType }).IsUnique();
            entity.HasIndex(e => new { e.UserId, e.SentAtUtc }).IsDescending(false, true);
        });
    }

    private static void ConfigureRecipe(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Recipe>(entity =>
        {
            entity.ToTable("Recipes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(150).IsRequired();
        });
    }

    private static void ConfigureRecipeIngredient(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RecipeIngredient>(entity =>
        {
            entity.ToTable("RecipeIngredients");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.IngredientName).HasMaxLength(120).IsRequired();
            entity.Property(e => e.NormalizedIngredientName).HasMaxLength(120).IsRequired();

            entity.HasOne(e => e.Recipe).WithMany(r => r.Ingredients).HasForeignKey(e => e.RecipeId).OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.RecipeId);
            entity.HasIndex(e => e.NormalizedIngredientName);
        });
    }

    private static void ConfigureOcrCorrectionLog(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OcrCorrectionLog>(entity =>
        {
            entity.ToTable("OcrCorrectionLogs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RawOcrText).IsRequired();
        });
    }

    private static void ConfigureDeviceToken(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DeviceToken>(entity =>
        {
            entity.ToTable("DeviceTokens");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Token).HasMaxLength(512).IsRequired();
            entity.Property(e => e.Platform).HasMaxLength(20).IsRequired();

            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasIndex(e => e.UserId);
        });
    }

    private static void ConfigureEmailVerification(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EmailVerification>(entity =>
        {
            entity.ToTable("EmailVerifications");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).HasMaxLength(320).IsRequired();
            entity.Property(e => e.Code).HasMaxLength(6).IsRequired();
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.LastName).HasMaxLength(100);

            entity.HasIndex(e => e.Email).IsUnique();
        });
    }

    private static void SeedCategories(ModelBuilder modelBuilder)
    {
        var now = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        string[] categories = ["General", "Dairy", "Fruits", "Vegetables", "Meat", "Bakery Item", "Snacks", "Grains", "Beverages", "Condiments", "Frozen"];

        var seeds = categories.Select(name => new Category
        {
            Id = Guid.NewGuid(),
            Name = name,
            NormalizedName = name.Trim().ToLowerInvariant(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        }).ToArray();

        // Use deterministic GUIDs for seeding so migrations are stable
        for (int i = 0; i < seeds.Length; i++)
        {
            seeds[i].Id = new Guid($"00000000-0000-0000-0000-{i + 1:D12}");
        }

        modelBuilder.Entity<Category>().HasData(seeds);
    }
}
