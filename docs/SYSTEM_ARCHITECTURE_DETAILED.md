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

```
┌─────────────────────────────────────────────────────────────┐
│  1. OPERATIONAL EXCELLENCE (Vận hành xuất sắc)             │
│     ✓ Infrastructure as Code (CloudFormation/Terraform)     │
│     ✓ Automated deployment (Elastic Beanstalk)             │
│     ✓ Monitoring & logging (CloudWatch)                    │
│     ✓ Incident response procedures                         │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│  2. SECURITY (Bảo mật)                                      │
│     ✓ Identity & Access Management (IAM)                   │
│     ✓ Data encryption (at rest & in transit)               │
│     ✓ Network isolation (VPC, Security Groups)             │
│     ✓ Secrets management (Secrets Manager)                 │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│  3. RELIABILITY (Độ tin cậy)                                │
│     ✓ Multi-AZ database (RDS auto-failover)                │
│     ✓ Auto-scaling (Elastic Beanstalk)                     │
│     ✓ Backup & disaster recovery (automated RDS backups)   │
│     ✓ Health checks & auto-recovery                        │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│  4. PERFORMANCE EFFICIENCY (Hiệu suất)                      │
│     ✓ Right-sized resources (t3.small/medium for prod)     │
│     ✓ Serverless where appropriate (S3, Cognito)           │
│     ✓ Caching strategy (in-memory cache)                   │
│     ✓ Database optimization (indexes, connection pooling)  │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│  5. HIGH AVAILABILITY & COST OPTIMIZATION (Độ sẵn sàng cao) │
│     ✓ Multi-AZ Deployment (2 Availability Zones)           │
│     ✓ S3 lifecycle policies (auto-archive to Glacier)      │
│     ✓ Right-sizing instances (Auto Scaling 2-4 nodes)      │
│     ✓ Cost monitoring & alerts                             │
└─────────────────────────────────────────────────────────────┘
```

---

### 1.2 Architecture Style

**Hybrid Architecture**: Layered Monolith + Microservices (AI Processing)

```
┌───────────────────────────────────────────────────────────┐
│  WHY LAYERED MONOLITH (Backend)?                          │
├───────────────────────────────────────────────────────────┤
│  ✅ Team size: 2 backend devs (không cần microservices)   │
│  ✅ Timeline: 3 months (microservices phức tạp hơn)       │
│  ✅ Deployment: Đơn giản hóa (1 API service)              │
│  ✅ Data consistency: Dễ maintain ACID transactions       │
│  ✅ AWS Elastic Beanstalk: Native support monolith        │
└───────────────────────────────────────────────────────────┘

┌───────────────────────────────────────────────────────────┐
│  WHY Custom OCR API for AI?                               │
├───────────────────────────────────────────────────────────┤
│  ✅ Accuracy: PaddleOCR + VietOCR optimized for VN        │
│  ✅ Cost: Run internally, flat cost instead of pay-per-use│
│  ✅ Control: Full control over extraction logic           │
│  ✅ Team setup: Dedicated AI member handles this service  │
└───────────────────────────────────────────────────────────┘
```

---

### 1.3 High-Level Architecture Decision Records (ADR)

**ADR-001: Database - PostgreSQL on RDS**
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

**ADR-002: File Storage - Amazon S3**
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

**ADR-003: AI/OCR - Internal OCR API (PaddleOCR + VietOCR)**
```
Context:
  - Cần extract text từ PDF/Images (hóa đơn scan)
  - Accuracy requirement: ≥85% cho tiếng Việt
  - Team có 1 thành viên chuyên trách AI (xây dựng model)
  
Decision: Internal OCR API (PaddleOCR + VietOCR)
  
Rationale:
  ✓ Vietnamese Support → VietOCR vượt trội hơn các giải pháp quốc tế
  ✓ Team setup → Có nguồn lực AI chuyên biệt để tự host và manage model
  ✓ Cost Control → Tránh chi phí pay-per-use của các Cloud Managed Services
  
Alternatives Rejected:
  ✗ AWS Textract: Hỗ trợ tiếng Việt kém, trả phí per-page
  ✗ Google Vision API: Không support Vietnam region
  ✗ Azure Form Recognizer: Trả phí cao per-page
```

**ADR-004: Backend Framework - .NET 6 Web API**
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

**ADR-005: Frontend Framework - React 18 + TypeScript**
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

**ADR-006: Authentication - Amazon Cognito (JWT & OTP)**
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
  1. Đăng ký (Register): Gọi API `SignUpAsync` -> Người dùng được lưu vào Pool (Unconfirmed) -> Cognito tự bắn Email chứa OTP.
  2. Xác thực OTP (Verify): Gọi API `ConfirmSignUpAsync` kèm mã OTP -> Kích hoạt tài khoản.
  3. Đăng nhập (Login): Gọi API `InitiateAuthAsync` (USER_PASSWORD_AUTH) -> Xác thực thành công trả về JWT Tokens.
  
Payload:
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
                    │         SMARTINVOICE SHIELD SYSTEM         │
                    │    (Invoice Management & Risk Assessment)   │
                    └──────────────┬──────────────────────────────┘
                                   │
         ┌─────────────────────────┼─────────────────────────┐
         │                         │                         │
         ▼                         ▼                         ▼
