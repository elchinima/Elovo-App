
namespace Elovo.Infrastructure.Data;

public class ElovoDbContext : DbContext
{
    public ElovoDbContext(DbContextOptions<ElovoDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<UserTwoFactor> UserTwoFactor => Set<UserTwoFactor>();
    public DbSet<UserEmail> UserEmails => Set<UserEmail>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<FriendRequest> FriendRequests => Set<FriendRequest>();
    public DbSet<PendingMessage> PendingMessages => Set<PendingMessage>();
    public DbSet<ActiveCall> ActiveCalls => Set<ActiveCall>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Username).HasMaxLength(32).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(256).IsRequired();
            entity.Property(x => x.ProfileImagePath).HasMaxLength(512);
            entity.HasIndex(x => x.Username).IsUnique();
        });

        modelBuilder.Entity<UserSession>(entity =>
        {
            entity.HasKey(x => x.UserId);
            entity.Property(x => x.IsOnline).HasDefaultValue(false);
            entity.Property(x => x.LastLoginIp).HasMaxLength(45);
            entity.Property(x => x.RegistrationIp).HasMaxLength(45);
            entity.Property(x => x.FcmToken).HasMaxLength(4096);
            entity.Property(x => x.PreferredLanguage).HasMaxLength(2).HasDefaultValue("en");
            entity.Property(x => x.ActivityVisibility).HasMaxLength(32).HasDefaultValue("full");
            entity.HasIndex(x => x.IsOnline);

            entity.HasOne(x => x.User)
                .WithOne(x => x.Session)
                .HasForeignKey<UserSession>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserTwoFactor>(entity =>
        {
            entity.ToTable("UserTwoFactor");
            entity.HasKey(x => x.UserId);
            entity.Property(x => x.IsTwoFactorEnabled).HasDefaultValue(false);
            entity.Property(x => x.TwoFactorCodeHash).HasMaxLength(256);

            entity.HasOne(x => x.User)
                .WithOne(x => x.TwoFactor)
                .HasForeignKey<UserTwoFactor>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserEmail>(entity =>
        {
            entity.HasKey(x => x.UserId);
            entity.Property(x => x.Email).HasMaxLength(256);
            entity.Property(x => x.IsEmailConfirmed).HasDefaultValue(false);
            entity.Property(x => x.EmailConfirmationCodeHash).HasMaxLength(256);
            entity.HasIndex(x => x.Email).IsUnique().HasFilter("\"Email\" IS NOT NULL");

            entity.HasOne(x => x.User)
                .WithOne(x => x.EmailSettings)
                .HasForeignKey<UserEmail>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FirstUserReadAt);
            entity.Property(x => x.SecondUserReadAt);
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

        modelBuilder.Entity<PendingMessage>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Content).IsRequired();
            entity.Property(x => x.VoiceUrl).HasMaxLength(512);
            entity.Property(x => x.CallStatus).HasMaxLength(16);
            entity.Property(x => x.IsCall).HasDefaultValue(false);
            entity.HasIndex(x => x.IsNotificationSent);
            entity.HasIndex(x => new { x.ReceiverId, x.SentAt });
            entity.HasIndex(x => x.SenderId);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.ReceiverId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ActiveCall>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CallerName).HasMaxLength(32).IsRequired();
            entity.Property(x => x.CallerAvatar).HasMaxLength(512).IsRequired();
            entity.Property(x => x.OfferSdp);
            entity.Property(x => x.IsRejected).HasDefaultValue(false);
            entity.Property(x => x.AnsweredAt);
            entity.HasIndex(x => x.ReceiverId);
            entity.HasIndex(x => new { x.CallerId, x.ReceiverId }).IsUnique();
            entity.HasIndex(x => x.StartedAt);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.CallerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.ReceiverId)
                .OnDelete(DeleteBehavior.Cascade);
        });

    }
}
