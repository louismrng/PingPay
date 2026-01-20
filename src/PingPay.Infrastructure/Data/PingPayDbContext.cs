using Microsoft.EntityFrameworkCore;
using PingPay.Core.Entities;

namespace PingPay.Infrastructure.Data;

public class PingPayDbContext : DbContext
{
    public PingPayDbContext(DbContextOptions<PingPayDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.PhoneNumber).HasColumnName("phone_number").HasMaxLength(20).IsRequired();
            entity.Property(e => e.DisplayName).HasColumnName("display_name").HasMaxLength(100);
            entity.Property(e => e.IsVerified).HasColumnName("is_verified");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.IsFrozen).HasColumnName("is_frozen");
            entity.Property(e => e.DailyTransferLimit).HasColumnName("daily_transfer_limit").HasPrecision(18, 6);
            entity.Property(e => e.DailyTransferredAmount).HasColumnName("daily_transferred_amount").HasPrecision(18, 6);
            entity.Property(e => e.DailyLimitResetAt).HasColumnName("daily_limit_reset_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(e => e.PhoneNumber).IsUnique();

            entity.HasOne(e => e.Wallet)
                  .WithOne(w => w.User)
                  .HasForeignKey<Wallet>(w => w.UserId);
        });

        // Wallet configuration
        modelBuilder.Entity<Wallet>(entity =>
        {
            entity.ToTable("wallets");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.PublicKey).HasColumnName("public_key").HasMaxLength(64).IsRequired();
            entity.Property(e => e.EncryptedPrivateKey).HasColumnName("encrypted_private_key").IsRequired();
            entity.Property(e => e.KeyVersion).HasColumnName("key_version").HasMaxLength(100).IsRequired();
            entity.Property(e => e.CachedUsdcBalance).HasColumnName("cached_usdc_balance").HasPrecision(18, 6);
            entity.Property(e => e.CachedUsdtBalance).HasColumnName("cached_usdt_balance").HasPrecision(18, 6);
            entity.Property(e => e.BalanceLastUpdatedAt).HasColumnName("balance_last_updated_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasIndex(e => e.PublicKey).IsUnique();
            entity.HasIndex(e => e.UserId).IsUnique();
        });

        // Transaction configuration
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.ToTable("transactions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(64).IsRequired();
            entity.Property(e => e.SenderId).HasColumnName("sender_id");
            entity.Property(e => e.ReceiverId).HasColumnName("receiver_id");
            entity.Property(e => e.Amount).HasColumnName("amount").HasPrecision(18, 6);
            entity.Property(e => e.TokenType).HasColumnName("token_type");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.Type).HasColumnName("type");
            entity.Property(e => e.SolanaSignature).HasColumnName("solana_signature").HasMaxLength(100);
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message").HasMaxLength(500);
            entity.Property(e => e.RetryCount).HasColumnName("retry_count");
            entity.Property(e => e.ConfirmedAt).HasColumnName("confirmed_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(e => e.IdempotencyKey).IsUnique();
            entity.HasIndex(e => e.SenderId);
            entity.HasIndex(e => e.ReceiverId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.SolanaSignature);

            entity.HasOne(e => e.Sender)
                  .WithMany(u => u.SentTransactions)
                  .HasForeignKey(e => e.SenderId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Receiver)
                  .WithMany(u => u.ReceivedTransactions)
                  .HasForeignKey(e => e.ReceiverId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // AuditLog configuration
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Action).HasColumnName("action").HasMaxLength(100).IsRequired();
            entity.Property(e => e.EntityType).HasColumnName("entity_type").HasMaxLength(50).IsRequired();
            entity.Property(e => e.EntityId).HasColumnName("entity_id").HasMaxLength(50);
            entity.Property(e => e.OldValues).HasColumnName("old_values").HasColumnType("jsonb");
            entity.Property(e => e.NewValues).HasColumnName("new_values").HasColumnType("jsonb");
            entity.Property(e => e.IpAddress).HasColumnName("ip_address").HasMaxLength(50);
            entity.Property(e => e.UserAgent).HasColumnName("user_agent").HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.CreatedAt);
        });
    }
}
