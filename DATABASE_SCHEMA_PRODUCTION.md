# DATABASE SCHEMA - PRODUCTION EDITION
## SmartInvoice Shield - Tuân thủ 100% Nghị định 123/2020/NĐ-CP

**Version**: 1.0 Production  
**Database**: PostgreSQL 14+  
**Compliance**: Nghị định 123/2020/NĐ-CP + Quyết định 1550/QĐ-TCT

---

## 📋 MỤC LỤC

1. [Overview](#1-overview)
2. [Core Tables (6 tables)](#2-core-tables)
3. [Support Tables (6 tables)](#3-support-tables)
4. [Indexes Strategy](#4-indexes-strategy)
5. [Constraints & Rules](#5-constraints--rules)
6. [Migration Scripts](#6-migration-scripts)

---

## 1. OVERVIEW

### 1.1 Database Structure

```
Total Tables: 12
Total Indexes: 35+
Total Constraints: 40+
Estimated Size: 100GB (cho 1M invoices)

Multi-tenant: Yes (CompanyId partition)
Audit Trail: Complete (InvoiceAuditLogs)
Soft Delete: Yes (IsActive flags)
Version Control: Yes (Invoices.ReplacedBy)
```

### 1.2 Naming Conventions

```
Tables: PascalCase (Companies, Users, Invoices)
Columns: PascalCase (CompanyId, InvoiceNumber)
Indexes: idx_tablename_columnname
Constraints: chk/fk/pk_tablename_description
```

---

## 2. CORE TABLES

### 2.1 Companies (Multi-tenant Root)

```sql
CREATE TABLE Companies (
    -- Primary Key
    CompanyId UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    
    -- Basic Info
    CompanyName VARCHAR(200) NOT NULL,
    TaxCode VARCHAR(14) NOT NULL UNIQUE,  -- MST theo NĐ 123/2020
    Email VARCHAR(100) NOT NULL,
    PhoneNumber VARCHAR(20),
    Address TEXT,
    Website VARCHAR(200),
    
    -- Business Info
    LegalRepresentative VARCHAR(100),  -- Người đại diện pháp luật
    BusinessType VARCHAR(50),          -- Loại hình: TNHH, CP, Tư nhân...
    BusinessLicense VARCHAR(50),       -- Số ĐKKD
    
    -- Subscription Info
    SubscriptionTier VARCHAR(50) DEFAULT 'Free' 
        CHECK (SubscriptionTier IN ('Free', 'Starter', 'Professional', 'Enterprise')),
    SubscriptionStartDate TIMESTAMP,
    SubscriptionExpiredAt TIMESTAMP,
    MaxUsers INT DEFAULT 5,
    MaxInvoicesPerMonth INT DEFAULT 100,
    StorageQuotaGB INT DEFAULT 5,
    
    -- Status
    IsActive BOOLEAN DEFAULT true,
    RegistrationDate TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    
    -- Metadata
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CreatedBy UUID,  -- System or Admin
    
    -- Constraints
    CONSTRAINT chk_companies_tax_code_format 
        CHECK (TaxCode ~ '^[0-9]{10}$|^[0-9]{10}-[0-9]{3}$|^[0-9]{12}$'),
    CONSTRAINT chk_companies_subscription_dates
        CHECK (SubscriptionExpiredAt IS NULL OR SubscriptionExpiredAt > SubscriptionStartDate)
);

-- Indexes
CREATE INDEX idx_companies_tax_code ON Companies(TaxCode);
CREATE INDEX idx_companies_active ON Companies(IsActive) WHERE IsActive = true;
CREATE INDEX idx_companies_subscription ON Companies(SubscriptionTier, SubscriptionExpiredAt);

-- Comments
COMMENT ON TABLE Companies IS 'Công ty sử dụng hệ thống - Multi-tenant root entity';
COMMENT ON COLUMN Companies.TaxCode IS 'Mã số thuế theo NĐ 123/2020: 10 số, 13 số (MST-chi nhánh), hoặc 12 số (CCCD)';
COMMENT ON COLUMN Companies.SubscriptionTier IS 'Gói dịch vụ: Free (5 users), Starter (20), Pro (100), Enterprise (unlimited)';
```

---

### 2.2 Users (Authentication & Authorization)

```sql
CREATE TABLE Users (
    -- Primary Key
    UserId UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    
    -- Company Relation (Multi-tenant)
    CompanyId UUID NOT NULL REFERENCES Companies(CompanyId) ON DELETE CASCADE,
    
    -- Authentication
    Email VARCHAR(100) NOT NULL UNIQUE,
    PasswordHash VARCHAR(255) NOT NULL,  -- BCrypt hash
    Salt VARCHAR(50),                    -- For additional security
    
    -- Personal Info
    FullName VARCHAR(100) NOT NULL,
    PhoneNumber VARCHAR(20),
    EmployeeId VARCHAR(50),              -- Mã nhân viên nội bộ
    Department VARCHAR(100),             -- Phòng ban
    Position VARCHAR(100),               -- Chức vụ
    
    -- Authorization
    Role VARCHAR(50) NOT NULL DEFAULT 'Member'
        CHECK (Role IN ('Member', 'CompanyAdmin', 'SuperAdmin')),
    Permissions JSONB DEFAULT '[]'::jsonb,  -- Custom permissions array
    
    -- Security
    TwoFactorEnabled BOOLEAN DEFAULT false,
    TwoFactorSecret VARCHAR(100),
    LastPasswordChangeAt TIMESTAMP,
    MustChangePassword BOOLEAN DEFAULT false,
    FailedLoginAttempts INT DEFAULT 0,
    LockedUntil TIMESTAMP,
    
    -- Activity
    IsActive BOOLEAN DEFAULT true,
    LastLoginAt TIMESTAMP,
    LastLoginIP VARCHAR(50),
    LastLoginUserAgent TEXT,
    
    -- Metadata
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CreatedBy UUID REFERENCES Users(UserId),  -- Who created this user
    DeactivatedAt TIMESTAMP,
    DeactivatedBy UUID REFERENCES Users(UserId),
    
    -- Constraints
    CONSTRAINT chk_users_email_format CHECK (Email ~* '^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}$'),
    CONSTRAINT chk_users_role_admin CHECK (
        (Role = 'CompanyAdmin' AND CompanyId IS NOT NULL) OR
        (Role = 'SuperAdmin') OR
        (Role = 'Member' AND CompanyId IS NOT NULL)
    )
);

-- Indexes
CREATE INDEX idx_users_company_role ON Users(CompanyId, Role) WHERE IsActive = true;
CREATE INDEX idx_users_email ON Users(Email);
CREATE INDEX idx_users_last_login ON Users(LastLoginAt DESC);
CREATE UNIQUE INDEX idx_users_employee_company ON Users(CompanyId, EmployeeId) 
    WHERE EmployeeId IS NOT NULL AND IsActive = true;

-- Comments
COMMENT ON TABLE Users IS 'Người dùng hệ thống - Authentication & Authorization';
COMMENT ON COLUMN Users.Role IS 'Member: Kế toán viên | CompanyAdmin: Kế toán trưởng | SuperAdmin: System admin';
COMMENT ON COLUMN Users.Permissions IS 'Custom permissions JSONB array: ["invoice:approve", "user:create", etc.]';
```

---

### 2.3 DocumentTypes (Invoice Classifications)

```sql
CREATE TABLE DocumentTypes (
    -- Primary Key
    DocumentTypeId SERIAL PRIMARY KEY,
    
    -- Classification
    TypeCode VARCHAR(50) NOT NULL UNIQUE,
    TypeName VARCHAR(100) NOT NULL,
    TypeNameEN VARCHAR(100),
    Description TEXT,
    
    -- Compliance Rules (theo NĐ 123/2020)
    FormTemplate VARCHAR(20),              -- "01GTKT", "02GTTT", etc.
    RequiresXML BOOLEAN DEFAULT false,     -- Bắt buộc có file XML?
    RequiresDigitalSignature BOOLEAN DEFAULT false,
    RequiresMCCQT BOOLEAN DEFAULT false,   -- Bắt buộc Mã cơ quan thuế?
    RequiresVAT BOOLEAN DEFAULT false,     -- Có thuế GTGT?
    
    -- Validation Rules (JSONB)
    ValidationRules JSONB DEFAULT '{}'::jsonb,
    /*
    Example ValidationRules:
    {
      "required_fields": ["InvoiceDate", "TotalAmount", "SellerTaxCode"],
      "optional_fields": ["BuyerTaxCode", "PaymentMethod"],
      "regex_patterns": {
        "InvoiceNumber": "^[0-9]{7,8}$",
        "SerialNumber": "^[A-Z0-9]{2,10}$"
      },
      "amount_limits": {
        "min": 0,
        "max": 10000000000
      },
      "vat_rates": [0, 5, 8, 10]
    }
    */
    
    -- Processing Config
    ProcessingConfig JSONB DEFAULT '{}'::jsonb,
    /*
    {
      "ocr_enabled": true,
      "textract_model": "AnalyzeExpense",
      "confidence_threshold": 0.8,
      "auto_approve_threshold": 0.95
    }
    */
    
    -- Status
    IsActive BOOLEAN DEFAULT true,
    DisplayOrder INT DEFAULT 0,
    
    -- Metadata
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Indexes
CREATE INDEX idx_document_types_code ON DocumentTypes(TypeCode);
CREATE INDEX idx_document_types_active ON DocumentTypes(IsActive, DisplayOrder) 
    WHERE IsActive = true;

-- Seed Data (theo Nghị định 123/2020)
INSERT INTO DocumentTypes (TypeCode, TypeName, TypeNameEN, FormTemplate, RequiresXML, RequiresDigitalSignature, RequiresVAT, ValidationRules) VALUES
('GTGT', 'Hóa đơn GTGT (Mẫu 01)', 'VAT Invoice (Form 01)', '01GTKT', true, true, true, 
 '{"required_fields": ["InvoiceDate", "InvoiceNumber", "SerialNumber", "SellerTaxCode", "BuyerTaxCode", "TotalAmount", "VATAmount"], "vat_rates": [0, 5, 8, 10]}'::jsonb),

('SALE', 'Hóa đơn Bán hàng (Mẫu 02)', 'Sales Invoice (Form 02)', '02GTTT', true, true, false,
 '{"required_fields": ["InvoiceDate", "InvoiceNumber", "SerialNumber", "SellerTaxCode", "TotalAmount"]}'::jsonb),

('CASH_REGISTER', 'Hóa đơn Máy tính tiền', 'Cash Register Invoice', '', false, false, false,
 '{"required_fields": ["InvoiceDate", "TotalAmount", "MCCQT"]}'::jsonb),

('AIRPLANE', 'Vé máy bay', 'Airplane Ticket', '', false, false, false,
 '{"required_fields": ["InvoiceDate", "TotalAmount"], "optional_fields": ["FlightNumber", "Route", "PassengerName"]}'::jsonb),

('TAXI', 'Hóa đơn Taxi', 'Taxi Invoice', '', false, false, false,
 '{"required_fields": ["InvoiceDate", "TotalAmount"], "optional_fields": ["LicensePlate", "Distance", "DriverName"]}'::jsonb),

('BOT_TOLL', 'Vé cước BOT', 'Toll Receipt', '', false, false, false,
 '{"required_fields": ["InvoiceDate", "TotalAmount"], "optional_fields": ["StationName", "VehicleType"]}'::jsonb),

('UTILITIES', 'Hóa đơn Điện/Nước', 'Utility Bill', '', false, false, false,
 '{"required_fields": ["InvoiceDate", "TotalAmount"], "optional_fields": ["UsageAmount", "UnitPrice", "MeterNumber"]}'::jsonb),

('OCR_IMAGE', 'Hóa đơn Ảnh (OCR)', 'Image Invoice (OCR)', '', false, false, false,
 '{"processing_method": "OCR", "confidence_threshold": 0.8}'::jsonb);

-- Comments
COMMENT ON TABLE DocumentTypes IS 'Phân loại chứng từ theo NĐ 123/2020 và Quyết định 1550/QĐ-TCT';
COMMENT ON COLUMN DocumentTypes.RequiresMCCQT IS 'Mã cơ quan thuế - bắt buộc với hóa đơn máy tính tiền';
```

---

### 2.4 FileStorages (S3 Metadata)

```sql
CREATE TABLE FileStorages (
    -- Primary Key
    FileId UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    
    -- Company Relation
    CompanyId UUID NOT NULL REFERENCES Companies(CompanyId) ON DELETE CASCADE,
    UploadedBy UUID NOT NULL REFERENCES Users(UserId),
    
    -- File Info
    OriginalFileName VARCHAR(255) NOT NULL,
    FileExtension VARCHAR(10) NOT NULL 
        CHECK (FileExtension IN ('.xml', '.pdf', '.jpg', '.jpeg', '.png')),
    FileSize BIGINT NOT NULL CHECK (FileSize > 0 AND FileSize <= 10485760),  -- Max 10MB
    MimeType VARCHAR(100) NOT NULL,
    FileHash VARCHAR(64),  -- SHA-256 hash for deduplication
    
    -- S3 Info
    S3BucketName VARCHAR(100) NOT NULL,
    S3Key VARCHAR(500) NOT NULL UNIQUE,
    S3Region VARCHAR(50) DEFAULT 'ap-southeast-1',
    S3VersionId VARCHAR(100),  -- For S3 versioning
    S3Url TEXT,  -- Pre-signed URL (temporary)
    S3UrlExpiresAt TIMESTAMP,
    
    -- Processing Status
    IsProcessed BOOLEAN DEFAULT false,
    ProcessedAt TIMESTAMP,
    ProcessingError TEXT,
    
    -- Lifecycle
    ArchivedToGlacier BOOLEAN DEFAULT false,
    ArchivedAt TIMESTAMP,
    DeletedFromS3 BOOLEAN DEFAULT false,
    DeletedAt TIMESTAMP,
    
    -- Metadata
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    
    -- Constraints
    CONSTRAINT chk_files_size_limit CHECK (FileSize <= 10485760),
    CONSTRAINT chk_files_processed_time CHECK (ProcessedAt IS NULL OR ProcessedAt >= CreatedAt)
);

-- Indexes
CREATE INDEX idx_files_company_uploaded ON FileStorages(CompanyId, UploadedBy, CreatedAt DESC);
CREATE INDEX idx_files_processed ON FileStorages(IsProcessed, ProcessedAt);
CREATE INDEX idx_files_s3_key ON FileStorages(S3Key);
CREATE INDEX idx_files_hash ON FileStorages(FileHash) WHERE FileHash IS NOT NULL;

-- Comments
COMMENT ON TABLE FileStorages IS 'Metadata của files trên S3 - không lưu binary data trong DB';
COMMENT ON COLUMN FileStorages.FileHash IS 'SHA-256 hash để detect duplicate files';
COMMENT ON COLUMN FileStorages.S3Url IS 'Pre-signed URL tạm thời (expire sau 1 giờ)';
```

---

### 2.5 Invoices (Central Entity) ⭐ KEY TABLE

```sql
CREATE TABLE Invoices (
    -- Primary Key
    InvoiceId UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    
    -- Company Relation (Multi-tenant)
    CompanyId UUID NOT NULL REFERENCES Companies(CompanyId) ON DELETE CASCADE,
    DocumentTypeId INT NOT NULL REFERENCES DocumentTypes(DocumentTypeId),
    OriginalFileId UUID NOT NULL REFERENCES FileStorages(FileId),
    
    -- Processing Method
    ProcessingMethod VARCHAR(10) NOT NULL DEFAULT 'XML'
        CHECK (ProcessingMethod IN ('XML', 'OCR', 'MANUAL')),
    
    -- ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    -- CORE FIELDS - Theo Nghị định 123/2020/NĐ-CP Điều 12
    -- ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    
    -- Thông tin chung (TTChung)
    FormNumber VARCHAR(20),                    -- Mẫu số hóa đơn (KHMSHDon): "01GTKT", "02GTTT"
    SerialNumber VARCHAR(50),                  -- Ký hiệu hóa đơn (KHHDon): "C24T", "AA/25E"
    InvoiceNumber VARCHAR(50) NOT NULL,        -- Số hóa đơn (SHDon): "0001234"
    InvoiceDate DATE NOT NULL,                 -- Ngày lập (NLap)
    InvoiceCurrency VARCHAR(3) DEFAULT 'VND',  -- Đơn vị tiền tệ (DVTTe)
    ExchangeRate DECIMAL(18,6) DEFAULT 1,      -- Tỷ giá (TGia)
    
    -- Người bán (NBan)
    SellerName VARCHAR(200),                   -- Tên người bán
    SellerTaxCode VARCHAR(14),                 -- MST người bán (bắt buộc)
    SellerAddress TEXT,                        -- Địa chỉ
    SellerPhone VARCHAR(20),                   -- Số điện thoại
    SellerEmail VARCHAR(100),                  -- Email
    SellerBankAccount VARCHAR(50),             -- Số tài khoản ngân hàng
    SellerBankName VARCHAR(200),               -- Tên ngân hàng
    
    -- Người mua (NMua)
    BuyerName VARCHAR(200),                    -- Tên người mua
    BuyerTaxCode VARCHAR(14),                  -- MST người mua
    BuyerAddress TEXT,                         -- Địa chỉ
    BuyerPhone VARCHAR(20),                    -- Số điện thoại
    BuyerEmail VARCHAR(100),                   -- Email
    BuyerContactPerson VARCHAR(100),           -- Người mua hàng (HVTNMHang)
    
    -- Tổng tiền (TToan)
    TotalAmountBeforeTax DECIMAL(18,2),        -- Tổng tiền chưa thuế (TgTCThue)
    TotalTaxAmount DECIMAL(18,2),              -- Tổng tiền thuế GTGT (TgTThue)
    TotalAmount DECIMAL(18,2) NOT NULL,        -- Tổng thanh toán (TgTTTBSo)
    TotalAmountInWords TEXT,                   -- Tổng bằng chữ (TgTTTBChu)
    
    -- Thông tin bổ sung
    PaymentMethod VARCHAR(100),                -- Hình thức thanh toán (HTTToan)
    MCCQT VARCHAR(50),                         -- Mã của cơ quan thuế (bắt buộc với máy tính tiền)
    Notes TEXT,                                -- Ghi chú (GChu)
    
    -- ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    -- FLEXIBLE DATA (JSONB) - Lưu toàn bộ dữ liệu dynamic
    -- ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    
    RawData JSONB,
    /*
    Lưu toàn bộ XML gốc hoặc Textract response
    Example for XML:
    {
      "xml_version": "1.0",
      "invoice_template": "01GTKT",
      "signature": {...},
      "line_items": [...]
    }
    
    Example for OCR:
    {
      "textract_job_id": "xxx",
      "confidence_scores": {...},
      "bounding_boxes": {...}
    }
    */
    
    ExtractedData JSONB,
    /*
    Dữ liệu đã parse & chuẩn hóa
    {
      "line_items": [
        {
          "stt": 1,
          "product_name": "Laptop Dell",
          "unit": "Cái",
          "quantity": 10,
          "unit_price": 15000000,
          "total_amount": 150000000,
          "vat_rate": 10,
          "vat_amount": 15000000
        }
      ],
      "payment_terms": "Chuyển khoản trong 30 ngày",
      "delivery_address": "123 Nguyễn Huệ, Q1, TP.HCM"
    }
    */
    
    ValidationResult JSONB,
    /*
    Kết quả validate 3 lớp
    {
      "structure_valid": true,
      "signature_valid": false,
      "business_logic_valid": true,
      "errors": [
        {
          "layer": "Signature",
          "code": "ANTI_SPOOFING",
          "message": "MST người ký không khớp MST người bán",
          "severity": "Red"
        }
      ],
      "warnings": [...],
      "validation_timestamp": "2025-01-15T10:30:00Z"
    }
    */
    
    -- ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    -- WORKFLOW & RISK MANAGEMENT
    -- ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    
    Status VARCHAR(20) NOT NULL DEFAULT 'Draft'
        CHECK (Status IN ('Draft', 'Pending', 'Approved', 'Rejected', 'Archived')),
    
    RiskLevel VARCHAR(20) DEFAULT 'Green'
        CHECK (RiskLevel IN ('Green', 'Yellow', 'Orange', 'Red')),
    
    RiskReasons JSONB DEFAULT '[]'::jsonb,
    /*
    [
      {
        "code": "MISSING_XML",
        "severity": "Yellow",
        "message": "Hóa đơn GTGT thiếu file XML pháp lý",
        "auto_detected": true
      },
      {
        "code": "INVALID_TAX_CODE",
        "severity": "Red",
        "message": "MST người bán không hoạt động (theo VietQR API)",
        "checked_at": "2025-01-15T10:30:00Z"
      }
    ]
    */
    
    -- ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    -- VERSION CONTROL
    -- ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    
    IsReplaced BOOLEAN DEFAULT false,
    ReplacedBy UUID REFERENCES Invoices(InvoiceId),
    Version INT DEFAULT 1,
    
    -- ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    -- USER TRACKING
    -- ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    
    UploadedBy UUID NOT NULL REFERENCES Users(UserId),
    SubmittedBy UUID REFERENCES Users(UserId),
    ApprovedBy UUID REFERENCES Users(UserId),
    RejectedBy UUID REFERENCES Users(UserId),
    
    SubmittedAt TIMESTAMP,
    ApprovedAt TIMESTAMP,
    RejectedAt TIMESTAMP,
    RejectionReason TEXT,
    
    -- ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    -- METADATA
    -- ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    
    -- ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    -- CONSTRAINTS
    -- ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    
    CONSTRAINT chk_invoices_amounts CHECK (TotalAmount >= 0),
    CONSTRAINT chk_invoices_tax_math CHECK (
        (TotalTaxAmount IS NULL) OR 
        (TotalAmountBeforeTax + TotalTaxAmount - TotalAmount BETWEEN -10 AND 10)  -- Tolerance 10 VND
    ),
    CONSTRAINT chk_invoices_date_not_future CHECK (InvoiceDate <= CURRENT_DATE),
    CONSTRAINT chk_invoices_approval_flow CHECK (
        (Status = 'Approved' AND ApprovedBy IS NOT NULL AND ApprovedAt IS NOT NULL) OR
        (Status = 'Rejected' AND RejectedBy IS NOT NULL AND RejectedAt IS NOT NULL) OR
        (Status NOT IN ('Approved', 'Rejected'))
    ),
    CONSTRAINT chk_invoices_seller_tax_code CHECK (
        SellerTaxCode IS NULL OR 
        SellerTaxCode ~ '^[0-9]{10}$|^[0-9]{10}-[0-9]{3}$|^[0-9]{12}$'
    )
);

-- ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
-- INDEXES (Performance Critical)
-- ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

-- Primary queries
CREATE INDEX idx_invoices_company_date ON Invoices(CompanyId, InvoiceDate DESC);
CREATE INDEX idx_invoices_company_status ON Invoices(CompanyId, Status, CreatedAt DESC);
CREATE INDEX idx_invoices_company_risk ON Invoices(CompanyId, RiskLevel) 
    WHERE RiskLevel IN ('Yellow', 'Orange', 'Red');

-- Search indexes
CREATE INDEX idx_invoices_invoice_number ON Invoices(InvoiceNumber) 
    WHERE InvoiceNumber IS NOT NULL;
CREATE INDEX idx_invoices_seller_tax ON Invoices(SellerTaxCode) 
    WHERE SellerTaxCode IS NOT NULL;
CREATE INDEX idx_invoices_buyer_tax ON Invoices(BuyerTaxCode) 
    WHERE BuyerTaxCode IS NOT NULL;

-- Full-text search (PostgreSQL native)
CREATE INDEX idx_invoices_fulltext ON Invoices USING gin(
    to_tsvector('simple', 
        COALESCE(InvoiceNumber, '') || ' ' ||
        COALESCE(SerialNumber, '') || ' ' ||
        COALESCE(SellerName, '') || ' ' ||
        COALESCE(SellerTaxCode, '') || ' ' ||
        COALESCE(BuyerName, '') || ' ' ||
        COALESCE(BuyerTaxCode, '')
    )
);

-- JSONB indexes (GIN for flexible queries)
CREATE INDEX idx_invoices_raw_data_gin ON Invoices USING gin(RawData);
CREATE INDEX idx_invoices_extracted_data_gin ON Invoices USING gin(ExtractedData);
CREATE INDEX idx_invoices_validation_result_gin ON Invoices USING gin(ValidationResult);

-- Workflow indexes
CREATE INDEX idx_invoices_uploaded_by ON Invoices(UploadedBy, CreatedAt DESC);
CREATE INDEX idx_invoices_pending_approval ON Invoices(Status, SubmittedAt) 
    WHERE Status = 'Pending';

-- Version control
CREATE INDEX idx_invoices_replaced ON Invoices(IsReplaced, ReplacedBy) 
    WHERE IsReplaced = true;

-- ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
-- COMMENTS
-- ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

COMMENT ON TABLE Invoices IS 'Hóa đơn điện tử - Entity trung tâm tuân thủ 100% NĐ 123/2020/NĐ-CP';
COMMENT ON COLUMN Invoices.FormNumber IS 'Ký hiệu mẫu số (KHMSHDon) theo Quyết định 1550/QĐ-TCT';
COMMENT ON COLUMN Invoices.RawData IS 'Toàn bộ XML gốc hoặc Textract response (JSONB)';
COMMENT ON COLUMN Invoices.ExtractedData IS 'Dữ liệu đã parse, bao gồm line items, payment terms, etc.';
COMMENT ON COLUMN Invoices.ValidationResult IS 'Kết quả validate 3 lớp: Structure/Signature/BusinessLogic';
COMMENT ON COLUMN Invoices.RiskLevel IS 'Green: OK | Yellow: Warning | Orange: Medium risk | Red: Critical';
```

---

### 2.6 ValidationLayers (3-Layer Check Results)

```sql
CREATE TABLE ValidationLayers (
    -- Primary Key
    LayerId UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    
    -- Invoice Relation
    InvoiceId UUID NOT NULL REFERENCES Invoices(InvoiceId) ON DELETE CASCADE,
    
    -- Layer Info
    LayerName VARCHAR(50) NOT NULL 
        CHECK (LayerName IN ('Structure', 'Signature', 'BusinessLogic')),
    LayerOrder INT NOT NULL CHECK (LayerOrder BETWEEN 1 AND 3),
    
    -- Result
    IsValid BOOLEAN NOT NULL,
    ValidationStatus VARCHAR(20) NOT NULL
        CHECK (ValidationStatus IN ('Pass', 'Warning', 'Fail', 'Skipped')),
    
    -- Error Details
    ErrorCode VARCHAR(50),
    ErrorMessage TEXT,
    ErrorDetails JSONB,
    /*
    {
      "line_number": 45,
      "element": "KHMSHDon",
      "expected": "01GTKT or 02GTTT",
      "actual": null,
      "suggestion": "Thêm trường KHMSHDon vào TTChung"
    }
    */
    
    -- Specific Layer Data
    StructureValidationData JSONB,  -- XSD errors, schema violations
    SignatureValidationData JSONB,  -- Certificate info, signer details
    BusinessLogicValidationData JSONB,  -- Math errors, MST check results
    
    -- Performance
    ValidationDurationMs INT,  -- Time taken to validate this layer
    
    -- Metadata
    CheckedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CheckedBy VARCHAR(100) DEFAULT 'System',  -- 'System' or 'Manual'
    
    -- Constraints
    CONSTRAINT unique_invoice_layer ON (InvoiceId, LayerName, CheckedAt)
);

-- Indexes
CREATE INDEX idx_validation_layers_invoice ON ValidationLayers(InvoiceId, LayerOrder);
CREATE INDEX idx_validation_layers_status ON ValidationLayers(ValidationStatus, CheckedAt DESC);
CREATE INDEX idx_validation_layers_errors ON ValidationLayers(IsValid, ErrorCode) 
    WHERE IsValid = false;

-- Comments
COMMENT ON TABLE ValidationLayers IS 'Kết quả validate từng lớp theo quy trình 3-layer check';
COMMENT ON COLUMN ValidationLayers.LayerOrder IS '1: Structure | 2: Signature | 3: BusinessLogic';
```

---

## 3. SUPPORT TABLES

### 3.1 InvoiceAuditLogs (Immutable Audit Trail)

```sql
CREATE TABLE InvoiceAuditLogs (
    -- Primary Key
    AuditId UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    
    -- Invoice Relation
    InvoiceId UUID NOT NULL REFERENCES Invoices(InvoiceId) ON DELETE CASCADE,
    
    -- User Info
    UserId UUID NOT NULL REFERENCES Users(UserId),
    UserEmail VARCHAR(100),  -- Denormalized for history
    UserRole VARCHAR(50),    -- Role at the time of action
    
    -- Action Info
    Action VARCHAR(50) NOT NULL
        CHECK (Action IN ('UPLOAD', 'EDIT', 'SUBMIT', 'APPROVE', 'REJECT', 'OVERRIDE', 'DELETE', 'RESTORE')),
    
    -- Data Changes (JSONB for flexibility)
    OldData JSONB,
    /*
    {
      "TotalAmount": 1500000,
      "Status": "Draft",
      "RiskLevel": "Yellow"
    }
    */
    
    NewData JSONB,
    /*
    {
      "TotalAmount": 1550000,  -- User corrected
      "Status": "Draft",
      "RiskLevel": "Green"     -- Recalculated
    }
    */
    
    Changes JSONB,  -- Diff between OldData and NewData
    /*
    [
      {
        "field": "TotalAmount",
        "old_value": 1500000,
        "new_value": 1550000,
        "change_type": "UPDATE"
      }
    ]
    */
    
    -- Reason & Context
    Reason TEXT,  -- User-provided reason for change
    Comment TEXT,  -- Additional comments
    
    -- Request Info
    IpAddress VARCHAR(50),
    UserAgent TEXT,
    RequestId VARCHAR(100),  -- For tracing
    
    -- Metadata (IMMUTABLE - cannot be updated)
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
    
    -- Constraints
    CONSTRAINT chk_audit_logs_changes CHECK (
        (Action = 'UPLOAD' AND OldData IS NULL) OR
        (Action != 'UPLOAD' AND OldData IS NOT NULL)
    )
);

-- Indexes
CREATE INDEX idx_audit_logs_invoice_created ON InvoiceAuditLogs(InvoiceId, CreatedAt DESC);
CREATE INDEX idx_audit_logs_user ON InvoiceAuditLogs(UserId, CreatedAt DESC);
CREATE INDEX idx_audit_logs_action ON InvoiceAuditLogs(Action, CreatedAt DESC);
CREATE INDEX idx_audit_logs_created ON InvoiceAuditLogs(CreatedAt DESC);

-- Prevent updates and deletes (Immutable table)
CREATE RULE audit_logs_no_update AS ON UPDATE TO InvoiceAuditLogs DO INSTEAD NOTHING;
CREATE RULE audit_logs_no_delete AS ON DELETE TO InvoiceAuditLogs DO INSTEAD NOTHING;

-- Comments
COMMENT ON TABLE InvoiceAuditLogs IS 'Immutable audit trail - WHO did WHAT, WHEN, WHY';
COMMENT ON COLUMN InvoiceAuditLogs.Changes IS 'Calculated diff between OldData and NewData for easy review';
```

---

### 3.2 RiskCheckResults

```sql
CREATE TABLE RiskCheckResults (
    -- Primary Key
    CheckId UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    
    -- Invoice Relation
    InvoiceId UUID NOT NULL REFERENCES Invoices(InvoiceId) ON DELETE CASCADE,
    
    -- Check Info
    CheckType VARCHAR(50) NOT NULL
        CHECK (CheckType IN ('LEGAL', 'VALID', 'REASONABLE', 'CUSTOM')),
    CheckSubType VARCHAR(100),  -- e.g., "MST_VERIFICATION", "MATH_CHECK", "DATE_CHECK"
    
    -- Result
    CheckStatus VARCHAR(20) NOT NULL
        CHECK (CheckStatus IN ('PASS', 'WARNING', 'FAIL')),
    RiskLevel VARCHAR(20) NOT NULL
        CHECK (RiskLevel IN ('Green', 'Yellow', 'Orange', 'Red')),
    
    -- Error Info
    ErrorCode VARCHAR(50),
    ErrorMessage TEXT,
    Suggestion TEXT,  -- How to fix this issue
    
    -- Check Details (JSONB)
    CheckDetails JSONB,
    /*
    For MST check:
    {
      "tax_code": "0123456789",
      "api_endpoint": "https://api.vietqr.io/v2/business/0123456789",
      "api_response": {
        "code": "00",
        "data": {
          "name": "CÔNG TY ABC",
          "status": "Active"
        }
      },
      "is_valid": true,
      "checked_at": "2025-01-15T10:30:00Z"
    }
    
    For math check:
    {
      "line_item_index": 2,
      "quantity": 10,
      "unit_price": 150000,
      "expected_total": 1500000,
      "actual_total": 1500010,
      "difference": 10,
      "tolerance": 10,
      "is_within_tolerance": true
    }
    */
    
    -- Performance
    CheckDurationMs INT,
    CheckedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CheckedBy VARCHAR(100) DEFAULT 'System'
);

-- Indexes
CREATE INDEX idx_risk_checks_invoice ON RiskCheckResults(InvoiceId, CheckedAt DESC);
CREATE INDEX idx_risk_checks_status ON RiskCheckResults(CheckStatus, RiskLevel);
CREATE INDEX idx_risk_checks_type ON RiskCheckResults(CheckType, CheckSubType);

-- Comments
COMMENT ON TABLE RiskCheckResults IS 'Chi tiết từng lần check rủi ro (LEGAL/VALID/REASONABLE)';
COMMENT ON COLUMN RiskCheckResults.CheckType IS 'LEGAL: MST verification | VALID: Signature, format | REASONABLE: Math, logic';
```

---

### 3.3 Notifications

```sql
CREATE TABLE Notifications (
    -- Primary Key
    NotificationId UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    
    -- User Relation
    UserId UUID NOT NULL REFERENCES Users(UserId) ON DELETE CASCADE,
    
    -- Notification Info
    Type VARCHAR(50) NOT NULL
        CHECK (Type IN ('INVOICE_SUBMITTED', 'INVOICE_APPROVED', 'INVOICE_REJECTED', 
                       'RISK_DETECTED', 'SYSTEM_ALERT', 'USER_MENTIONED', 'REPORT_READY')),
    Priority VARCHAR(20) DEFAULT 'Normal'
        CHECK (Priority IN ('Low', 'Normal', 'High', 'Urgent')),
    
    -- Content
    Title VARCHAR(200) NOT NULL,
    Message TEXT NOT NULL,
    ActionUrl VARCHAR(500),  -- Deep link to relevant page
    ActionText VARCHAR(50),  -- e.g., "View Invoice", "Approve Now"
    
    -- Related Entities
    RelatedInvoiceId UUID REFERENCES Invoices(InvoiceId) ON DELETE SET NULL,
    RelatedUserId UUID REFERENCES Users(UserId) ON DELETE SET NULL,
    
    -- Status
    IsRead BOOLEAN DEFAULT false,
    IsArchived BOOLEAN DEFAULT false,
    ReadAt TIMESTAMP,
    ArchivedAt TIMESTAMP,
    
    -- Metadata
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    ExpiresAt TIMESTAMP,  -- Auto-delete after this date
    
    -- Constraints
    CONSTRAINT chk_notifications_read_time CHECK (ReadAt IS NULL OR ReadAt >= CreatedAt)
);

-- Indexes
CREATE INDEX idx_notifications_user_unread ON Notifications(UserId, IsRead, CreatedAt DESC) 
    WHERE IsRead = false AND IsArchived = false;
CREATE INDEX idx_notifications_user_all ON Notifications(UserId, CreatedAt DESC);
CREATE INDEX idx_notifications_expires ON Notifications(ExpiresAt) 
    WHERE ExpiresAt IS NOT NULL AND ExpiresAt < CURRENT_TIMESTAMP;

-- Comments
COMMENT ON TABLE Notifications IS 'In-app notifications cho users';
COMMENT ON COLUMN Notifications.ExpiresAt IS 'Auto-delete notifications sau 90 ngày (scheduled job)';
```

---

### 3.4 ExportHistories

```sql
CREATE TABLE ExportHistories (
    -- Primary Key
    ExportId UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    
    -- Company & User
    CompanyId UUID NOT NULL REFERENCES Companies(CompanyId) ON DELETE CASCADE,
    ExportedBy UUID NOT NULL REFERENCES Users(UserId),
    
    -- Export Info
    ExportFormat VARCHAR(20) NOT NULL
        CHECK (ExportFormat IN ('EXCEL', 'CSV', 'PDF', 'XML')),
    FileType VARCHAR(50) NOT NULL
        CHECK (FileType IN ('MISA', 'FAST', 'STANDARD', 'CUSTOM')),
    
    -- Filter Criteria (what was exported)
    FilterCriteria JSONB,
    /*
    {
      "date_range": {
        "from": "2025-01-01",
        "to": "2025-01-31"
      },
      "status": ["Approved"],
      "risk_level": ["Green"],
      "document_types": ["GTGT", "SALE"],
      "total_amount_min": 1000000,
      "total_amount_max": null
    }
    */
    
    -- Result
    TotalRecords INT NOT NULL,
    FileSize BIGINT,
    S3Key VARCHAR(500),
    S3Url TEXT,
    S3UrlExpiresAt TIMESTAMP,
    
    -- Download Tracking
    DownloadCount INT DEFAULT 0,
    LastDownloadAt TIMESTAMP,
    
    -- Metadata
    ExportedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    ExpiresAt TIMESTAMP,  -- Link expires after 7 days
    
    -- Constraints
    CONSTRAINT chk_exports_total_records CHECK (TotalRecords >= 0)
);

-- Indexes
CREATE INDEX idx_exports_company_date ON ExportHistories(CompanyId, ExportedAt DESC);
CREATE INDEX idx_exports_user ON ExportHistories(ExportedBy, ExportedAt DESC);
CREATE INDEX idx_exports_expires ON ExportHistories(ExpiresAt) 
    WHERE ExpiresAt IS NOT NULL;

-- Comments
COMMENT ON TABLE ExportHistories IS 'Lịch sử export file - tracking & download management';
COMMENT ON COLUMN ExportHistories.FileType IS 'MISA/FAST: Format tương thích phần mềm kế toán VN';
```

---

### 3.5 AIProcessingLogs

```sql
CREATE TABLE AIProcessingLogs (
    -- Primary Key
    LogId UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    
    -- File & Invoice Relation
    FileId UUID NOT NULL REFERENCES FileStorages(FileId) ON DELETE CASCADE,
    InvoiceId UUID REFERENCES Invoices(InvoiceId) ON DELETE SET NULL,
    
    -- AI Service Info
    AIService VARCHAR(50) NOT NULL
        CHECK (AIService IN ('TEXTRACT', 'REKOGNITION', 'COMPREHEND', 'CUSTOM_MODEL')),
    AIModel VARCHAR(100),  -- e.g., "AnalyzeExpense", "DetectText"
    AIRegion VARCHAR(50) DEFAULT 'ap-southeast-1',
    
    -- Request Info
    RequestPayload JSONB,  -- What we sent to AI
    ResponsePayload JSONB,  -- What AI returned (full response)
    
    -- Processing Result
    Status VARCHAR(20) NOT NULL
        CHECK (Status IN ('SUCCESS', 'FAILED', 'PARTIAL', 'TIMEOUT')),
    ErrorMessage TEXT,
    ErrorCode VARCHAR(50),
    
    -- Quality Metrics
    ConfidenceScore DECIMAL(5,2) CHECK (ConfidenceScore BETWEEN 0 AND 100),
    ProcessingTimeMs INT,
    TokensUsed INT,  -- For billing tracking
    
    -- Extracted Data
    ProcessedData JSONB,
    /*
    After mapping Textract response to our format:
    {
      "invoice_number": {"value": "0001234", "confidence": 0.95},
      "invoice_date": {"value": "2025-01-15", "confidence": 0.92},
      "total_amount": {"value": 1500000, "confidence": 0.98}
    }
    */
    
    -- Cost Tracking
    EstimatedCostUSD DECIMAL(10,4),
    
    -- Metadata
    ProcessedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Indexes
CREATE INDEX idx_ai_logs_file ON AIProcessingLogs(FileId, ProcessedAt DESC);
CREATE INDEX idx_ai_logs_invoice ON AIProcessingLogs(InvoiceId, ProcessedAt DESC);
CREATE INDEX idx_ai_logs_status ON AIProcessingLogs(Status, AIService, ProcessedAt DESC);
CREATE INDEX idx_ai_logs_cost ON AIProcessingLogs(ProcessedAt DESC, EstimatedCostUSD DESC);

-- Comments
COMMENT ON TABLE AIProcessingLogs IS 'Log xử lý AI - tracking accuracy, cost, performance';
COMMENT ON COLUMN AIProcessingLogs.ConfidenceScore IS 'Overall confidence từ AI (0-100%)';
COMMENT ON COLUMN AIProcessingLogs.EstimatedCostUSD IS 'For AWS cost optimization & billing';
```

---

### 3.6 SystemConfigurations

```sql
CREATE TABLE SystemConfigurations (
    -- Primary Key
    ConfigId SERIAL PRIMARY KEY,
    
    -- Config Info
    ConfigKey VARCHAR(100) NOT NULL UNIQUE,
    ConfigValue TEXT NOT NULL,
    ConfigType VARCHAR(20) DEFAULT 'String'
        CHECK (ConfigType IN ('String', 'Integer', 'Boolean', 'JSON', 'Secret')),
    
    -- Metadata
    Category VARCHAR(50),  -- e.g., "AWS", "Validation", "Features", "Limits"
    Description TEXT,
    DefaultValue TEXT,
    IsEncrypted BOOLEAN DEFAULT false,
    
    -- Permissions
    IsReadOnly BOOLEAN DEFAULT false,
    RequiresRestart BOOLEAN DEFAULT false,
    
    -- Change Tracking
    UpdatedBy UUID REFERENCES Users(UserId),
    UpdatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Indexes
CREATE INDEX idx_configs_key ON SystemConfigurations(ConfigKey);
CREATE INDEX idx_configs_category ON SystemConfigurations(Category);

-- Seed Data
INSERT INTO SystemConfigurations (ConfigKey, ConfigValue, ConfigType, Category, Description) VALUES
-- AWS
('AWS_REGION', 'ap-southeast-1', 'String', 'AWS', 'Default AWS region'),
('S3_BUCKET_NAME', 'smartinvoice-shield-prod', 'String', 'AWS', 'S3 bucket for file storage'),
('TEXTRACT_CONFIDENCE_THRESHOLD', '0.8', 'String', 'AWS', 'Minimum confidence for Textract results'),

-- Validation
('VALIDATION_ENABLE_MST_API', 'true', 'Boolean', 'Validation', 'Enable MST verification via VietQR API'),
('VALIDATION_MATH_TOLERANCE', '10', 'Integer', 'Validation', 'Math tolerance in VND'),
('VALIDATION_FUTURE_DATE_DAYS', '0', 'Integer', 'Validation', 'Allow invoice date X days in future'),

-- File Limits
('MAX_UPLOAD_SIZE_MB', '10', 'Integer', 'Limits', 'Maximum file size in MB'),
('ALLOWED_FILE_EXTENSIONS', '.xml,.pdf,.jpg,.jpeg,.png', 'String', 'Limits', 'Allowed file types'),

-- Features
('FEATURE_OCR_ENABLED', 'true', 'Boolean', 'Features', 'Enable OCR processing'),
('FEATURE_EMAIL_NOTIFICATIONS', 'true', 'Boolean', 'Features', 'Enable email notifications'),
('FEATURE_EXPORT_EXCEL', 'true', 'Boolean', 'Features', 'Enable Excel export'),

-- Retention
('DATA_RETENTION_DAYS', '730', 'Integer', 'Retention', 'Keep data for 2 years'),
('AUDIT_LOG_RETENTION_DAYS', '2190', 'Integer', 'Retention', 'Keep audit logs for 6 years (theo luật)');

-- Comments
COMMENT ON TABLE SystemConfigurations IS 'System-wide configurations - editable via Admin panel';
COMMENT ON COLUMN SystemConfigurations.IsEncrypted IS 'True if value contains sensitive data (passwords, API keys)';
```

---

## 4. INDEXES STRATEGY

### 4.1 Index Types

```sql
-- B-tree indexes (default) - for equality and range queries
CREATE INDEX idx_invoices_date ON Invoices(InvoiceDate);

-- GIN indexes - for JSONB and full-text search
CREATE INDEX idx_invoices_raw_data_gin ON Invoices USING gin(RawData);

-- Partial indexes - for specific conditions
CREATE INDEX idx_invoices_pending ON Invoices(CompanyId, SubmittedAt) 
    WHERE Status = 'Pending';

-- Composite indexes - for multiple columns
CREATE INDEX idx_invoices_company_date_status ON Invoices(CompanyId, InvoiceDate DESC, Status);

-- Expression indexes - for computed values
CREATE INDEX idx_invoices_year_month ON Invoices(
    EXTRACT(YEAR FROM InvoiceDate),
    EXTRACT(MONTH FROM InvoiceDate)
);
```

---

## 5. CONSTRAINTS & RULES

### 5.1 Data Integrity Rules

```sql
-- Cascade deletes (Multi-tenant isolation)
ALTER TABLE Invoices ADD CONSTRAINT fk_invoices_company
    FOREIGN KEY (CompanyId) REFERENCES Companies(CompanyId) ON DELETE CASCADE;

-- Prevent orphaned records
ALTER TABLE InvoiceAuditLogs ADD CONSTRAINT fk_audit_invoice
    FOREIGN KEY (InvoiceId) REFERENCES Invoices(InvoiceId) ON DELETE CASCADE;

-- Check constraints for business rules
ALTER TABLE Invoices ADD CONSTRAINT chk_invoice_approval_logic
    CHECK (
        (Status = 'Approved' AND ApprovedBy IS NOT NULL) OR
        (Status != 'Approved')
    );
```

---

## 6. MIGRATION SCRIPTS

File `database_migration.sql` đầy đủ sẽ được tạo riêng với:
- CREATE EXTENSION statements
- All CREATE TABLE statements
- All INDEX statements
- All CONSTRAINT statements
- Seed data
- Helper functions
- Triggers for UpdatedAt auto-update

---

**[END OF DATABASE SCHEMA DOCUMENT]**

**Next Steps**:
1. Run migration script
2. Insert seed data
3. Create database user with proper permissions
4. Setup connection pooling (PgBouncer)
5. Configure backups (AWS RDS automated backups)

**Document Status**: Complete ✅  
**Last Updated**: 06/02/2026
