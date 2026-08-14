-- Products 資料表
-- 注意：created_at / updated_at 由 production code（透過注入的 TimeProvider）決定，
-- 刻意不使用 DB 觸發器覆寫 updated_at，才能讓 FakeTimeProvider 在整合測試中完整控制時間戳記。
CREATE TABLE IF NOT EXISTS products
(
    id         UUID           PRIMARY KEY DEFAULT gen_random_uuid(),
    name       VARCHAR(200)   NOT NULL,
    price      DECIMAL(10, 2) NOT NULL,
    created_at TIMESTAMPTZ    NOT NULL,
    updated_at TIMESTAMPTZ    NOT NULL
);

-- 建立索引以提升查詢效能
CREATE INDEX IF NOT EXISTS idx_products_name ON products (name);
CREATE INDEX IF NOT EXISTS idx_products_price ON products (price);
CREATE INDEX IF NOT EXISTS idx_products_created_at ON products (created_at);
CREATE INDEX IF NOT EXISTS idx_products_updated_at ON products (updated_at);
