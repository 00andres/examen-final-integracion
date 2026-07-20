using Microsoft.EntityFrameworkCore;
using Streaming.Api.Models;

namespace Streaming.Api.Data
{
    public class StreamingDbContext : DbContext
    {
        public StreamingDbContext(DbContextOptions<StreamingDbContext> options)
            : base(options)
        {
        }

        public DbSet<UserView> UserViews { get; set; } = null!;
        public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<UserView>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.VideoId).IsRequired();
                entity.Property(e => e.UserId).IsRequired();
                entity.Property(e => e.CreatorId).IsRequired();
            });

            modelBuilder.Entity<OutboxMessage>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Type).IsRequired();
                entity.Property(e => e.Payload).IsRequired();
                entity.HasIndex(e => new { e.ProcessedDate, e.CreatedAt });
            });
        }
    }
}