┌──────────────────┐    ┌──────────────────┐    ┌──────────────────┐
│  MEMBER USERS    │    │  ADMIN USERS     │    │ SUPER ADMIN      │
│  (Kế toán viên)  │    │ (Kế toán trưởng) │    │ (System Admin)   │
├──────────────────┤    ├──────────────────┤    ├──────────────────┤
│ • Upload invoice │    │ • Approve/Reject │    │ • Manage users   │
│ • Edit data      │    │ • View dashboard │    │ • Manage config  │
│ • Submit         │    │ • Export reports │    │ • System monitor │
│ • Search         │    │ • Audit review   │    │ • NO invoice data│
└──────────────────┘    └──────────────────┘    └──────────────────┘
         │                         │                         │
         └─────────────────────────┼─────────────────────────┘
                                   │
                                   │ HTTPS (REST API)
                                   │
         ┌─────────────────────────┼─────────────────────────┐
         │                         │                         │
         ▼                         ▼                         ▼
┌──────────────────┐    ┌──────────────────┐    ┌──────────────────┐
│  EXTERNAL APIs   │    │  AWS SERVICES    │    │  FILE SOURCES    │
├──────────────────┤    ├──────────────────┤    ├──────────────────┤
│ • VietQR API     │    │ • S3             │    │ • Email (XML)    │
│   (MST verify)   │    │ • RDS PostgreSQL │    │ • Scanner (PDF)  │
│ • Internal OCR   │    │ • CloudWatch     │    │ • Mobile camera  │
│   (AI Team API)  │    │ • Secrets Mgr    │    │ • Manual upload  │
└──────────────────┘    └──────────────────┘    └──────────────────┘
```

### 2.2 System Boundaries

```
┌─────────────────────────────────────────────────────────────┐
│  INSIDE SYSTEM BOUNDARY                                     │
├─────────────────────────────────────────────────────────────┤
│  ✓ React Frontend (Web UI)                                 │
│  ✓ .NET Core API (Business Logic)                          │
│  ✓ PostgreSQL Database (Data Storage)                      │
│  ✓ S3 File Storage                                          │
│  ✓ Internal OCR Client (via HTTP API)                      │
│  ✓ Authentication & Authorization                           │
│  ✓ Audit Trail System                                       │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│  OUTSIDE SYSTEM BOUNDARY (External Dependencies)            │
├─────────────────────────────────────────────────────────────┤
│  ✗ VietQR API (MST verification) - 3rd party               │
│  ✗ Email Service Provider (AWS SES/SendGrid) - optional    │
│  ✗ Accounting Software (MISA/FAST) - export only           │
│  ✗ Tax Authority System - future integration               │
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
│                               │                                     │
│                               │ HTTPS (443)                         │
│                               │                                     │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │  [Container] REACT SPA                                       │  │
│  │  Technology: React 18 + TypeScript                           │  │
│  │  Hosting: AWS Amplify                                        │  │
│  │  ┌────────────────────────────────────────────────────────┐  │  │
│  │  │  • Login/Register pages                                │  │  │
│  │  │  • Invoice Management (List/Detail/Upload/Edit)        │  │  │
│  │  │  • Dashboard & Analytics                               │  │  │
│  │  │  • Admin Panel (Approval Queue, User Management)       │  │  │
│  │  │  • Search & Filter                                     │  │  │
│  │  │  • Export functionality                                │  │  │
│  │  └────────────────────────────────────────────────────────┘  │  │
│  │                                                              │  │
│  │  Dependencies:                                               │  │
│  │  • Material-UI v5 (UI components)                            │  │
│  │  • Recharts (Dashboard charts)                              │  │
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
│  │  ┌─────────────────────────────────────────────────────┐    │  │
│  │  │  PRESENTATION LAYER (Controllers)                   │    │  │
│  │  │  ├─ AuthController (Login, Register, RefreshToken) │    │  │
│  │  │  ├─ InvoiceController (CRUD, Validate, Submit)     │    │  │
│  │  │  ├─ DashboardController (Stats, Charts)            │    │  │
│  │  │  ├─ ExportController (Excel, PDF)                  │    │  │
│  │  │  ├─ AdminController (Users, Config)                │    │  │
│  │  │  └─ HealthCheckController (Monitoring)             │    │  │
│  │  └─────────────────────────────────────────────────────┘    │  │
│  │                           │                                  │  │
│  │  ┌─────────────────────────────────────────────────────┐    │  │
│  │  │  BUSINESS LOGIC LAYER (Services)                    │    │  │
│  │  │  ├─ InvoiceProcessorService (3-layer validation)   │    │  │
│  │  │  ├─ OcrClientService (Internal AI integration)     │    │  │
│  │  │  ├─ ValidationService (Risk calculation)           │    │  │
│  │  │  ├─ S3Service (File operations)                    │    │  │
│  │  │  ├─ SearchService (Full-text search)               │    │  │
│  │  │  ├─ ExportService (Excel generation)               │    │  │
│  │  │  ├─ NotificationService (Alerts)                   │    │  │
│  │  │  ├─ AuditLogService (Audit trail)                  │    │  │
│  │  │  └─ VietQRService (MST verification)               │    │  │
│  │  └─────────────────────────────────────────────────────┘    │  │
│  │                           │                                  │  │
│  │  ┌─────────────────────────────────────────────────────┐    │  │
│  │  │  DATA ACCESS LAYER (Repositories)                   │    │  │
│  │  │  ├─ IRepository<T> (Generic repository)            │    │  │
│  │  │  ├─ InvoiceRepository                              │    │  │
│  │  │  ├─ UserRepository                                 │    │  │
│  │  │  ├─ CompanyRepository                              │    │  │
│  │  │  ├─ FileStorageRepository                          │    │  │
│  │  │  ├─ AuditLogRepository                             │    │  │
│  │  │  └─ Unit of Work (Transaction management)          │    │  │
│  │  └─────────────────────────────────────────────────────┘    │  │
│  │                                                              │  │
│  │  Middleware Stack:                                           │  │
│  │  • JwtBearerAuthentication (validate JWT)                    │  │
│  │  • ExceptionHandlerMiddleware (global error handling)        │  │
│  │  • RequestLoggingMiddleware (Serilog)                        │  │
│  │  • CorrelationIdMiddleware (request tracing)                 │  │
│  └──────────────────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────────────────┘
         │                      │                      │
         │                      │                      │
         ▼                      ▼                      ▼
┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐
│ [Container]      │  │ [Container]      │  │ [External]       │
│ POSTGRESQL DB    │  │ AMAZON S3        │  │ INTERNAL OCR API │
│                  │  │                  │  │                  │
│ Technology:      │  │ Technology:      │  │ Technology:      │
│ Technology:      │  │ Technology:      │  │ PaddleOCR +      │
│ PostgreSQL 14    │  │ S3 Standard      │  │ VietOCR (Python) │
│                  │  │                  │  │                  │
│ Hosting:         │  │ Hosting:         │  │ Hosting:         │
│ AWS RDS          │  │ AWS S3           │  │ Custom Host      │
│ Multi-AZ         │  │ Cross-AZ         │  │                  │
│                  │  │ Buckets:         │  │ Endpoint:        │
│ Instance:        │  │ • dev-bucket     │  │ /api/v1/extract  │
│ db.t3.small      │  │ • prod-bucket    │  │                  │
│ (Production HA)  │  │                  │  │ Accuracy Goal:   │
│ Storage:         │  │ • Versioning     │  │ >85% for VN text │
│ 20 GB            │  │ • Lifecycle      │  │                  │
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

**React SPA Container**:
```
Responsibilities:
✓ User interface rendering
✓ Client-side validation (UX optimization)
✓ State management (React Context)
✓ API communication (Axios)
✓ Client-side routing (React Router)
✓ Session management (JWT storage)

Not Responsible For:
✗ Business logic (delegated to API)
✗ Data persistence (delegated to API)
✗ Authentication logic (only token storage)
```

**.NET Core API Container**:
```
Responsibilities:
✓ Request authentication & authorization
✓ Business logic execution (3-layer validation)
✓ Data validation (server-side)
✓ Database operations (via repositories)
✓ External API integration (S3, Internal OCR, VietQR)
✓ File processing orchestration
✓ Audit logging
✓ Error handling & logging

Not Responsible For:
✗ UI rendering (delegated to React)
✗ File storage (delegated to S3)
✗ OCR processing (delegated to Internal OCR API)
```

**PostgreSQL Database Container**:
```
Responsibilities:
✓ Persistent data storage
✓ ACID transactions
✓ Data integrity enforcement (constraints)
✓ Query optimization (indexes)
✓ Full-text search (native FTS)
✓ Backup & recovery (RDS automated)

Not Responsible For:
✗ Business logic (should be in API)
✗ File storage (use S3)
✗ Complex computations (use application layer)
```

**Amazon S3 Container**:
```
Responsibilities:
✓ File storage (XML, PDF, Images, Exports)
✓ File versioning
✓ Lifecycle management (archive to Glacier)
✓ Pre-signed URL generation
✓ Data durability (99.999999999%)

Not Responsible For:
✗ File processing (use API + OCR)
✗ Metadata storage (use PostgreSQL)
✗ Access control logic (use API + IAM)
```

---

## 4. COMPONENT ARCHITECTURE

### 4.1 Backend Component Diagram (Detailed)

