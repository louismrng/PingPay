-- ============================================================================
-- PingPay Database Schema v1.0
-- Production-ready PostgreSQL schema for Solana payment platform
-- ============================================================================

-- Enable required extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- ============================================================================
-- ENUM TYPES (for type safety and clarity)
-- ============================================================================

CREATE TYPE token_type AS ENUM ('USDC', 'USDT');
CREATE TYPE transaction_status AS ENUM ('pending', 'processing', 'confirmed', 'failed', 'cancelled');
CREATE TYPE transaction_type AS ENUM ('transfer', 'withdrawal', 'deposit');
CREATE TYPE audit_action AS ENUM (
    'user_registered', 'user_login', 'user_frozen', 'user_unfrozen',
    'wallet_created', 'payment_sent', 'payment_received',
    'withdrawal_requested', 'withdrawal_completed', 'withdrawal_failed',
    'limit_changed', 'whitelist_added', 'whitelist_removed'
);

-- ============================================================================
-- CORE TABLES
-- ============================================================================

-- Users table
CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    phone_number VARCHAR(20) NOT NULL,
    phone_country_code VARCHAR(5) NOT NULL DEFAULT '+1',
    display_name VARCHAR(100),
    is_verified BOOLEAN NOT NULL DEFAULT FALSE,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    is_frozen BOOLEAN NOT NULL DEFAULT FALSE,
    frozen_reason VARCHAR(500),
    frozen_at TIMESTAMP WITH TIME ZONE,
    frozen_by UUID,

    -- Transfer limits
    daily_transfer_limit DECIMAL(18, 6) NOT NULL DEFAULT 1000.000000,
    daily_transferred_amount DECIMAL(18, 6) NOT NULL DEFAULT 0.000000,
    daily_limit_reset_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT (CURRENT_DATE + INTERVAL '1 day'),
    monthly_transfer_limit DECIMAL(18, 6) NOT NULL DEFAULT 10000.000000,
    monthly_transferred_amount DECIMAL(18, 6) NOT NULL DEFAULT 0.000000,
    monthly_limit_reset_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT (DATE_TRUNC('month', CURRENT_DATE) + INTERVAL '1 month'),

    -- Metadata
    last_login_at TIMESTAMP WITH TIME ZONE,
    last_activity_at TIMESTAMP WITH TIME ZONE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,

    -- Constraints
    CONSTRAINT users_phone_number_unique UNIQUE (phone_country_code, phone_number),
    CONSTRAINT users_daily_limit_positive CHECK (daily_transfer_limit >= 0),
    CONSTRAINT users_monthly_limit_positive CHECK (monthly_transfer_limit >= 0),
    CONSTRAINT users_daily_transferred_non_negative CHECK (daily_transferred_amount >= 0),
    CONSTRAINT users_monthly_transferred_non_negative CHECK (monthly_transferred_amount >= 0)
);

-- Wallets table (1:1 with users)
CREATE TABLE wallets (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL UNIQUE REFERENCES users(id) ON DELETE RESTRICT,

    -- Solana keys
    public_key VARCHAR(44) NOT NULL,
    encrypted_private_key TEXT NOT NULL,
    key_version VARCHAR(100) NOT NULL,
    key_algorithm VARCHAR(50) NOT NULL DEFAULT 'AES-256-GCM',

    -- Cached balances (updated periodically)
    cached_usdc_balance DECIMAL(18, 6) NOT NULL DEFAULT 0.000000,
    cached_usdt_balance DECIMAL(18, 6) NOT NULL DEFAULT 0.000000,
    cached_sol_balance DECIMAL(18, 9) NOT NULL DEFAULT 0.000000000,
    balance_last_updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,

    -- Metadata
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,

    -- Constraints
    CONSTRAINT wallets_public_key_unique UNIQUE (public_key),
    CONSTRAINT wallets_public_key_length CHECK (LENGTH(public_key) BETWEEN 32 AND 44),
    CONSTRAINT wallets_balances_non_negative CHECK (
        cached_usdc_balance >= 0 AND
        cached_usdt_balance >= 0 AND
        cached_sol_balance >= 0
    )
);

