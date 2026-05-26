using Microsoft.EntityFrameworkCore;

namespace OrderApi.Infrastructure.Persistence;

public sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options)
    : DbContext(options)
{
    public DbSet<OrderEntity> Orders => Set<OrderEntity>();

    public DbSet<IdempotencyRecordEntity> IdempotencyRecords => Set<IdempotencyRecordEntity>();

    public DbSet<BackgroundJobEntity> BackgroundJobs => Set<BackgroundJobEntity>();

    public DbSet<OrderConfirmationEntity> OrderConfirmations => Set<OrderConfirmationEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrderEntity>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.CustomerId)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.ResponseJson)
                .IsRequired();

            entity.Property(x => x.CreatedAt)
                .IsRequired();
        });

        modelBuilder.Entity<IdempotencyRecordEntity>(entity =>
        {
            entity.HasKey(x => x.Key);

            entity.Property(x => x.Key)
                .HasMaxLength(200);

            entity.Property(x => x.RequestHash)
                .HasMaxLength(128)
                .IsRequired();

            entity.Property(x => x.State)
                .HasConversion<string>()
                .HasMaxLength(40)
                .IsRequired();

            entity.Property(x => x.ResponseJson);
        });

        modelBuilder.Entity<BackgroundJobEntity>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => x.IdempotencyKey)
                .IsUnique();

            entity.Property(x => x.JobType)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.PayloadJson)
                .IsRequired();

            entity.Property(x => x.IdempotencyKey)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.State)
                .HasConversion<string>()
                .HasMaxLength(40)
                .IsRequired();

            entity.Property(x => x.LastError)
                .HasMaxLength(1000);
        });

        modelBuilder.Entity<OrderConfirmationEntity>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => x.IdempotencyKey)
                .IsUnique();

            entity.Property(x => x.IdempotencyKey)
                .HasMaxLength(200)
                .IsRequired();
        });
    }
}