```
┌─────────────────────────────────────────────────────────────────────┐
│                    .NET CORE WEB API (Detailed)                     │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ┌───────────────────────────────────────────────────────────────┐ │
│  │  HTTP REQUEST PIPELINE (Middleware)                           │ │
│  │  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐      │ │
│  │  │ CORS     │→ │ Auth     │→ │ Exception│→ │ Logging  │      │ │
│  │  │ Policy   │  │ (JWT)    │  │ Handler  │  │ (Serilog)│      │ │
│  │  └──────────┘  └──────────┘  └──────────┘  └──────────┘      │ │
│  └───────────────────────────────────────────────────────────────┘ │
│                              │                                      │
│                              ▼                                      │
│  ┌───────────────────────────────────────────────────────────────┐ │
│  │  CONTROLLERS (Presentation Layer)                             │ │
│  │  ┌─────────────────────────────────────────────────────────┐  │ │
│  │  │  AuthController                                         │  │ │
│  │  │  • POST /api/auth/register                              │  │ │
│  │  │  • POST /api/auth/login                                 │  │ │
│  │  │  • POST /api/auth/refresh-token                         │  │ │
│  │  │  • POST /api/auth/logout                                │  │ │
│  │  │  • GET  /api/auth/me                                    │  │ │
│  │  └─────────────────────────────────────────────────────────┘  │ │
│  │  ┌─────────────────────────────────────────────────────────┐  │ │
│  │  │  InvoiceController                                      │  │ │
│  │  │  • GET    /api/invoices (list with pagination)          │  │ │
│  │  │  • GET    /api/invoices/{id}                            │  │ │
│  │  │  • POST   /api/invoices/upload                          │  │ │
│  │  │  • PUT    /api/invoices/{id}                            │  │ │
│  │  │  • DELETE /api/invoices/{id}                            │  │ │
│  │  │  • POST   /api/invoices/{id}/validate                   │  │ │
│  │  │  • POST   /api/invoices/{id}/submit                     │  │ │
│  │  │  • POST   /api/invoices/{id}/approve                    │  │ │
│  │  │  • POST   /api/invoices/{id}/reject                     │  │ │
│  │  │  • GET    /api/invoices/{id}/audit-logs                 │  │ │
│  │  └─────────────────────────────────────────────────────────┘  │ │
│  │  ┌─────────────────────────────────────────────────────────┐  │ │
│  │  │  DashboardController                                    │  │ │
│  │  │  • GET /api/dashboard/stats                             │  │ │
│  │  │  • GET /api/dashboard/charts/invoice-by-month           │  │ │
│  │  │  • GET /api/dashboard/charts/risk-distribution          │  │ │
│  │  │  • GET /api/dashboard/charts/amount-trend               │  │ │
│  │  └─────────────────────────────────────────────────────────┘  │ │
│  │  ┌─────────────────────────────────────────────────────────┐  │ │
│  │  │  ExportController                                       │  │ │
│  │  │  • POST /api/export/excel (create export job)           │  │ │
│  │  │  • GET  /api/export/{id}/download                       │  │ │
│  │  │  • GET  /api/export/history                             │  │ │
│  │  └─────────────────────────────────────────────────────────┘  │ │
│  │  ┌─────────────────────────────────────────────────────────┐  │ │
│  │  │  AdminController                                        │  │ │
│  │  │  • GET  /api/admin/users                                │  │ │
│  │  │  • POST /api/admin/users                                │  │ │
│  │  │  • PUT  /api/admin/users/{id}                           │  │ │
│  │  │  • POST /api/admin/users/{id}/deactivate                │  │ │
│  │  │  • GET  /api/admin/system-config                        │  │ │
│  │  │  • PUT  /api/admin/system-config                        │  │ │
│  │  └─────────────────────────────────────────────────────────┘  │ │
│  └───────────────────────────────────────────────────────────────┘ │
│                              │                                      │
│                              ▼                                      │
│  ┌───────────────────────────────────────────────────────────────┐ │
│  │  SERVICES (Business Logic Layer)                              │ │
│  │                                                               │ │
│  │  ┌──────────────────────────────────────────────────────┐    │ │
│  │  │  InvoiceProcessorService (Core Business Logic)      │    │ │
│  │  │  ├─ ValidateXmlAsync(s3Key)                         │    │ │
│  │  │  │  ├─ Layer 1: XSD Structure Validation            │    │ │
│  │  │  │  ├─ Layer 2: Digital Signature Verification      │    │ │
│  │  │  │  │  └─ Anti-Spoofing: MST match check            │    │ │
│  │  │  │  └─ Layer 3: Business Logic Validation           │    │ │
│  │  │  │     ├─ Auto-detect invoice type                  │    │ │
│  │  │  │     ├─ Check mandatory fields                    │    │ │
│  │  │  │     ├─ Math validation (qty × price = total)     │    │ │
│  │  │  │     └─ MST verification (VietQR API)             │    │ │
│  │  │  └─ CalculateRiskLevel(validationResult)            │    │ │
│  │  └──────────────────────────────────────────────────────┘    │ │
│  │                                                               │ │
│  │  ┌──────────────────────────────────────────────────────┐    │ │
│  │  │  OcrClientService (AI Integration)                  │    │ │
│  │  │  ├─ ExtractInvoiceDataAsync(s3Url or fileBytes)     │    │ │
│  │  │  │  ├─ Call Internal OCR HTTP API                   │    │ │
│  │  │  │  ├─ Parse Custom JSON response                   │    │ │
│  │  │  │  ├─ Map to Invoice model                         │    │ │
│  │  │  │  └─ Extract confidence scores                    │    │ │
│  │  │  └─ SaveProcessingLog(result)                       │    │ │
│  │  └──────────────────────────────────────────────────────┘    │ │
│  │                                                               │ │
│  │  ┌──────────────────────────────────────────────────────┐    │ │
│  │  │  ValidationService (Risk Assessment)                │    │ │
│  │  │  ├─ ValidateInvoiceAsync(invoice)                   │    │ │
│  │  │  │  ├─ LEGAL checks (MST format, required fields)   │    │ │
│  │  │  │  ├─ VALID checks (signature, date logic)         │    │ │
│  │  │  │  └─ REASONABLE checks (amounts, math)            │    │ │
│  │  │  ├─ CalculateRiskLevel(checks)                      │    │ │
│  │  │  │  ├─ Green: All pass                              │    │ │
│  │  │  │  ├─ Yellow: Warnings only                        │    │ │
│  │  │  │  ├─ Orange: Some failures                        │    │ │
│  │  │  │  └─ Red: Critical failures                       │    │ │
│  │  │  └─ SaveRiskCheckResults(invoice, results)          │    │ │
│  │  └──────────────────────────────────────────────────────┘    │ │
│  │                                                               │ │
│  │  ┌──────────────────────────────────────────────────────┐    │ │
│  │  │  S3Service (File Operations)                        │    │ │
│  │  │  ├─ UploadFileAsync(stream, key)                    │    │ │
│  │  │  ├─ DownloadFileAsync(key)                          │    │ │
│  │  │  ├─ GeneratePresignedUrl(key, expiry)               │    │ │
│  │  │  ├─ DeleteFileAsync(key)                            │    │ │
│  │  │  └─ ListFilesAsync(prefix)                          │    │ │
│  │  └──────────────────────────────────────────────────────┘    │ │
│  │                                                               │ │
│  │  ┌──────────────────────────────────────────────────────┐    │ │
│  │  │  ExportService (Report Generation)                  │    │ │
│  │  │  ├─ GenerateExcelAsync(filter, format)              │    │ │
│  │  │  │  ├─ Query invoices from DB                       │    │ │
│  │  │  │  ├─ Format for MISA/FAST/Standard                │    │ │
│  │  │  │  ├─ Generate Excel (EPPlus library)              │    │ │
│  │  │  │  └─ Upload to S3                                 │    │ │
│  │  │  └─ GeneratePdfReportAsync(invoiceId)               │    │ │
│  │  └──────────────────────────────────────────────────────┘    │ │
│  │                                                               │ │
│  │  ┌──────────────────────────────────────────────────────┐    │ │
│  │  │  SearchService (Full-Text Search)                   │    │ │
│  │  │  ├─ SearchInvoicesAsync(query)                      │    │ │
│  │  │  │  └─ PostgreSQL FTS (to_tsvector, to_tsquery)     │    │ │
│  │  │  └─ BuildSearchFilter(criteria)                     │    │ │
│  │  └──────────────────────────────────────────────────────┘    │ │
│  │                                                               │ │
│  │  ┌──────────────────────────────────────────────────────┐    │ │
│  │  │  AuditLogService (Audit Trail)                      │    │ │
│  │  │  ├─ LogActionAsync(invoice, user, action, changes)  │    │ │
│  │  │  │  ├─ Calculate diff (OldData vs NewData)          │    │ │
│  │  │  │  ├─ Create audit record (immutable)              │    │ │
│  │  │  │  └─ Capture context (IP, UserAgent, Timestamp)   │    │ │
│  │  │  └─ GetAuditTrailAsync(invoiceId)                   │    │ │
│  │  └──────────────────────────────────────────────────────┘    │ │
│  │                                                               │ │
│  │  ┌──────────────────────────────────────────────────────┐    │ │
│  │  │  NotificationService (Alerts)                       │    │ │
│  │  │  ├─ CreateNotificationAsync(user, type, content)    │    │ │
│  │  │  ├─ SendEmailAsync(user, template) [Optional]       │    │ │
│  │  │  └─ GetUnreadCountAsync(userId)                     │    │ │
│  │  └──────────────────────────────────────────────────────┘    │ │
│  │                                                               │ │
│  │  ┌──────────────────────────────────────────────────────┐    │ │
│  │  │  VietQRService (External API Integration)           │    │ │
│  │  │  ├─ VerifyTaxCodeAsync(taxCode)                     │    │ │
│  │  │  │  ├─ Call VietQR API                              │    │ │
│  │  │  │  ├─ Parse response                               │    │ │
│  │  │  │  └─ Cache result (in-memory, 24h)                │    │ │
│  │  │  └─ ValidateTaxCodeFormat(taxCode)                  │    │ │
│  │  │     ├─ 10 digits: Mod-11 checksum                   │    │ │
│  │  │     ├─ 13 digits: 10 digits + "-NNN"                │    │ │
│  │  │     └─ 12 digits: CCCD (numeric only)               │    │ │
│  │  └──────────────────────────────────────────────────────┘    │ │
│  └───────────────────────────────────────────────────────────────┘ │
│                              │                                      │
│                              ▼                                      │
│  ┌───────────────────────────────────────────────────────────────┐ │
│  │  REPOSITORIES (Data Access Layer)                             │ │
│  │  ┌──────────────────────────────────────────────────────┐    │ │
│  │  │  GenericRepository<T> : IRepository<T>              │    │ │
│  │  │  ├─ GetByIdAsync(id)                                │    │ │
│  │  │  ├─ GetAllAsync(filter, orderBy, includes)          │    │ │
│  │  │  ├─ AddAsync(entity)                                │    │ │
│  │  │  ├─ UpdateAsync(entity)                             │    │ │
│  │  │  ├─ DeleteAsync(id)                                 │    │ │
│  │  │  └─ CountAsync(filter)                              │    │ │
│  │  └──────────────────────────────────────────────────────┘    │ │
│  │  ┌──────────────────────────────────────────────────────┐    │ │
│  │  │  InvoiceRepository : GenericRepository<Invoice>     │    │ │
│  │  │  ├─ GetByCompanyAsync(companyId, filter)            │    │ │
│  │  │  ├─ SearchAsync(query)                              │    │ │
│  │  │  ├─ GetPendingApprovalsAsync(companyId)             │    │ │
│  │  │  └─ GetByRiskLevelAsync(riskLevel)                  │    │ │
│  │  └──────────────────────────────────────────────────────┘    │ │
│  │  ┌──────────────────────────────────────────────────────┐    │ │
│  │  │  UnitOfWork : IUnitOfWork                           │    │ │
│  │  │  ├─ BeginTransactionAsync()                         │    │ │
│  │  │  ├─ CommitAsync()                                   │    │ │
│  │  │  ├─ RollbackAsync()                                 │    │ │
│  │  │  └─ SaveChangesAsync()                              │    │ │
│  │  └──────────────────────────────────────────────────────┘    │ │
│  └───────────────────────────────────────────────────────────────┘ │
│                              │                                      │
│                              ▼                                      │
│  ┌───────────────────────────────────────────────────────────────┐ │
│  │  DATA MODELS (Domain Entities)                                │ │
│  │  ├─ Company                                                   │ │
│  │  ├─ User                                                      │ │
│  │  ├─ Invoice (Central entity)                                 │ │
│  │  ├─ DocumentType                                             │ │
│  │  ├─ FileStorage                                              │ │
│  │  ├─ ValidationLayer                                          │ │
│  │  ├─ InvoiceAuditLog                                          │ │
│  │  ├─ RiskCheckResult                                          │ │
│  │  ├─ Notification                                             │ │
│  │  ├─ ExportHistory                                            │ │
│  │  ├─ AIProcessingLog                                          │ │
│  │  └─ SystemConfiguration                                      │ │
│  └───────────────────────────────────────────────────────────────┘ │
│                              │                                      │
│                              ▼                                      │
│  ┌───────────────────────────────────────────────────────────────┐ │
│  │  DATABASE CONTEXT (Entity Framework Core)                     │ │
│  │  ├─ AppDbContext : DbContext                                 │ │
│  │  │  ├─ DbSet<Company> Companies                              │ │
│  │  │  ├─ DbSet<User> Users                                     │ │
│  │  │  ├─ DbSet<Invoice> Invoices                               │ │
│  │  │  └─ ... (12 DbSets total)                                 │ │
│  │  │                                                            │ │
│  │  │  OnModelCreating():                                        │ │
│  │  │  ├─ Configure relationships                               │ │
│  │  │  ├─ Configure indexes                                     │ │
│  │  │  ├─ Configure constraints                                 │ │
│  │  │  └─ Seed data (DocumentTypes)                             │ │
│  │  └────────────────────────────────────────────────────────────│ │
│  └───────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────┘
```