-- Transactions table
CREATE TABLE transactions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    idempotency_key VARCHAR(64) NOT NULL,

    -- Participants
    sender_id UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    receiver_id UUID REFERENCES users(id) ON DELETE RESTRICT,

    -- For withdrawals to external addresses
    external_address VARCHAR(44),

    -- Transaction details
    amount DECIMAL(18, 6) NOT NULL,
    fee_amount DECIMAL(18, 6) NOT NULL DEFAULT 0.000000,
    token token_type NOT NULL,
    status transaction_status NOT NULL DEFAULT 'pending',
    type transaction_type NOT NULL DEFAULT 'transfer',

    -- Solana details
    solana_signature VARCHAR(88),
    solana_slot BIGINT,
    solana_block_time TIMESTAMP WITH TIME ZONE,

    -- Error handling
    error_code VARCHAR(50),
    error_message VARCHAR(1000),
    retry_count SMALLINT NOT NULL DEFAULT 0,
    max_retries SMALLINT NOT NULL DEFAULT 3,
    next_retry_at TIMESTAMP WITH TIME ZONE,

    -- Timestamps
    confirmed_at TIMESTAMP WITH TIME ZONE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,

    -- Constraints
    CONSTRAINT transactions_idempotency_key_unique UNIQUE (idempotency_key),
    CONSTRAINT transactions_amount_positive CHECK (amount > 0),
    CONSTRAINT transactions_fee_non_negative CHECK (fee_amount >= 0),
    CONSTRAINT transactions_retry_count_valid CHECK (retry_count >= 0 AND retry_count <= max_retries),
    CONSTRAINT transactions_external_address_for_withdrawal CHECK (
        (type = 'withdrawal' AND external_address IS NOT NULL) OR
        (type != 'withdrawal' AND external_address IS NULL)
    ),
    CONSTRAINT transactions_receiver_for_transfer CHECK (
        (type = 'transfer' AND receiver_id IS NOT NULL) OR
        (type != 'transfer')
    )
);

-- ============================================================================
-- SUPPORTING TABLES
-- ============================================================================

-- Withdrawal address whitelist
CREATE TABLE withdrawal_whitelist (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    address VARCHAR(44) NOT NULL,
    label VARCHAR(100),
    is_verified BOOLEAN NOT NULL DEFAULT FALSE,
    verified_at TIMESTAMP WITH TIME ZONE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT withdrawal_whitelist_user_address_unique UNIQUE (user_id, address),
    CONSTRAINT withdrawal_whitelist_address_length CHECK (LENGTH(address) BETWEEN 32 AND 44)
);

-- Fee schedule
CREATE TABLE fee_schedule (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(100) NOT NULL,
    transaction_type transaction_type NOT NULL,
    token token_type NOT NULL,

    -- Fee structure (either flat or percentage)
    flat_fee DECIMAL(18, 6) NOT NULL DEFAULT 0.000000,
    percentage_fee DECIMAL(5, 4) NOT NULL DEFAULT 0.0000, -- e.g., 0.0025 = 0.25%
    min_fee DECIMAL(18, 6) NOT NULL DEFAULT 0.000000,
    max_fee DECIMAL(18, 6),

    -- Thresholds
    min_amount DECIMAL(18, 6) NOT NULL DEFAULT 0.000000,
    max_amount DECIMAL(18, 6),

    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    effective_from TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    effective_until TIMESTAMP WITH TIME ZONE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fee_schedule_percentage_valid CHECK (percentage_fee >= 0 AND percentage_fee <= 1),
    CONSTRAINT fee_schedule_flat_fee_non_negative CHECK (flat_fee >= 0),
    CONSTRAINT fee_schedule_min_max_valid CHECK (max_amount IS NULL OR min_amount <= max_amount)
);

-- System settings (key-value store for global config)
CREATE TABLE system_settings (
    key VARCHAR(100) PRIMARY KEY,
    value JSONB NOT NULL,
    description VARCHAR(500),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by UUID
);

