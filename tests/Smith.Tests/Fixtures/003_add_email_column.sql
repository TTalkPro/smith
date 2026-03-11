-- 添加邮件验证字段
ALTER TABLE users ADD COLUMN IF NOT EXISTS email_verified BOOLEAN DEFAULT FALSE;

CREATE INDEX idx_users_email_verified ON users (email_verified);