---

### 4.2 Frontend Component Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                    REACT SPA (Component Tree)                       │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ┌───────────────────────────────────────────────────────────────┐ │
│  │  APP ROOT                                                     │ │
│  │  ├─ AuthProvider (Context: user, token, login, logout)       │ │
│  │  ├─ ThemeProvider (Material-UI theme)                        │ │
│  │  └─ Router (React Router v6)                                 │ │
│  └───────────────────────────────────────────────────────────────┘ │
│                              │                                      │
│                              ▼                                      │
│  ┌───────────────────────────────────────────────────────────────┐ │
│  │  LAYOUT COMPONENTS                                            │ │
│  │  ┌─────────────────────────────────────────────────────────┐  │ │
│  │  │  MainLayout                                             │  │ │
│  │  │  ├─ AppBar (Header)                                     │  │ │
│  │  │  │  ├─ Logo                                             │  │ │
│  │  │  │  ├─ Navigation links                                 │  │ │
│  │  │  │  ├─ NotificationBadge (unread count)                 │  │ │
│  │  │  │  └─ UserMenu (Logout, Profile)                       │  │ │
│  │  │  │                                                       │  │ │
│  │  │  ├─ Sidebar (Drawer)                                    │  │ │
│  │  │  │  ├─ Dashboard link                                   │  │ │
│  │  │  │  ├─ Invoices link                                    │  │ │
│  │  │  │  ├─ Upload link                                      │  │ │
│  │  │  │  ├─ Reports link                                     │  │ │
│  │  │  │  └─ Admin link (if role = Admin)                     │  │ │
│  │  │  │                                                       │  │ │
│  │  │  └─ Content Area (Outlet for routes)                    │  │ │
│  │  └─────────────────────────────────────────────────────────┘  │ │
│  └───────────────────────────────────────────────────────────────┘ │
│                              │                                      │
│                              ▼                                      │
│  ┌───────────────────────────────────────────────────────────────┐ │
│  │  PAGE COMPONENTS (Routes)                                     │ │
│  │                                                               │ │
│  │  ┌─────────────────────────────────────────────────────────┐  │ │
│  │  │  /login → LoginPage                                     │  │ │
│  │  │  ├─ LoginForm                                           │  │ │
│  │  │  │  ├─ EmailField (validation)                          │  │ │
│  │  │  │  ├─ PasswordField (masked)                           │  │ │
│  │  │  │  └─ LoginButton (loading state)                      │  │ │
│  │  │  └─ useAuth() hook (calls /api/auth/login)              │  │ │
│  │  └─────────────────────────────────────────────────────────┘  │ │
│  │                                                               │ │
│  │  ┌─────────────────────────────────────────────────────────┐  │ │
│  │  │  /dashboard → DashboardPage                             │  │ │
│  │  │  ├─ StatCards (4 cards)                                 │  │ │
│  │  │  │  ├─ TotalInvoicesCard                                │  │ │
│  │  │  │  ├─ PendingApprovalCard                              │  │ │
│  │  │  │  ├─ HighRiskCard (Red + Orange)                      │  │ │
│  │  │  │  └─ TotalAmountCard                                  │  │ │
│  │  │  │                                                       │  │ │
│  │  │  ├─ Charts (Recharts)                                   │  │ │
│  │  │  │  ├─ RiskDistributionPieChart                         │  │ │
│  │  │  │  ├─ InvoiceCountByMonthBarChart                      │  │ │
│  │  │  │  └─ AmountTrendLineChart                            │  │ │
│  │  │  │                                                       │  │ │
│  │  │  └─ useDashboardData() hook                             │  │ │
│  │  └─────────────────────────────────────────────────────────┘  │ │
│  │                                                               │ │
│  │  ┌─────────────────────────────────────────────────────────┐  │ │
│  │  │  /invoices → InvoiceListPage                            │  │ │
│  │  │  ├─ FilterSidebar                                       │  │ │
│  │  │  │  ├─ DateRangePicker                                  │  │ │
│  │  │  │  ├─ StatusFilter (multi-select)                      │  │ │
│  │  │  │  ├─ RiskLevelFilter (multi-select)                   │  │ │
│  │  │  │  └─ DocumentTypeFilter                               │  │ │
│  │  │  │                                                       │  │ │
│  │  │  ├─ SearchBar (full-text search)                        │  │ │
│  │  │  │                                                       │  │ │
│  │  │  ├─ InvoiceTable (DataGrid)                             │  │ │
│  │  │  │  ├─ Columns: Number, Date, Seller, Buyer, Amount,    │  │ │
│  │  │  │  │          Status, RiskLevel, Actions               │  │ │
│  │  │  │  ├─ RiskBadge component (color-coded)                │  │ │
│  │  │  │  └─ Actions: View, Edit, Delete                      │  │ │
│  │  │  │                                                       │  │ │
│  │  │  └─ Pagination                                          │  │ │
│  │  └─────────────────────────────────────────────────────────┘  │ │
│  │                                                               │ │
│  │  ┌─────────────────────────────────────────────────────────┐  │ │
│  │  │  /invoices/:id → InvoiceDetailPage                      │  │ │
│  │  │  ├─ InvoiceHeader (Number, Date, Status badge)          │  │ │
│  │  │  │                                                       │  │ │
│  │  │  ├─ ValidationResultCard                                │  │ │
│  │  │  │  ├─ Layer1Result (Structure)                         │  │ │
│  │  │  │  ├─ Layer2Result (Signature)                         │  │ │
│  │  │  │  └─ Layer3Result (Business Logic)                    │  │ │
│  │  │  │                                                       │  │ │
│  │  │  ├─ RiskAssessmentCard                                  │  │ │
│  │  │  │  ├─ RiskLevelBadge (large, prominent)                │  │ │
│  │  │  │  └─ RiskReasonsList                                  │  │ │
│  │  │  │                                                       │  │ │
│  │  │  ├─ InvoiceDataCard (table)                             │  │ │
│  │  │  │  ├─ General info (Number, Date, Currency)            │  │ │
│  │  │  │  ├─ Seller info                                      │  │ │
│  │  │  │  ├─ Buyer info                                       │  │ │
│  │  │  │  ├─ Line items table                                 │  │ │
│  │  │  │  └─ Total amounts                                    │  │ │
│  │  │  │                                                       │  │ │
│  │  │  ├─ AuditLogTimeline (chronological)                    │  │ │
│  │  │  │  └─ Each entry: User, Action, Timestamp, Changes     │  │ │
│  │  │  │                                                       │  │ │
│  │  │  └─ ActionButtons                                       │  │ │
│  │  │     ├─ Edit (if Draft)                                  │  │ │
│  │  │     ├─ Submit (if Draft)                                │  │ │
│  │  │     ├─ Approve (if Pending & isAdmin)                   │  │ │
│  │  │     └─ Reject (if Pending & isAdmin)                    │  │ │
│  │  └─────────────────────────────────────────────────────────┘  │ │
│  │                                                               │ │
│  │  ┌─────────────────────────────────────────────────────────┐  │ │
│  │  │  /invoices/upload → InvoiceUploadPage                   │  │ │
│  │  │  ├─ FileDropzone                                        │  │ │
│  │  │  │  ├─ Drag & drop area                                 │  │ │
│  │  │  │  ├─ File type validation (.xml, .pdf, .jpg, .png)    │  │ │
│  │  │  │  ├─ Size validation (max 10MB)                       │  │ │
│  │  │  │  └─ Preview thumbnail                                │  │ │
│  │  │  │                                                       │  │ │
│  │  │  ├─ UploadProgressBar                                   │  │ │
│  │  │  │  └─ Shows: Uploading → Processing → Validating       │  │ │
│  │  │  │                                                       │  │ │
│  │  │  ├─ ProcessingStatusCard                                │  │ │
│  │  │  │  ├─ If XML: Show 3-layer validation progress         │  │ │
│  │  │  │  └─ If PDF: Show OCR extraction progress             │  │ │
│  │  │  │                                                       │  │ │
│  │  │  └─ ResultCard (after processing)                       │  │ │
│  │  │     ├─ Success: Show extracted data, risk level         │  │ │
│  │  │     ├─ Partial: Show warnings, allow manual correction  │  │ │
│  │  │     └─ Failure: Show errors, suggestions                │  │ │
│  │  └─────────────────────────────────────────────────────────┘  │ │
│  │                                                               │ │
│  │  ┌─────────────────────────────────────────────────────────┐  │ │
│  │  │  /invoices/:id/edit → InvoiceEditPage                   │  │ │
│  │  │  ├─ InvoiceForm (React Hook Form + Yup validation)      │  │ │
│  │  │  │  ├─ GeneralInfoSection (Number, Date, etc.)          │  │ │
│  │  │  │  ├─ SellerInfoSection (Name, MST, Address, etc.)     │  │ │
│  │  │  │  ├─ BuyerInfoSection                                 │  │ │
│  │  │  │  ├─ LineItemsSection (dynamic array)                 │  │ │
│  │  │  │  │  └─ Add/Remove line item buttons                  │  │ │
│  │  │  │  └─ TotalsSection (auto-calculated)                  │  │ │
│  │  │  │                                                       │  │ │
│  │  │  ├─ ConfidenceScoreIndicators (if OCR)                  │  │ │
│  │  │  │  └─ Highlight fields with low confidence (<80%)      │  │ │
│  │  │  │                                                       │  │ │
│  │  │  └─ ActionButtons                                       │  │ │
│  │  │     ├─ Save (create new version)                        │  │ │
│  │  │     ├─ Revalidate (run validation again)                │  │ │
│  │  │     └─ Cancel                                           │  │ │
│  │  └─────────────────────────────────────────────────────────┘  │ │
│  │                                                               │ │
│  │  ┌─────────────────────────────────────────────────────────┐  │ │
│  │  │  /admin/approval-queue → ApprovalQueuePage (Admin only) │  │ │
│  │  │  ├─ PendingInvoicesTable                                │  │ │
│  │  │  │  ├─ Quick view modal                                 │  │ │
│  │  │  │  └─ Bulk approve/reject                              │  │ │
│  │  │  │                                                       │  │ │
│  │  │  └─ Filters: Risk level, Date, Submitter                │  │ │
│  │  └─────────────────────────────────────────────────────────┘  │ │
│  │                                                               │ │
│  │  ┌─────────────────────────────────────────────────────────┐  │ │
│  │  │  /export → ExportPage                                   │  │ │
│  │  │  ├─ FilterForm (same as Invoice List filters)           │  │ │
│  │  │  ├─ FormatSelector (MISA/FAST/Standard)                 │  │ │
│  │  │  ├─ PreviewTable (shows what will be exported)          │  │ │
│  │  │  ├─ ExportButton (generates file)                       │  │ │
│  │  │  └─ ExportHistoryTable (past exports with download)     │  │ │
│  │  └─────────────────────────────────────────────────────────┘  │ │
│  └───────────────────────────────────────────────────────────────┘ │
│                              │                                      │
│                              ▼                                      │
│  ┌───────────────────────────────────────────────────────────────┐ │
│  │  REUSABLE UI COMPONENTS                                       │ │
│  │  ├─ RiskBadge (Green/Yellow/Orange/Red with icons)           │ │
│  │  ├─ StatusBadge (Draft/Pending/Approved/Rejected)            │ │
│  │  ├─ ConfidenceScore (percentage bar with color gradient)     │ │
│  │  ├─ DataTable (generic table with sort/filter/pagination)    │ │
│  │  ├─ SearchInput (debounced search)                           │ │
│  │  ├─ DateRangePicker (Material-UI DatePicker)                 │ │
│  │  ├─ FileUploadZone (drag & drop)                             │ │
│  │  ├─ ProgressStepper (multi-step process indicator)           │ │
│  │  ├─ ConfirmDialog (reusable confirmation modal)              │ │
│  │  └─ LoadingOverlay (full-page or component-level)            │ │
│  └───────────────────────────────────────────────────────────────┘ │
│                              │                                      │
│                              ▼                                      │
│  ┌───────────────────────────────────────────────────────────────┐ │
│  │  CUSTOM HOOKS                                                 │ │
│  │  ├─ useAuth() - Authentication state & methods               │ │
│  │  ├─ useInvoices(filter) - Fetch & manage invoices            │ │
│  │  ├─ useInvoiceDetail(id) - Fetch single invoice              │ │
│  │  ├─ useDashboardData() - Fetch dashboard stats & charts      │ │
│  │  ├─ useFileUpload() - Handle file upload with progress       │ │
│  │  ├─ useNotifications() - Real-time notifications             │ │
│  │  └─ useDebounce(value, delay) - Debounced value              │ │
│  └───────────────────────────────────────────────────────────────┘ │
│                              │                                      │
│                              ▼                                      │
│  ┌───────────────────────────────────────────────────────────────┐ │
│  │  API CLIENT (Axios)                                           │ │
│  │  ├─ axios.create({ baseURL, timeout })                       │ │
│  │  ├─ Request interceptor (add JWT token to headers)           │ │
│  │  ├─ Response interceptor (handle 401, refresh token)         │ │
│  │  └─ Error handler (show toast notifications)                 │ │
│  └───────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────┘
```

---

**[Tiếp theo: Phần 5-8 sẽ được tạo trong file tiếp theo do giới hạn độ dài]**

Tài liệu này đang được xây dựng, tôi sẽ tiếp tục tạo phần còn lại:
- Data Flow Architecture (chi tiết flows)
- Deployment Architecture (AWS production setup)
- Security Architecture (authentication, authorization, encryption)
- Scalability & Performance (caching, optimization)

Bạn muốn tôi tiếp tục không?
