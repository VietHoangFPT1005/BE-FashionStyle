-- =====================================================================
-- SỬA 3: Thêm audit fields vào bảng "Orders"
-- SỬA 4: Thêm fields vào bảng "Payments"
-- Database: FASHION-DB (PostgreSQL)
--
-- HƯỚNG DẪN: Chạy script này trước, sau đó re-scaffold lại Entities
-- Command scaffold:
--   dotnet ef dbcontext scaffold "Host=localhost;Port=5432;Database=FASHION-DB;Username=postgres;Password=12345" Npgsql.EntityFrameworkCore.PostgreSQL --output-dir ../MV.DomainLayer/Entities --context-dir DBContext --context FashionDbContext --force --project MV.InfrastructureLayer
-- =====================================================================

-- ======================== SỬA 3: Orders ========================

-- Audit trail: ai đã xác nhận / giao hàng / hủy đơn
ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "UpdatedAt" TIMESTAMP WITHOUT TIME ZONE;
ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "ConfirmedBy" INTEGER;
ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "ShippedBy" INTEGER;
ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "CancelledBy" INTEGER;

-- Shipping tracking
ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "TrackingNumber" VARCHAR(100);
ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "Carrier" VARCHAR(100);
ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "ExpectedDeliveryDate" TIMESTAMP WITHOUT TIME ZONE;

-- FK constraints cho audit fields (tham chiếu đến bảng Users)
ALTER TABLE "Orders"
    ADD CONSTRAINT "Orders_ConfirmedBy_fkey"
    FOREIGN KEY ("ConfirmedBy") REFERENCES "Users"("Id") ON DELETE SET NULL;

ALTER TABLE "Orders"
    ADD CONSTRAINT "Orders_ShippedBy_fkey"
    FOREIGN KEY ("ShippedBy") REFERENCES "Users"("Id") ON DELETE SET NULL;

ALTER TABLE "Orders"
    ADD CONSTRAINT "Orders_CancelledBy_fkey"
    FOREIGN KEY ("CancelledBy") REFERENCES "Users"("Id") ON DELETE SET NULL;


-- ======================== SỬA 4: Payments ========================

-- Số tiền thực tế nhận được (có thể khác Amount do chênh lệch)
ALTER TABLE "Payments" ADD COLUMN IF NOT EXISTS "ReceivedAmount" NUMERIC(15,2);

-- Thông tin bank từ webhook SePay
ALTER TABLE "Payments" ADD COLUMN IF NOT EXISTS "BankCode" VARCHAR(50);

-- Mã tham chiếu thanh toán (referenceNumber từ SePay)
ALTER TABLE "Payments" ADD COLUMN IF NOT EXISTS "PaymentReference" VARCHAR(255);

-- URL QR code đã tạo
ALTER TABLE "Payments" ADD COLUMN IF NOT EXISTS "QrCodeUrl" VARCHAR(500);

-- Admin xác nhận thủ công: ai xác nhận + thời điểm
ALTER TABLE "Payments" ADD COLUMN IF NOT EXISTS "VerifiedBy" INTEGER;
ALTER TABLE "Payments" ADD COLUMN IF NOT EXISTS "VerifiedAt" TIMESTAMP WITHOUT TIME ZONE;

-- FK constraint cho VerifiedBy
ALTER TABLE "Payments"
    ADD CONSTRAINT "Payments_VerifiedBy_fkey"
    FOREIGN KEY ("VerifiedBy") REFERENCES "Users"("Id") ON DELETE SET NULL;


-- ======================== VERIFY ========================
-- Kiểm tra kết quả sau khi chạy:
-- SELECT column_name, data_type, is_nullable
-- FROM information_schema.columns
-- WHERE table_name = 'Orders'
-- ORDER BY ordinal_position;
--
-- SELECT column_name, data_type, is_nullable
-- FROM information_schema.columns
-- WHERE table_name = 'Payments'
-- ORDER BY ordinal_position;
