-- PingPay Initial Database Schema
-- Run this against your PostgreSQL database

-- Enable UUID extension
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Users table
CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    phone_number VARCHAR(20) NOT NULL UNIQUE,
    display_name VARCHAR(100),
    is_verified BOOLEAN NOT NULL DEFAULT FALSE,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    is_frozen BOOLEAN NOT NULL DEFAULT FALSE,
    daily_transfer_limit DECIMAL(18, 6) NOT NULL DEFAULT 1000,
    daily_transferred_amount DECIMAL(18, 6) NOT NULL DEFAULT 0,
    daily_limit_reset_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_users_phone_number ON users(phone_number);

-- Wallets table
CREATE TABLE wallets (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL UNIQUE REFERENCES users(id) ON DELETE CASCADE,
    public_key VARCHAR(64) NOT NULL UNIQUE,
    encrypted_private_key TEXT NOT NULL,
    key_version VARCHAR(100) NOT NULL,
    cached_usdc_balance DECIMAL(18, 6) NOT NULL DEFAULT 0,
    cached_usdt_balance DECIMAL(18, 6) NOT NULL DEFAULT 0,
    balance_last_updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_wallets_public_key ON wallets(public_key);
CREATE INDEX idx_wallets_user_id ON wallets(user_id);

-- Transactions table
CREATE TABLE transactions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    idempotency_key VARCHAR(64) NOT NULL UNIQUE,
    sender_id UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    receiver_id UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    amount DECIMAL(18, 6) NOT NULL,
    token_type SMALLINT NOT NULL,
    status SMALLINT NOT NULL DEFAULT 0,
    type SMALLINT NOT NULL DEFAULT 0,
    solana_signature VARCHAR(100),
    error_message VARCHAR(500),
    retry_count INT NOT NULL DEFAULT 0,
    confirmed_at TIMESTAMP WITH TIME ZONE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_transactions_idempotency_key ON transactions(idempotency_key);
CREATE INDEX idx_transactions_sender_id ON transactions(sender_id);
CREATE INDEX idx_transactions_receiver_id ON transactions(receiver_id);
CREATE INDEX idx_transactions_status ON transactions(status);
CREATE INDEX idx_transactions_solana_signature ON transactions(solana_signature);
CREATE INDEX idx_transactions_created_at ON transactions(created_at);

-- Audit logs table
CREATE TABLE audit_logs (
    id BIGSERIAL PRIMARY KEY,
    user_id UUID,
    action VARCHAR(100) NOT NULL,
    entity_type VARCHAR(50) NOT NULL,
    entity_id VARCHAR(50),
    old_values JSONB,
    new_values JSONB,
    ip_address VARCHAR(50),
    user_agent VARCHAR(500),
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_audit_logs_user_id ON audit_logs(user_id);
CREATE INDEX idx_audit_logs_created_at ON audit_logs(created_at);
CREATE INDEX idx_audit_logs_action ON audit_logs(action);

-- Function to update updated_at timestamp
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ language 'plpgsql';

-- Triggers for updated_at
CREATE TRIGGER update_users_updated_at
    BEFORE UPDATE ON users
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_transactions_updated_at
    BEFORE UPDATE ON transactions
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Comments for documentation
COMMENT ON TABLE users IS 'PingPay user accounts identified by phone number';
COMMENT ON TABLE wallets IS 'Solana wallets with encrypted private keys';
COMMENT ON TABLE transactions IS 'Payment transactions between users';
COMMENT ON TABLE audit_logs IS 'Audit trail for security and compliance';

COMMENT ON COLUMN wallets.encrypted_private_key IS 'Envelope-encrypted private key (IV + encrypted DEK + encrypted key)';
COMMENT ON COLUMN wallets.key_version IS 'KMS/Key Vault key version used for envelope encryption';
COMMENT ON COLUMN transactions.token_type IS '0=USDC, 1=USDT';
COMMENT ON COLUMN transactions.status IS '0=Pending, 1=Processing, 2=Confirmed, 3=Failed, 4=Cancelled';
COMMENT ON COLUMN transactions.type IS '0=Transfer, 1=Withdrawal, 2=Deposit';
