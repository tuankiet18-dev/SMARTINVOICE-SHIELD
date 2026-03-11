# SYSTEM ARCHITECTURE DESIGN

## SmartInvoice Shield - Production-Ready Architecture

**Version**: 1.0 Production  
**Date**: 06/02/2026  
**Compliance**: Nghị định 123/2020/NĐ-CP + AWS Well-Architected Framework  
**Team**: 5 members, 3 months timeline

---

## 📋 MỤC LỤC

1. [Architecture Overview](#1-architecture-overview)
2. [System Context Diagram](#2-system-context-diagram)
3. [Container Architecture](#3-container-architecture)
4. [Component Architecture](#4-component-architecture)
5. [Data Flow Architecture](#5-data-flow-architecture)
6. [Deployment Architecture](#6-deployment-architecture)
7. [Security Architecture](#7-security-architecture)
8. [Scalability & Performance](#8-scalability--performance)

---

## 1. ARCHITECTURE OVERVIEW

### 1.1 Architecture Principles (AWS Well-Architected)

Hệ thống được thiết kế dựa trên **5 trụ cột** của AWS Well-Architected Framework:

| #   | Trụ cột                                   | Chi tiết                                                                                                            |
| --- | ----------------------------------------- | ------------------------------------------------------------------------------------------------------------------- |
| 1   | **Operational Excellence** (Vận hành)     | Infrastructure as Code, Automated deployment (Elastic Beanstalk), Monitoring & logging (CloudWatch)                 |
| 2   | **Security** (Bảo mật)                    | IAM, Data encryption (at rest & in transit), VPC & Security Groups, Secrets management                              |
| 3   | **Reliability** (Độ tin cậy)              | Multi-AZ database (RDS auto-failover), Auto-scaling, Backup & disaster recovery, Health checks & auto-recovery      |
| 4   | **Performance Efficiency** (Hiệu suất)    | Right-sized resources (t3), Serverless where appropriate (S3, Cognito), Caching, Database optimization (indexes)    |
| 5   | **HA & Cost Optimization** (Sẵn sàng cao) | Multi-AZ (2 AZs), S3 lifecycle policies (auto-archive to Glacier), Auto Scaling 2-4 nodes, Cost monitoring & alerts |

---

### 1.2 Architecture Style

**Hybrid Architecture**: Layered Monolith + Microservices (AI Processing)

| Thành phần                     | Lý do chọn                                                                                                                                                                     |
| ------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **Layered Monolith** (Backend) | Team 2 backend devs, 3 months timeline → microservices phức tạp hơn. Đơn giản hóa deployment (1 API service). Dễ maintain ACID transactions. Elastic Beanstalk native support. |
| **Custom OCR API** (AI)        | PaddleOCR + VietOCR optimized cho tiếng Việt. Cost: flat cost thay vì pay-per-use. Full control over extraction logic. Dedicated AI member.                                    |

---

### 1.3 High-Level Architecture Decision Records (ADR)

#### ADR-001: Database - PostgreSQL on RDS

```
Context:
  - Cần lưu trữ structured data (invoices, users, audit logs)
  - Cần ACID transactions (approval workflow)
  - Cần complex queries (dashboard, search, reporting)

Decision: PostgreSQL 14 on AWS RDS

Rationale:
  ✓ JSONB support → Flexible schema (RawData, ExtractedData)
  ✓ Full-text search → Native FTS (không cần Elasticsearch)
  ✓ Mature ecosystem → ORM support (Entity Framework Core)
  ✓ AWS Free Tier → 750h/month db.t3.micro
  ✓ Automated backups → Point-in-time recovery

Alternatives Rejected:
  ✗ DynamoDB: Không phù hợp với complex joins, transactions
  ✗ MongoDB: Thiếu transaction support mạnh như PostgreSQL
  ✗ MySQL: JSONB support kém hơn PostgreSQL
```

#### ADR-002: File Storage - Amazon S3

```
Context:
  - Cần lưu trữ files: XML (1-10KB), PDF (100KB-5MB), Images (500KB-5MB)
  - Expect: ~1000 files/company/month
  - Cần: Versioning, lifecycle management, public access control

Decision: Amazon S3 Standard → Glacier Deep Archive

Rationale:
  ✓ Unlimited storage → No quota planning needed
  ✓ 99.999999999% durability → Data safety
  ✓ Lifecycle policies → Auto-archive after 90 days
  ✓ S3 Versioning → File version control
  ✓ Pre-signed URLs → Secure temporary access
  ✓ AWS Free Tier → 5GB storage

Storage Strategy:
  - Recent files (0-30 days): S3 Standard (fast access)
  - Archive files (30-90 days): S3 Intelligent-Tiering
  - Old files (>90 days): Glacier Deep Archive ($1/TB/month)
```

#### ADR-003: AI/OCR - AWS ECS Fargate (PaddleOCR + VietOCR)

```
Context:
  - Cần extract text từ PDF/Images (hóa đơn scan)
  - Accuracy requirement: ≥85% cho tiếng Việt
  - Team có 1 thành viên chuyên trách AI (xây dựng model)
  - Cần Serverless Container để tối ưu tài nguyên (chỉ tính tiền khi chạy OCR)

Decision: Deploy OCR API as a Container on AWS ECS Fargate

Rationale:
  ✓ Vietnamese Support → VietOCR vượt trội hơn các giải pháp quốc tế
  ✓ Team setup → Có nguồn lực AI chuyên biệt để tự host model
  ✓ Cost Control → Tránh chi phí pay-per-use per-page, chạy Serverless
    nên không tốn phí EC2 chạy không tải (idle).

Alternatives Rejected:
  ✗ AWS Textract: Hỗ trợ tiếng Việt kém, trả phí per-page
  ✗ Google Vision API: Không support Vietnam region
  ✗ Azure Form Recognizer: Trả phí cao per-page
```

#### ADR-004: Backend Framework - .NET 6 Web API

```
Context:
  - Có sẵn code C# (InvoiceProcessor.cs) cần reuse
  - Team quen C# (từ code có sẵn)
  - Cần performance cao, async I/O

Decision: ASP.NET Core 6 Web API

Rationale:
  ✓ Code reuse → Refactor InvoiceProcessor.cs
  ✓ Performance → Top 3 TechEmpower benchmarks
  ✓ Async/await → Non-blocking I/O
  ✓ Built-in DI → Clean architecture
  ✓ AWS SDK support → Native S3, Textract integration
  ✓ Entity Framework Core → Type-safe ORM

Cross-platform: Runs on Linux (AWS Elastic Beanstalk)
```

#### ADR-005: Frontend Framework - React 18 + TypeScript

```
Context:
  - Cần SPA (Single Page Application)
  - Team có 2 frontend devs
  - Cần UI component library

Decision: React 18 + TypeScript + Material-UI

Rationale:
  ✓ React 18 → Concurrent rendering, auto batching
  ✓ TypeScript → Type safety, better IDE support
  ✓ Material-UI → Pre-built components, production-ready
  ✓ Vite → Fast build tool
  ✓ AWS Amplify → Easy deployment

Component Library: Material-UI v5 (MUI)
  - Comprehensive components (300+)
  - Customizable theme
  - Accessibility built-in
  - Vietnamese documentation available
```

#### ADR-006: Authentication - Amazon Cognito (JWT & OTP)

```
Context:
  - Cần hệ thống quản lý danh tính (Identity Management) secure & scalable
  - Cần luồng xác thực an toàn: Đăng ký -> Gửi OTP -> Xác thực -> Đăng nhập
  - Cần phát hành/quản lý JWT (JSON Web Tokens)

Decision: Amazon Cognito User Pools

Rationale:
  ✓ Managed Service → Không cần tự code luồng cấp phát/lưu trữ mật khẩu trong DB
  ✓ Native OTP Support → Tự động gửi mã xác thực (OTP) qua Email/SMS khi đăng ký
  ✓ JWT Standard → Trả về AccessToken, IdToken, RefreshToken chuẩn RFC 7519
  ✓ Scalable & Secure → Brute-force protection, bảo mật chuẩn AWS

Luồng hoạt động (Auth Flow):
  1. Đăng ký (Register): Gọi API SignUpAsync -> Pool (Unconfirmed) -> Email OTP
  2. Xác thực OTP (Verify): Gọi API ConfirmSignUpAsync kèm mã OTP -> Kích hoạt
  3. Đăng nhập (Login): Gọi API InitiateAuthAsync (USER_PASSWORD_AUTH) -> JWT Tokens

JWT Payload:
  {
    "sub": "user-id",
    "email": "user@example.com",
    "role": "Member",
    "company_id": "company-id",
    "exp": 1234567890
  }
```

---

## 2. SYSTEM CONTEXT DIAGRAM

### 2.1 External Systems & Users

```
                    ┌─────────────────────────────────────────────┐
                    │         SMARTINVOICE SHIELD SYSTEM           │
                    │    (Invoice Management & Risk Assessment)    │
                    └──────────────────┬──────────────────────────┘
                                       │
         ┌─────────────────────────────┼─────────────────────────────┐
         │                             │                             │
         ▼                             ▼                             ▼
┌──────────────────┐       ┌──────────────────┐       ┌──────────────────┐
│  MEMBER          │       │  COMPANY_ADMIN   │       │  SUPER_ADMIN     │
│  (Kế toán viên)  │       │  (Kế toán trưởng)│       │  (System Admin)  │
├──────────────────┤       ├──────────────────┤       ├──────────────────┤
│ • Upload invoice │       │ • Approve/Reject │       │ • Manage users   │
│ • Edit data      │       │ • View dashboard │       │ • Manage config  │
│ • Submit         │       │ • Export reports  │       │ • System monitor │
│ • Search         │       │ • Audit review   │       │ • NO invoice data│
└──────────────────┘       └──────────────────┘       └──────────────────┘
         │                             │                             │
         └─────────────────────────────┼─────────────────────────────┘
                                       │
                                       │ HTTPS (REST API)
                                       │
         ┌─────────────────────────────┼─────────────────────────────┐
         │                             │                             │
         ▼                             ▼                             ▼
┌──────────────────┐       ┌──────────────────┐       ┌──────────────────┐
│  EXTERNAL APIs   │       │  AWS SERVICES    │       │  FILE SOURCES    │
├──────────────────┤       ├──────────────────┤       ├──────────────────┤
│ • VietQR API     │       │ • S3             │       │ • Email (XML)    │
│   (MST verify)   │       │ • RDS PostgreSQL │       │ • Scanner (PDF)  │
│ • Internal OCR   │       │ • CloudWatch     │       │ • Mobile camera  │
│   (AI Team API)  │       │ • Secrets Mgr    │       │ • Manual upload  │
└──────────────────┘       └──────────────────┘       └──────────────────┘
```

### 2.2 System Boundaries

```
┌─────────────────────────────────────────────────────────────┐
│  INSIDE SYSTEM BOUNDARY                                     │
├─────────────────────────────────────────────────────────────┤
│  ✓ React Frontend (Web UI)                                  │
│  ✓ .NET Core API (Business Logic)                           │
│  ✓ PostgreSQL Database (Data Storage)                       │
│  ✓ S3 File Storage                                          │
│  ✓ Internal OCR Client (via HTTP API)                       │
│  ✓ Authentication & Authorization                           │
│  ✓ Audit Trail System                                       │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│  OUTSIDE SYSTEM BOUNDARY (External Dependencies)            │
├─────────────────────────────────────────────────────────────┤
│  ✗ VietQR API (MST verification) - 3rd party               │
│  ✗ Email Service Provider (AWS SES/SendGrid) - optional     │
│  ✗ Accounting Software (MISA/FAST) - export only            │
│  ✗ Tax Authority System - future integration                │
└─────────────────────────────────────────────────────────────┘
```

---

## 3. CONTAINER ARCHITECTURE

### 3.1 Container Diagram (C4 Model Level 2)

```
┌────────────────────────────────────────────────────────────────────┐
│                         CLIENT LAYER                               │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │  WEB BROWSER (Desktop + Mobile)                              │  │
│  │  • Chrome, Firefox, Safari, Edge                             │  │
│  │  • Responsive design (1920px desktop, 375px mobile)          │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                               │                                    │
│                               │ HTTPS (443)                        │
│                               │                                    │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │  [Container] REACT SPA                                       │  │
│  │  Technology: React 18 + TypeScript                           │  │
│  │  Hosting: AWS Amplify                                        │  │
│  │                                                              │  │
│  │  Features:                                                   │  │
│  │  • Login/Register pages                                      │  │
│  │  • Invoice Management (List/Detail/Upload/Edit)              │  │
│  │  • Dashboard & Analytics                                     │  │
│  │  • Admin Panel (Approval Queue, User Management)             │  │
│  │  • Search & Filter                                           │  │
│  │  • Export functionality                                      │  │
│  │                                                              │  │
│  │  Dependencies:                                               │  │
│  │  • Material-UI v5 (UI components)                            │  │
│  │  • Recharts (Dashboard charts)                               │  │
│  │  • Axios (HTTP client)                                       │  │
│  │  • React Router v6 (SPA routing)                             │  │
│  │  • React Hook Form + Yup (Form validation)                   │  │
│  └──────────────────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────────────────┘
                               │
                               │ REST API (JSON over HTTPS)
                               │ Authorization: Bearer {JWT}
                               ▼
┌────────────────────────────────────────────────────────────────────┐
│                         API LAYER                                  │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │  [Container] .NET CORE WEB API                               │  │
│  │  Technology: ASP.NET Core 6.0 (C#)                           │  │
│  │  Hosting: AWS Elastic Beanstalk                              │  │
│  │  Runtime: Linux (Amazon Linux 2)                             │  │
│  │                                                              │  │
│  │  PRESENTATION LAYER (Controllers):                           │  │
│  │  ├─ AuthController (Login, Register, RefreshToken)           │  │
│  │  ├─ InvoiceController (CRUD, Validate, Submit)               │  │
│  │  ├─ DashboardController (Stats, Charts)                      │  │
│  │  ├─ ExportController (Excel, PDF)                            │  │
│  │  ├─ AdminController (Users, Config)                          │  │
│  │  └─ HealthCheckController (Monitoring)                       │  │
│  │                                                              │  │
│  │  BUSINESS LOGIC LAYER (Services):                            │  │
│  │  ├─ InvoiceProcessorService (3-layer validation)             │  │
│  │  ├─ OcrClientService (Internal AI integration)               │  │
│  │  ├─ ValidationService (Risk calculation)                     │  │
│  │  ├─ S3Service (File operations)                              │  │
│  │  ├─ SearchService (Full-text search)                         │  │
│  │  ├─ ExportService (Excel generation)                         │  │
│  │  ├─ NotificationService (Alerts)                             │  │
│  │  ├─ AuditLogService (Audit trail)                            │  │
│  │  └─ VietQRService (MST verification)                         │  │
│  │                                                              │  │
│  │  DATA ACCESS LAYER (Repositories):                           │  │
│  │  ├─ IRepository<T> (Generic repository)                      │  │
│  │  ├─ InvoiceRepository                                        │  │
│  │  ├─ UserRepository                                           │  │
│  │  ├─ CompanyRepository                                        │  │
│  │  ├─ FileStorageRepository                                    │  │
│  │  ├─ AuditLogRepository                                       │  │
│  │  └─ Unit of Work (Transaction management)                    │  │
│  │                                                              │  │
│  │  Middleware Stack:                                            │  │
│  │  • JwtBearerAuthentication (validate JWT)                    │  │
│  │  • ExceptionHandlerMiddleware (global error handling)        │  │
│  │  • RequestLoggingMiddleware (Serilog)                        │  │
│  │  • CorrelationIdMiddleware (request tracing)                 │  │
│  └──────────────────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────────────────┘
         │                      │                      │
         ▼                      ▼                      ▼
┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐
│ [Container]      │  │ [Container]      │  │ [Container]      │
│ POSTGRESQL DB    │  │ AMAZON S3        │  │ AWS ECS FARGATE  │
│                  │  │                  │  │                  │
│ Technology:      │  │ Technology:      │  │ Technology:      │
│ PostgreSQL 14    │  │ S3 Standard      │  │ PaddleOCR +      │
│                  │  │                  │  │ VietOCR (Python) │
│ Hosting:         │  │ Hosting:         │  │                  │
│ AWS RDS          │  │ AWS S3           │  │ Hosting:         │
│ Multi-AZ         │  │ Cross-AZ         │  │ AWS ECS Fargate  │
│                  │  │                  │  │                  │
│ Instance:        │  │ Buckets:         │  │ Endpoint:        │
│ db.t3.small      │  │ • dev-bucket     │  │ /api/v1/extract  │
│ (Production HA)  │  │ • prod-bucket    │  │                  │
│ Storage: 20 GB   │  │ • Versioning     │  │ Accuracy Goal:   │
│                  │  │ • Lifecycle      │  │ >85% for VN text │
│                  │  │ • Encryption     │  │                  │
└──────────────────┘  └──────────────────┘  └──────────────────┘
         │
         ▼
┌──────────────────┐
│ [External]       │
│ VIETQR API       │
│                  │
│ Endpoint:        │
│ api.vietqr.io    │
│ /v2/business/    │
│ {tax_code}       │
│                  │
│ Purpose:         │
│ MST verification │
│                  │
│ Rate Limit:      │
│ 100 req/day FREE │
└──────────────────┘
```

---

### 3.2 Container Responsibilities

| Container         | Responsibilities                                                                                                                                                                     | Not Responsible For                                     |
| ----------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------- |
| **React SPA**     | UI rendering, Client-side validation, State management (React Context), API communication (Axios), Client-side routing, Session management (JWT storage)                             | Business logic, Data persistence, Authentication logic  |
| **.NET Core API** | Request auth & authorization, Business logic (3-layer validation), Server-side validation, DB operations, External API integration (S3, OCR, VietQR), File processing, Audit logging | UI rendering, File storage, OCR processing              |
| **PostgreSQL DB** | Persistent storage, ACID transactions, Data integrity (constraints), Query optimization (indexes), Full-text search (native FTS), Backup & recovery                                  | Business logic, File storage, Complex computations      |
| **Amazon S3**     | File storage (XML, PDF, Images, Exports), File versioning, Lifecycle management, Pre-signed URL generation, 99.999999999% durability                                                 | File processing, Metadata storage, Access control logic |

---

## 4. COMPONENT ARCHITECTURE

### 4.1 Backend Component Diagram (Detailed)

#### HTTP Request Pipeline (Middleware)

```
Request → [CORS Policy] → [JWT Auth] → [Exception Handler] → [Serilog Logging] → Controller
```

#### Controllers (Presentation Layer)

| Controller              | Endpoints                                                                                                                                                                                                                                                                                                         |
| ----------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **AuthController**      | `POST /api/auth/register`, `POST /api/auth/login`, `POST /api/auth/refresh-token`, `POST /api/auth/logout`, `GET /api/auth/me`                                                                                                                                                                                    |
| **InvoiceController**   | `GET /api/invoices`, `GET /api/invoices/{id}`, `POST /api/invoices/upload`, `PUT /api/invoices/{id}`, `DELETE /api/invoices/{id}`, `POST /api/invoices/{id}/validate`, `POST /api/invoices/{id}/submit`, `POST /api/invoices/{id}/approve`, `POST /api/invoices/{id}/reject`, `GET /api/invoices/{id}/audit-logs` |
| **DashboardController** | `GET /api/dashboard/stats`, `GET /api/dashboard/charts/invoice-by-month`, `GET /api/dashboard/charts/risk-distribution`, `GET /api/dashboard/charts/amount-trend`                                                                                                                                                 |
| **ExportController**    | `POST /api/export/excel`, `GET /api/export/{id}/download`, `GET /api/export/history`                                                                                                                                                                                                                              |
| **AdminController**     | `GET /api/admin/users`, `POST /api/admin/users`, `PUT /api/admin/users/{id}`, `POST /api/admin/users/{id}/deactivate`, `GET /api/admin/system-config`, `PUT /api/admin/system-config`                                                                                                                             |

#### Services (Business Logic Layer)

| Service                     | Chức năng chính                                                                                                                                                                                                                  |
| --------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **InvoiceProcessorService** | Core business logic: `ValidateXmlAsync(s3Key)` — Layer 1: XSD Structure, Layer 2: Digital Signature + Anti-Spoofing, Layer 3: Business Logic (auto-detect type, mandatory fields, math, MST via VietQR). `CalculateRiskLevel()`. |
| **OcrClientService**        | AI Integration: `ExtractInvoiceDataAsync()` — Call Internal OCR HTTP API, parse JSON response, map to Invoice model, extract confidence scores.                                                                                  |
| **ValidationService**       | Risk Assessment: LEGAL checks (MST format, required fields), VALID checks (signature, date logic), REASONABLE checks (amounts, math). Risk levels: Green / Yellow / Orange / Red.                                                |
| **S3Service**               | File Operations: `UploadFileAsync()`, `DownloadFileAsync()`, `GeneratePresignedUrl()`, `DeleteFileAsync()`, `ListFilesAsync()`.                                                                                                  |
| **ExportService**           | Report Generation: `GenerateExcelAsync()` (format for MISA/FAST/Standard, EPPlus library), `GeneratePdfReportAsync()`.                                                                                                           |
| **SearchService**           | Full-Text Search: `SearchInvoicesAsync()` using PostgreSQL FTS (`to_tsvector`, `to_tsquery`), `BuildSearchFilter()`.                                                                                                             |
| **AuditLogService**         | Audit Trail: `LogActionAsync()` — calculate diff (OldData vs NewData), create immutable record, capture context (IP, UserAgent, Timestamp).                                                                                      |
| **NotificationService**     | Alerts: `CreateNotificationAsync()`, `SendEmailAsync()` [Optional], `GetUnreadCountAsync()`.                                                                                                                                     |
| **VietQRService**           | External API: `VerifyTaxCodeAsync()` — Call VietQR API, parse response, cache result (in-memory, 24h). `ValidateTaxCodeFormat()` — 10 digits: Mod-11, 13 digits: MST+NNN, 12 digits: CCCD.                                       |

#### Repositories (Data Access Layer)

| Repository                | Methods                                                                                           |
| ------------------------- | ------------------------------------------------------------------------------------------------- |
| **GenericRepository\<T>** | `GetByIdAsync()`, `GetAllAsync()`, `AddAsync()`, `UpdateAsync()`, `DeleteAsync()`, `CountAsync()` |
| **InvoiceRepository**     | `GetByCompanyAsync()`, `SearchAsync()`, `GetPendingApprovalsAsync()`, `GetByRiskLevelAsync()`     |
| **UnitOfWork**            | `BeginTransactionAsync()`, `CommitAsync()`, `RollbackAsync()`, `SaveChangesAsync()`               |

#### Data Models (Domain Entities)

| Entity              | Mô tả                          |
| ------------------- | ------------------------------ |
| Company             | Multi-tenant root              |
| User                | Authentication & Authorization |
| Invoice             | Central entity ⭐              |
| DocumentType        | Invoice classifications        |
| FileStorage         | S3 file metadata               |
| ValidationLayer     | 3-layer validation results     |
| InvoiceAuditLog     | Immutable audit trail          |
| RiskCheckResult     | Detailed risk analysis         |
| Notification        | In-app alerts                  |
| ExportHistory       | Export tracking                |
| AIProcessingLog     | Textract/OCR metrics           |
| SystemConfiguration | App settings                   |

#### Database Context (Entity Framework Core)

```
AppDbContext : DbContext
  ├─ DbSet<Company> Companies
  ├─ DbSet<User> Users
  ├─ DbSet<Invoice> Invoices
  └─ ... (12 DbSets total)

  OnModelCreating():
  ├─ Configure relationships
  ├─ Configure indexes
  ├─ Configure constraints
  └─ Seed data (DocumentTypes)
```

---

### 4.2 Frontend Component Architecture

#### App Root

```
App
├─ AuthProvider (Context: user, token, login, logout)
├─ ThemeProvider (Material-UI theme)
└─ Router (React Router v6)
```

#### Layout Components

```
MainLayout
├─ AppBar (Header)
│  ├─ Logo
│  ├─ Navigation links
│  ├─ NotificationBadge (unread count)
│  └─ UserMenu (Logout, Profile)
│
├─ Sidebar (Drawer)
│  ├─ Dashboard link
│  ├─ Invoices link
│  ├─ Upload link
│  ├─ Reports link
│  └─ Admin link (if role = CompanyAdmin)
│
└─ Content Area (Outlet for routes)
```

#### Page Components (Routes)

| Route                   | Component         | Nội dung chính                                                                                                                                    |
| ----------------------- | ----------------- | ------------------------------------------------------------------------------------------------------------------------------------------------- |
| `/login`                | LoginPage         | LoginForm (EmailField, PasswordField, LoginButton), useAuth() hook                                                                                |
| `/dashboard`            | DashboardPage     | StatCards (4 cards: Total, Pending, HighRisk, Amount), Charts (RiskDistributionPie, InvoiceByMonthBar, AmountTrendLine)                           |
| `/invoices`             | InvoiceListPage   | FilterSidebar (DateRange, Status, RiskLevel, DocType), SearchBar, InvoiceTable (DataGrid + RiskBadge), Pagination                                 |
| `/invoices/:id`         | InvoiceDetailPage | InvoiceHeader, ValidationResultCard (3 layers), RiskAssessmentCard, InvoiceDataCard, AuditLogTimeline, ActionButtons (Edit/Submit/Approve/Reject) |
| `/invoices/upload`      | InvoiceUploadPage | FileDropzone (.xml/.pdf/.jpg/.png, max 10MB), UploadProgressBar, ProcessingStatusCard, ResultCard (success/partial/failure)                       |
| `/invoices/:id/edit`    | InvoiceEditPage   | InvoiceForm (React Hook Form + Yup), ConfidenceScoreIndicators (if OCR), ActionButtons (Save/Revalidate/Cancel)                                   |
| `/admin/approval-queue` | ApprovalQueuePage | PendingInvoicesTable (quick view modal, bulk approve/reject), Filters (Risk level, Date, Submitter)                                               |
| `/export`               | ExportPage        | FilterForm, FormatSelector (MISA/FAST/Standard), PreviewTable, ExportButton, ExportHistoryTable                                                   |

#### Reusable UI Components

| Component       | Mô tả                                     |
| --------------- | ----------------------------------------- |
| RiskBadge       | Green/Yellow/Orange/Red with icons        |
| StatusBadge     | Draft/Pending/Approved/Rejected           |
| ConfidenceScore | Percentage bar with color gradient        |
| DataTable       | Generic table with sort/filter/pagination |
| SearchInput     | Debounced search                          |
| DateRangePicker | Material-UI DatePicker                    |
| FileUploadZone  | Drag & drop                               |
| ProgressStepper | Multi-step process indicator              |
| ConfirmDialog   | Reusable confirmation modal               |
| LoadingOverlay  | Full-page or component-level              |

#### Custom Hooks

| Hook                        | Mô tả                            |
| --------------------------- | -------------------------------- |
| `useAuth()`                 | Authentication state & methods   |
| `useInvoices(filter)`       | Fetch & manage invoices          |
| `useInvoiceDetail(id)`      | Fetch single invoice             |
| `useDashboardData()`        | Fetch dashboard stats & charts   |
| `useFileUpload()`           | Handle file upload with progress |
| `useNotifications()`        | Real-time notifications          |
| `useDebounce(value, delay)` | Debounced value                  |

#### API Client (Axios)

```
axios.create({ baseURL, timeout })
├─ Request interceptor (add JWT token to headers)
├─ Response interceptor (handle 401, refresh token)
└─ Error handler (show toast notifications)
```

---

**[Tiếp theo: Phần 5-8 nằm trong file SYSTEM_ARCHITECTURE_PART2.md]**

File bổ sung bao gồm:

- **5. Data Flow Architecture** — Chi tiết flows (XML Upload, PDF/OCR, Approval Workflow)
- **6. Deployment Architecture** — AWS production setup (Multi-AZ, VPC, Auto Scaling)
- **7. Security Architecture** — Authentication, Authorization, Encryption
- **8. Scalability & Performance** — Caching, Optimization strategies
