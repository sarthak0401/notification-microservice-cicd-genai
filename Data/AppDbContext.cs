using Microsoft.EntityFrameworkCore;
using NotificationMicroservice.Models;

namespace NotificationMicroservice.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        public DbSet<UserDeviceToken> UserDeviceTokens { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder
                .Entity<UserDeviceToken>()
                .HasIndex(x => new { x.UserId, x.DeviceToken })
                .IsUnique();

            // A message can be delivered more than once (retry), so keep one row per message id.
            modelBuilder
                .Entity<Notification>()
                .HasIndex(x => x.MessageId)
                .IsUnique()
                .HasFilter("[MessageId] IS NOT NULL");
        }
    }
}