-- Audit logs (append-only)
CREATE TABLE audit_logs (
    id BIGSERIAL PRIMARY KEY,
    user_id UUID,
    action VARCHAR(100) NOT NULL,
    entity_type VARCHAR(50) NOT NULL,
    entity_id VARCHAR(100),

    -- Change tracking
    old_values JSONB,
    new_values JSONB,

    -- Request context
    ip_address INET,
    user_agent VARCHAR(500),
    request_id VARCHAR(100),

    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Daily transfer history (for compliance/reporting)
CREATE TABLE daily_transfer_summaries (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    date DATE NOT NULL,

    -- USDC totals
    usdc_sent_count INT NOT NULL DEFAULT 0,
    usdc_sent_amount DECIMAL(18, 6) NOT NULL DEFAULT 0.000000,
    usdc_received_count INT NOT NULL DEFAULT 0,
    usdc_received_amount DECIMAL(18, 6) NOT NULL DEFAULT 0.000000,

    -- USDT totals
    usdt_sent_count INT NOT NULL DEFAULT 0,
    usdt_sent_amount DECIMAL(18, 6) NOT NULL DEFAULT 0.000000,
    usdt_received_count INT NOT NULL DEFAULT 0,
    usdt_received_amount DECIMAL(18, 6) NOT NULL DEFAULT 0.000000,

    -- Fees paid
    total_fees_paid DECIMAL(18, 6) NOT NULL DEFAULT 0.000000,

    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT daily_transfer_summaries_user_date_unique UNIQUE (user_id, date)
);

-- ============================================================================
-- INDEXES
-- ============================================================================

-- Users indexes
CREATE INDEX idx_users_phone ON users(phone_country_code, phone_number);
CREATE INDEX idx_users_is_active ON users(is_active) WHERE is_active = TRUE;
CREATE INDEX idx_users_is_frozen ON users(is_frozen) WHERE is_frozen = TRUE;
CREATE INDEX idx_users_daily_reset ON users(daily_limit_reset_at) WHERE daily_transferred_amount > 0;
CREATE INDEX idx_users_created_at ON users(created_at);

-- Wallets indexes
CREATE INDEX idx_wallets_user_id ON wallets(user_id);
CREATE INDEX idx_wallets_balance_updated ON wallets(balance_last_updated_at);

-- Transactions indexes (most important for performance)
CREATE INDEX idx_transactions_sender_id ON transactions(sender_id);
CREATE INDEX idx_transactions_receiver_id ON transactions(receiver_id) WHERE receiver_id IS NOT NULL;
CREATE INDEX idx_transactions_status ON transactions(status);
CREATE INDEX idx_transactions_created_at ON transactions(created_at DESC);
CREATE INDEX idx_transactions_solana_signature ON transactions(solana_signature) WHERE solana_signature IS NOT NULL;

-- Composite indexes for common queries
CREATE INDEX idx_transactions_sender_created ON transactions(sender_id, created_at DESC);
CREATE INDEX idx_transactions_receiver_created ON transactions(receiver_id, created_at DESC) WHERE receiver_id IS NOT NULL;
CREATE INDEX idx_transactions_status_created ON transactions(status, created_at) WHERE status IN ('pending', 'processing');
CREATE INDEX idx_transactions_pending_retry ON transactions(next_retry_at)
    WHERE status IN ('pending', 'processing') AND retry_count < max_retries;

-- User transaction history (covering index)
CREATE INDEX idx_transactions_user_history ON transactions(sender_id, created_at DESC, status, type, amount);

-- Audit logs indexes
CREATE INDEX idx_audit_logs_user_id ON audit_logs(user_id) WHERE user_id IS NOT NULL;
CREATE INDEX idx_audit_logs_created_at ON audit_logs(created_at DESC);
CREATE INDEX idx_audit_logs_action ON audit_logs(action);
CREATE INDEX idx_audit_logs_entity ON audit_logs(entity_type, entity_id);

-- Withdrawal whitelist indexes
CREATE INDEX idx_withdrawal_whitelist_user ON withdrawal_whitelist(user_id);

-- Daily summaries indexes
CREATE INDEX idx_daily_summaries_user_date ON daily_transfer_summaries(user_id, date DESC);

-- ============================================================================
-- FUNCTIONS
-- ============================================================================

-- Auto-update updated_at timestamp
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Reset daily transfer limits
CREATE OR REPLACE FUNCTION reset_daily_limits()
RETURNS INTEGER AS $$
DECLARE
    affected_rows INTEGER;
BEGIN
    UPDATE users
    SET
        daily_transferred_amount = 0,
        daily_limit_reset_at = CURRENT_DATE + INTERVAL '1 day',
        updated_at = CURRENT_TIMESTAMP
    WHERE daily_limit_reset_at <= CURRENT_TIMESTAMP
      AND daily_transferred_amount > 0;

    GET DIAGNOSTICS affected_rows = ROW_COUNT;
    RETURN affected_rows;
END;
$$ LANGUAGE plpgsql;

-- Reset monthly transfer limits
CREATE OR REPLACE FUNCTION reset_monthly_limits()
RETURNS INTEGER AS $$
DECLARE
    affected_rows INTEGER;
BEGIN
    UPDATE users
    SET
        monthly_transferred_amount = 0,
        monthly_limit_reset_at = DATE_TRUNC('month', CURRENT_DATE) + INTERVAL '1 month',
        updated_at = CURRENT_TIMESTAMP
    WHERE monthly_limit_reset_at <= CURRENT_TIMESTAMP
      AND monthly_transferred_amount > 0;

    GET DIAGNOSTICS affected_rows = ROW_COUNT;
    RETURN affected_rows;
END;
$$ LANGUAGE plpgsql;

-- Calculate fee for a transaction
CREATE OR REPLACE FUNCTION calculate_transaction_fee(
    p_transaction_type transaction_type,
    p_token token_type,
    p_amount DECIMAL(18, 6)
)
RETURNS DECIMAL(18, 6) AS $$
DECLARE
    v_fee DECIMAL(18, 6) := 0;
    v_schedule fee_schedule%ROWTYPE;
BEGIN
    SELECT * INTO v_schedule
    FROM fee_schedule
    WHERE transaction_type = p_transaction_type
      AND token = p_token
      AND is_active = TRUE
      AND effective_from <= CURRENT_TIMESTAMP
      AND (effective_until IS NULL OR effective_until > CURRENT_TIMESTAMP)
      AND p_amount >= min_amount
      AND (max_amount IS NULL OR p_amount <= max_amount)
    ORDER BY min_amount DESC
    LIMIT 1;

    IF FOUND THEN
        -- Calculate percentage + flat fee
        v_fee := v_schedule.flat_fee + (p_amount * v_schedule.percentage_fee);

        -- Apply min/max bounds
        IF v_fee < v_schedule.min_fee THEN
            v_fee := v_schedule.min_fee;
        ELSIF v_schedule.max_fee IS NOT NULL AND v_fee > v_schedule.max_fee THEN
            v_fee := v_schedule.max_fee;
        END IF;
    END IF;

    RETURN v_fee;
END;
$$ LANGUAGE plpgsql STABLE;

-- Check if user can transfer amount
CREATE OR REPLACE FUNCTION can_user_transfer(
    p_user_id UUID,
    p_amount DECIMAL(18, 6)
)
RETURNS TABLE (
    allowed BOOLEAN,
    reason VARCHAR(100),
    remaining_daily DECIMAL(18, 6),
    remaining_monthly DECIMAL(18, 6)
) AS $$
DECLARE
    v_user users%ROWTYPE;
BEGIN
    SELECT * INTO v_user FROM users WHERE id = p_user_id;

    IF NOT FOUND THEN
        RETURN QUERY SELECT FALSE, 'User not found'::VARCHAR(100), 0::DECIMAL(18,6), 0::DECIMAL(18,6);
        RETURN;
    END IF;

    IF NOT v_user.is_active THEN
        RETURN QUERY SELECT FALSE, 'Account inactive'::VARCHAR(100), 0::DECIMAL(18,6), 0::DECIMAL(18,6);
        RETURN;
    END IF;

    IF v_user.is_frozen THEN
        RETURN QUERY SELECT FALSE, 'Account frozen'::VARCHAR(100), 0::DECIMAL(18,6), 0::DECIMAL(18,6);
        RETURN;
    END IF;

    -- Reset limits if needed
    IF v_user.daily_limit_reset_at <= CURRENT_TIMESTAMP THEN
        v_user.daily_transferred_amount := 0;
    END IF;

    IF v_user.monthly_limit_reset_at <= CURRENT_TIMESTAMP THEN
        v_user.monthly_transferred_amount := 0;
    END IF;

    -- Check daily limit
    IF v_user.daily_transferred_amount + p_amount > v_user.daily_transfer_limit THEN
        RETURN QUERY SELECT
            FALSE,
            'Daily limit exceeded'::VARCHAR(100),
            GREATEST(0, v_user.daily_transfer_limit - v_user.daily_transferred_amount),
            GREATEST(0, v_user.monthly_transfer_limit - v_user.monthly_transferred_amount);
        RETURN;
    END IF;

    -- Check monthly limit
    IF v_user.monthly_transferred_amount + p_amount > v_user.monthly_transfer_limit THEN
        RETURN QUERY SELECT
            FALSE,
            'Monthly limit exceeded'::VARCHAR(100),
            GREATEST(0, v_user.daily_transfer_limit - v_user.daily_transferred_amount),
            GREATEST(0, v_user.monthly_transfer_limit - v_user.monthly_transferred_amount);
        RETURN;
    END IF;

    RETURN QUERY SELECT
        TRUE,
        NULL::VARCHAR(100),
        GREATEST(0, v_user.daily_transfer_limit - v_user.daily_transferred_amount - p_amount),
        GREATEST(0, v_user.monthly_transfer_limit - v_user.monthly_transferred_amount - p_amount);
END;
$$ LANGUAGE plpgsql;

-- ============================================================================
-- TRIGGERS
-- ============================================================================

CREATE TRIGGER update_users_updated_at
    BEFORE UPDATE ON users
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_transactions_updated_at
    BEFORE UPDATE ON transactions
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_daily_summaries_updated_at
    BEFORE UPDATE ON daily_transfer_summaries
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- ============================================================================
-- VIEWS
-- ============================================================================

-- User balance view (combines user and wallet data)
CREATE VIEW v_user_balances AS
SELECT
    u.id AS user_id,
    u.phone_country_code || u.phone_number AS phone_number,
    u.display_name,
    u.is_active,
    u.is_frozen,
    w.public_key,
    w.cached_usdc_balance,
    w.cached_usdt_balance,
    w.cached_sol_balance,
    w.balance_last_updated_at,
    u.daily_transfer_limit,
    u.daily_transferred_amount,
    u.daily_transfer_limit - u.daily_transferred_amount AS daily_remaining,
    u.monthly_transfer_limit,
    u.monthly_transferred_amount,
    u.monthly_transfer_limit - u.monthly_transferred_amount AS monthly_remaining
FROM users u
LEFT JOIN wallets w ON u.id = w.user_id
WHERE u.is_active = TRUE;

-- Pending transactions view
CREATE VIEW v_pending_transactions AS
SELECT
    t.id,
    t.idempotency_key,
    t.sender_id,
    s.phone_country_code || s.phone_number AS sender_phone,
    t.receiver_id,
    r.phone_country_code || r.phone_number AS receiver_phone,
    t.external_address,
    t.amount,
    t.fee_amount,
    t.token,
    t.status,
    t.type,
    t.retry_count,
    t.max_retries,
    t.next_retry_at,
    t.error_message,
    t.created_at,
    EXTRACT(EPOCH FROM (CURRENT_TIMESTAMP - t.created_at)) AS age_seconds
FROM transactions t
JOIN users s ON t.sender_id = s.id
LEFT JOIN users r ON t.receiver_id = r.id
WHERE t.status IN ('pending', 'processing')
ORDER BY t.created_at ASC;

-- Transaction statistics view
CREATE VIEW v_transaction_stats AS
SELECT
    DATE(created_at) AS date,
    token,
    type,
    status,
    COUNT(*) AS transaction_count,
    SUM(amount) AS total_amount,
    SUM(fee_amount) AS total_fees,
    AVG(amount) AS avg_amount,
    MIN(amount) AS min_amount,
    MAX(amount) AS max_amount
FROM transactions
WHERE created_at >= CURRENT_DATE - INTERVAL '30 days'
GROUP BY DATE(created_at), token, type, status
ORDER BY date DESC, token, type, status;

-- ============================================================================
-- INITIAL DATA
-- ============================================================================

-- Default system settings
INSERT INTO system_settings (key, value, description) VALUES
('maintenance_mode', 'false', 'Global maintenance mode flag'),
('new_registrations_enabled', 'true', 'Allow new user registrations'),
('withdrawals_enabled', 'true', 'Allow withdrawals to external addresses'),
('default_daily_limit', '1000', 'Default daily transfer limit for new users'),
('default_monthly_limit', '10000', 'Default monthly transfer limit for new users'),
('min_transfer_amount', '0.01', 'Minimum transfer amount'),
('max_transfer_amount', '10000', 'Maximum single transfer amount'),
('balance_cache_ttl_seconds', '30', 'How long to cache wallet balances');

-- Default fee schedule (0 fees for MVP)
INSERT INTO fee_schedule (name, transaction_type, token, flat_fee, percentage_fee, min_fee, min_amount) VALUES
('Transfer USDC - Free Tier', 'transfer', 'USDC', 0, 0, 0, 0),
('Transfer USDT - Free Tier', 'transfer', 'USDT', 0, 0, 0, 0),
('Withdrawal USDC - Free Tier', 'withdrawal', 'USDC', 0, 0, 0, 0),
('Withdrawal USDT - Free Tier', 'withdrawal', 'USDT', 0, 0, 0, 0);

-- ============================================================================
-- COMMENTS
-- ============================================================================

COMMENT ON TABLE users IS 'PingPay user accounts identified by phone number';
COMMENT ON TABLE wallets IS 'Solana wallets with envelope-encrypted private keys';
COMMENT ON TABLE transactions IS 'All payment transactions (transfers, withdrawals, deposits)';
COMMENT ON TABLE withdrawal_whitelist IS 'User-approved external withdrawal addresses';
COMMENT ON TABLE fee_schedule IS 'Transaction fee configuration by type and token';
COMMENT ON TABLE system_settings IS 'Global system configuration (key-value store)';
COMMENT ON TABLE audit_logs IS 'Immutable audit trail for compliance';
COMMENT ON TABLE daily_transfer_summaries IS 'Pre-aggregated daily transfer statistics per user';

COMMENT ON COLUMN wallets.encrypted_private_key IS 'Envelope-encrypted private key using AES-256-GCM with KMS-wrapped DEK';
COMMENT ON COLUMN wallets.key_version IS 'KMS/Key Vault key version used for envelope encryption';
COMMENT ON COLUMN transactions.idempotency_key IS 'Client-provided key to prevent duplicate transactions';
COMMENT ON COLUMN transactions.solana_signature IS 'Solana transaction signature (base58 encoded, 88 chars)';

COMMENT ON FUNCTION reset_daily_limits() IS 'Resets daily transfer counters for users past their reset time. Call via scheduled job.';
COMMENT ON FUNCTION reset_monthly_limits() IS 'Resets monthly transfer counters for users past their reset time. Call via scheduled job.';
COMMENT ON FUNCTION calculate_transaction_fee(transaction_type, token_type, DECIMAL) IS 'Calculates the fee for a transaction based on active fee schedule';
COMMENT ON FUNCTION can_user_transfer(UUID, DECIMAL) IS 'Checks if user can transfer specified amount (validates limits and status)';

COMMENT ON VIEW v_user_balances IS 'Consolidated view of user accounts with wallet balances and remaining limits';
COMMENT ON VIEW v_pending_transactions IS 'Transactions awaiting processing or confirmation';
COMMENT ON VIEW v_transaction_stats IS 'Aggregated transaction statistics for the last 30 days';
