using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PingPay.Core.Entities;
using PingPay.Core.Enums;

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
    public DbSet<WithdrawalWhitelist> WithdrawalWhitelists => Set<WithdrawalWhitelist>();
    public DbSet<FeeSchedule> FeeSchedules => Set<FeeSchedule>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<DailyTransferSummary> DailyTransferSummaries => Set<DailyTransferSummary>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // PostgreSQL enum conversions (stored as strings in DB)
        var tokenTypeConverter = new ValueConverter<TokenType, string>(
            v => v.ToString().ToUpperInvariant(),
            v => Enum.Parse<TokenType>(v, true));

        var transactionStatusConverter = new ValueConverter<TransactionStatus, string>(
            v => v.ToString().ToLowerInvariant(),
            v => Enum.Parse<TransactionStatus>(v, true));

        var transactionTypeConverter = new ValueConverter<TransactionType, string>(
            v => v.ToString().ToLowerInvariant(),
            v => Enum.Parse<TransactionType>(v, true));

        // =====================================================================
        // User configuration
        // =====================================================================
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.PhoneNumber).HasColumnName("phone_number").HasMaxLength(20).IsRequired();
            entity.Property(e => e.PhoneCountryCode).HasColumnName("phone_country_code").HasMaxLength(5).IsRequired();
            entity.Property(e => e.DisplayName).HasColumnName("display_name").HasMaxLength(100);
            entity.Property(e => e.IsVerified).HasColumnName("is_verified");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.IsFrozen).HasColumnName("is_frozen");
            entity.Property(e => e.FrozenReason).HasColumnName("frozen_reason").HasMaxLength(500);
            entity.Property(e => e.FrozenAt).HasColumnName("frozen_at");
            entity.Property(e => e.FrozenBy).HasColumnName("frozen_by");

            // Limits
            entity.Property(e => e.DailyTransferLimit).HasColumnName("daily_transfer_limit").HasPrecision(18, 6);
            entity.Property(e => e.DailyTransferredAmount).HasColumnName("daily_transferred_amount").HasPrecision(18, 6);
            entity.Property(e => e.DailyLimitResetAt).HasColumnName("daily_limit_reset_at");
            entity.Property(e => e.MonthlyTransferLimit).HasColumnName("monthly_transfer_limit").HasPrecision(18, 6);
            entity.Property(e => e.MonthlyTransferredAmount).HasColumnName("monthly_transferred_amount").HasPrecision(18, 6);
            entity.Property(e => e.MonthlyLimitResetAt).HasColumnName("monthly_limit_reset_at");

            // Metadata
            entity.Property(e => e.LastLoginAt).HasColumnName("last_login_at");
            entity.Property(e => e.LastActivityAt).HasColumnName("last_activity_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            // Computed properties - ignored
            entity.Ignore(e => e.FullPhoneNumber);
            entity.Ignore(e => e.DailyRemaining);
            entity.Ignore(e => e.MonthlyRemaining);

            entity.HasIndex(e => new { e.PhoneCountryCode, e.PhoneNumber }).IsUnique();

            entity.HasOne(e => e.Wallet)
                  .WithOne(w => w.User)
                  .HasForeignKey<Wallet>(w => w.UserId);
        });

        // =====================================================================
        // Wallet configuration
        // =====================================================================
        modelBuilder.Entity<Wallet>(entity =>
        {
            entity.ToTable("wallets");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.PublicKey).HasColumnName("public_key").HasMaxLength(44).IsRequired();
            entity.Property(e => e.EncryptedPrivateKey).HasColumnName("encrypted_private_key").IsRequired();
            entity.Property(e => e.KeyVersion).HasColumnName("key_version").HasMaxLength(100).IsRequired();
            entity.Property(e => e.KeyAlgorithm).HasColumnName("key_algorithm").HasMaxLength(50).IsRequired();
            entity.Property(e => e.CachedUsdcBalance).HasColumnName("cached_usdc_balance").HasPrecision(18, 6);
            entity.Property(e => e.CachedUsdtBalance).HasColumnName("cached_usdt_balance").HasPrecision(18, 6);
            entity.Property(e => e.CachedSolBalance).HasColumnName("cached_sol_balance").HasPrecision(18, 9);
            entity.Property(e => e.BalanceLastUpdatedAt).HasColumnName("balance_last_updated_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasIndex(e => e.PublicKey).IsUnique();
            entity.HasIndex(e => e.UserId).IsUnique();
        });

        // =====================================================================
        // Transaction configuration
        // =====================================================================
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.ToTable("transactions");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(64).IsRequired();
            entity.Property(e => e.SenderId).HasColumnName("sender_id");
            entity.Property(e => e.ReceiverId).HasColumnName("receiver_id");
            entity.Property(e => e.ExternalAddress).HasColumnName("external_address").HasMaxLength(44);
            entity.Property(e => e.Amount).HasColumnName("amount").HasPrecision(18, 6);
            entity.Property(e => e.FeeAmount).HasColumnName("fee_amount").HasPrecision(18, 6);
            entity.Property(e => e.TokenType).HasColumnName("token").HasConversion(tokenTypeConverter);
            entity.Property(e => e.Status).HasColumnName("status").HasConversion(transactionStatusConverter);
            entity.Property(e => e.Type).HasColumnName("type").HasConversion(transactionTypeConverter);
            entity.Property(e => e.SolanaSignature).HasColumnName("solana_signature").HasMaxLength(88);
            entity.Property(e => e.SolanaSlot).HasColumnName("solana_slot");
            entity.Property(e => e.SolanaBlockTime).HasColumnName("solana_block_time");
            entity.Property(e => e.ErrorCode).HasColumnName("error_code").HasMaxLength(50);
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message").HasMaxLength(1000);
            entity.Property(e => e.RetryCount).HasColumnName("retry_count");
            entity.Property(e => e.MaxRetries).HasColumnName("max_retries");
            entity.Property(e => e.NextRetryAt).HasColumnName("next_retry_at");
            entity.Property(e => e.ConfirmedAt).HasColumnName("confirmed_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            // Computed properties - ignored
            entity.Ignore(e => e.TotalAmount);
            entity.Ignore(e => e.CanRetry);

            entity.HasIndex(e => e.IdempotencyKey).IsUnique();
            entity.HasIndex(e => e.SenderId);
            entity.HasIndex(e => e.ReceiverId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.SolanaSignature);
            entity.HasIndex(e => e.CreatedAt);

            entity.HasOne(e => e.Sender)
                  .WithMany(u => u.SentTransactions)
                  .HasForeignKey(e => e.SenderId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Receiver)
                  .WithMany(u => u.ReceivedTransactions)
                  .HasForeignKey(e => e.ReceiverId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================================
        // WithdrawalWhitelist configuration
        // =====================================================================
        modelBuilder.Entity<WithdrawalWhitelist>(entity =>
        {
            entity.ToTable("withdrawal_whitelist");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Address).HasColumnName("address").HasMaxLength(44).IsRequired();
            entity.Property(e => e.Label).HasColumnName("label").HasMaxLength(100);
            entity.Property(e => e.IsVerified).HasColumnName("is_verified");
            entity.Property(e => e.VerifiedAt).HasColumnName("verified_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasIndex(e => new { e.UserId, e.Address }).IsUnique();

            entity.HasOne(e => e.User)
                  .WithMany(u => u.WithdrawalWhitelist)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // =====================================================================
        // FeeSchedule configuration
        // =====================================================================
        modelBuilder.Entity<FeeSchedule>(entity =>
        {
            entity.ToTable("fee_schedule");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.TransactionType).HasColumnName("transaction_type").HasConversion(transactionTypeConverter);
            entity.Property(e => e.TokenType).HasColumnName("token").HasConversion(tokenTypeConverter);
            entity.Property(e => e.FlatFee).HasColumnName("flat_fee").HasPrecision(18, 6);
            entity.Property(e => e.PercentageFee).HasColumnName("percentage_fee").HasPrecision(5, 4);
            entity.Property(e => e.MinFee).HasColumnName("min_fee").HasPrecision(18, 6);
            entity.Property(e => e.MaxFee).HasColumnName("max_fee").HasPrecision(18, 6);
            entity.Property(e => e.MinAmount).HasColumnName("min_amount").HasPrecision(18, 6);
            entity.Property(e => e.MaxAmount).HasColumnName("max_amount").HasPrecision(18, 6);
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.EffectiveFrom).HasColumnName("effective_from");
            entity.Property(e => e.EffectiveUntil).HasColumnName("effective_until");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        });

        // =====================================================================
        // SystemSetting configuration
        // =====================================================================
        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.ToTable("system_settings");
            entity.HasKey(e => e.Key);

            entity.Property(e => e.Key).HasColumnName("key").HasMaxLength(100);
            entity.Property(e => e.Value).HasColumnName("value").HasColumnType("jsonb").IsRequired();
            entity.Property(e => e.Description).HasColumnName("description").HasMaxLength(500);
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
        });

        // =====================================================================
        // DailyTransferSummary configuration
        // =====================================================================
        modelBuilder.Entity<DailyTransferSummary>(entity =>
        {
            entity.ToTable("daily_transfer_summaries");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Date).HasColumnName("date");
            entity.Property(e => e.UsdcSentCount).HasColumnName("usdc_sent_count");
            entity.Property(e => e.UsdcSentAmount).HasColumnName("usdc_sent_amount").HasPrecision(18, 6);
            entity.Property(e => e.UsdcReceivedCount).HasColumnName("usdc_received_count");
            entity.Property(e => e.UsdcReceivedAmount).HasColumnName("usdc_received_amount").HasPrecision(18, 6);
            entity.Property(e => e.UsdtSentCount).HasColumnName("usdt_sent_count");
            entity.Property(e => e.UsdtSentAmount).HasColumnName("usdt_sent_amount").HasPrecision(18, 6);
            entity.Property(e => e.UsdtReceivedCount).HasColumnName("usdt_received_count");
            entity.Property(e => e.UsdtReceivedAmount).HasColumnName("usdt_received_amount").HasPrecision(18, 6);
            entity.Property(e => e.TotalFeesPaid).HasColumnName("total_fees_paid").HasPrecision(18, 6);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            // Computed properties - ignored
            entity.Ignore(e => e.TotalSentAmount);
            entity.Ignore(e => e.TotalReceivedAmount);
            entity.Ignore(e => e.TotalTransactionCount);

            entity.HasIndex(e => new { e.UserId, e.Date }).IsUnique();

            entity.HasOne(e => e.User)
                  .WithMany(u => u.DailyTransferSummaries)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // =====================================================================
        // AuditLog configuration
        // =====================================================================
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Action).HasColumnName("action").HasMaxLength(100).IsRequired();
            entity.Property(e => e.EntityType).HasColumnName("entity_type").HasMaxLength(50).IsRequired();
            entity.Property(e => e.EntityId).HasColumnName("entity_id").HasMaxLength(100);
            entity.Property(e => e.OldValues).HasColumnName("old_values").HasColumnType("jsonb");
            entity.Property(e => e.NewValues).HasColumnName("new_values").HasColumnType("jsonb");
            entity.Property(e => e.IpAddress).HasColumnName("ip_address").HasMaxLength(50);
            entity.Property(e => e.UserAgent).HasColumnName("user_agent").HasMaxLength(500);
            entity.Property(e => e.RequestId).HasColumnName("request_id").HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.Action);
            entity.HasIndex(e => new { e.EntityType, e.EntityId });
        });
    }
}
