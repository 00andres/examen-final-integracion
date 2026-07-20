using Microsoft.EntityFrameworkCore;
using Accounting.Api.Models;

namespace Accounting.Api.Data
{
    public class AccountingDbContext : DbContext
    {
        public AccountingDbContext(DbContextOptions<AccountingDbContext> options)
            : base(options)
        {
        }

        public DbSet<CreatorRoyalty> CreatorRoyalties { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<CreatorRoyalty>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CreatorId).IsRequired();
                entity.HasIndex(e => e.CreatorId).IsUnique();
                entity.Property(e => e.EstimatedRevenue).HasColumnType("decimal(18,4)");
            });
        }
    }
}
