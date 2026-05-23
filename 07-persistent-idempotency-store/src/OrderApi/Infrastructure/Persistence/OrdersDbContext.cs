using Microsoft.EntityFrameworkCore;

namespace OrderApi.Infrastructure.Persistence;

public sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options)
    : DbContext(options)
{
    public DbSet<IdempotencyRecordEntity> IdempotencyRecords => Set<IdempotencyRecordEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
    }
}
