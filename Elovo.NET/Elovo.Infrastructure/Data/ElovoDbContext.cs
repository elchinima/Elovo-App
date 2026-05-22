using Elovo.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elovo.Infrastructure.Data;

public class ElovoDbContext : DbContext
{
    public ElovoDbContext(DbContextOptions<ElovoDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<FriendRequest> FriendRequests => Set<FriendRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Username).HasMaxLength(32).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(256).IsRequired();
            entity.HasIndex(x => x.Username).IsUnique();
            entity.HasIndex(x => x.IsOnline);
        });

        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.FirstUserId, x.SecondUserId }).IsUnique();
            entity.HasIndex(x => x.UpdatedAt);

            entity.HasOne(x => x.FirstUser)
                .WithMany(x => x.ConversationsAsFirstUser)
                .HasForeignKey(x => x.FirstUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.SecondUser)
                .WithMany(x => x.ConversationsAsSecondUser)
                .HasForeignKey(x => x.SecondUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FriendRequest>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.SenderId, x.ReceiverId }).IsUnique();
            entity.HasIndex(x => x.ReceiverId);

            entity.HasOne(x => x.Sender)
                .WithMany(x => x.SentFriendRequests)
                .HasForeignKey(x => x.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Receiver)
                .WithMany(x => x.ReceivedFriendRequests)
                .HasForeignKey(x => x.ReceiverId)
                .OnDelete(DeleteBehavior.Restrict);
        });

    }
}
