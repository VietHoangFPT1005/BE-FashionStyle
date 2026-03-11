-- ================================================================
-- CHAT SUPPORT MIGRATION
-- Tạo bảng ChatSupportMessages để chat giữa Customer và Staff/Admin
-- Chạy script này trong PostgreSQL, sau đó scaffold lại EF Entities
-- ================================================================

-- Tạo bảng tin nhắn hỗ trợ
CREATE TABLE IF NOT EXISTS "ChatSupportMessages" (
    "Id"         SERIAL PRIMARY KEY,
    "CustomerId" INT NOT NULL,           -- ID của khách hàng trong cuộc hội thoại
    "SenderId"   INT NOT NULL,           -- ID người gửi (Customer hoặc Staff/Admin)
    "SenderRole" INT NOT NULL,           -- 1=Admin, 2=Staff, 3=Customer
    "Message"    TEXT NOT NULL,          -- Nội dung tin nhắn
    "IsRead"     BOOLEAN NOT NULL DEFAULT FALSE,   -- Customer đã đọc chưa (staff đọc)
    "CreatedAt"  TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "FK_ChatSupportMessages_Customer"
        FOREIGN KEY ("CustomerId") REFERENCES "Users"("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_ChatSupportMessages_Sender"
        FOREIGN KEY ("SenderId") REFERENCES "Users"("Id") ON DELETE CASCADE
);

-- Index để query nhanh theo CustomerId (load lịch sử chat)
CREATE INDEX IF NOT EXISTS "Idx_ChatSupportMessages_Customer"
    ON "ChatSupportMessages"("CustomerId");

-- Index để query nhanh theo thời gian
CREATE INDEX IF NOT EXISTS "Idx_ChatSupportMessages_CreatedAt"
    ON "ChatSupportMessages"("CreatedAt");

-- Index để đếm tin nhắn chưa đọc nhanh
CREATE INDEX IF NOT EXISTS "Idx_ChatSupportMessages_IsRead"
    ON "ChatSupportMessages"("CustomerId", "IsRead");
