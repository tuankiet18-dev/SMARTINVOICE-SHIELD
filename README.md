<p align="center">
  <img src="https://img.shields.io/badge/.NET-9.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET 9" />
  <img src="https://img.shields.io/badge/React-18-61DAFB?style=for-the-badge&logo=react&logoColor=black" alt="React 18" />
  <img src="https://img.shields.io/badge/Python-3.10+-3776AB?style=for-the-badge&logo=python&logoColor=white" alt="Python 3.10+" />
  <img src="https://img.shields.io/badge/PostgreSQL-16-4169E1?style=for-the-badge&logo=postgresql&logoColor=white" alt="PostgreSQL 16" />
  <img src="https://img.shields.io/badge/AWS-Cloud-FF9900?style=for-the-badge&logo=amazonaws&logoColor=white" alt="AWS" />
  <img src="https://img.shields.io/badge/Docker-Compose-2496ED?style=for-the-badge&logo=docker&logoColor=white" alt="Docker" />
</p>

<h1 align="center">🛡️ SmartInvoice Shield</h1>

<p align="center">
  <strong>AI-Powered Invoice Management & Tax Risk Assessment Platform</strong><br/>
  <em>Automate data extraction · Assess tax compliance risk · Manage enterprise invoices at scale</em>
</p>

<p align="center">
  <a href="#-key-features">Features</a> •
  <a href="#-system-architecture">Architecture</a> •
  <a href="#-technology-stack">Tech Stack</a> •
  <a href="#-project-structure">Project Structure</a> •
  <a href="#-local-development">Getting Started</a> •
  <a href="#-cloud-deployment">Deployment</a> •
  <a href="#-documentation">Docs</a>
</p>

---

## 📋 Overview

**SmartInvoice Shield** is a multi-tenant SaaS platform designed for Vietnamese enterprises to automate the management of electronic invoices (e-invoices). The system leverages a **triple-engine AI/OCR pipeline** (Gemini Vision API, PaddleOCR, VietOCR) to extract invoice data from PDF/Image/XML files, performs automated **multi-tier tax risk assessment** based on General Department of Taxation (GDT) criteria, and provides a full invoice lifecycle management workflow — from upload to approval.

### Who is it for?

| Role | Description |
|------|-------------|
| **Accountant** | Upload invoices, review OCR results, reconcile data |
| **Chief Accountant** | Second-level approval, validate tax compliance |
| **Company Admin** | Full company management, team & subscription control |
| **System Admin (SuperAdmin)** | Platform-wide configuration, global blacklist, tenant management |
| **Viewer** | Read-only access to invoice data |

---

## ✨ Key Features

### 🤖 AI-Powered Data Extraction (OCR)
- **Triple-engine OCR pipeline**: Gemini Vision API → PaddleOCR → VietOCR with automatic fallback
- **Multi-format support**: PDF, PNG, JPG images and XML e-invoice files
- **Batch upload**: Process multiple invoices simultaneously
- **OCR review modal**: Side-by-side comparison of original image vs. extracted data
- **Structured extraction**: Invoice number, date, seller/buyer tax codes, line items, VAT amounts

### 📊 Multi-Tier Risk Assessment
Risk scoring based on **Decision No. 78/QĐ-TCT** of the General Department of Taxation:
- **Tier I — Qualitative Risk**: Cross-check tax codes against blacklisted/dissolved companies
- **Tier II — Quantitative Risk (AI Scoring)**: Anomaly detection on invoice frequency, amounts vs. registered capital
- **Tier III — Reference Risk**: Historical tax compliance data and third-party verification

Risk levels: 🟢 `Green` · 🟡 `Yellow` · 🟠 `Orange` · 🔴 `Red`

### 📝 Invoice Lifecycle Management
- **Workflow**: `Processing` → `Draft` → `Pending` → `Approved` / `Rejected` → `Archived`
- **Audit trail**: Complete logging of all actions with user attribution
- **Soft delete & trash**: Recoverable invoice deletion

### 💳 SaaS & Payments
- **Subscription tiers**: Free, Standard, Premium, Enterprise
- **VnPay integration**: HMAC-SHA512 secured online payment gateway
- **Quota management**: Invoice count and storage limits per company

### 🏢 Multi-Tenant Architecture
- **Data isolation**: Complete tenant separation at the database level
- **Company-scoped RBAC**: 5 roles × 14 granular permissions
- **Tenant status middleware**: Automatic enforcement of subscription status

### 📈 Analytics & Reporting
- **Interactive dashboard**: VAT input summaries, top suppliers, cost analysis
- **Export**: Excel (`.xlsx`) and PDF report generation
- **Configurable export templates**: Custom column mappings per company

### 🔔 Notifications
- **In-app notifications**: Real-time alerts for risk warnings, approval requests, and system events

### 🏛️ VietQR Integration
- **Tax code verification**: Async validation of business registration via VietQR API
- **Resilience**: Polly retry (3×, exponential backoff), circuit breaker (5 failures → 1 min cooldown), 5s timeout

---

## 🏗️ System Architecture

The system follows an **event-driven, microservice-oriented architecture** deployed on AWS:

```
┌─────────────────────────────────────────────────────────────────────┐
│                        INTERNET                                     │
│   👤 User ──→ AWS Amplify (React SPA)                              │
│              ──→ CloudFront (HTTPS Proxy) ──→ ALB ──→ Backend      │
│   💳 VnPay Gateway        🏛️ VietQR API                            │
└───────────────────────────────┬─────────────────────────────────────┘
                                │
┌───────────────────────────────▼─────────────────────────────────────┐
│                  AWS VPC (10.0.0.0/16)                              │
│                                                                     │
│  ┌─ Public Subnets ──────────────────────────────────────────────┐  │
│  │  ALB (Multi-AZ)  ·  NAT Gateway + Elastic IP                 │  │
│  └───────────────────────────────────────────────────────────────┘  │
│                                                                     │
│  ┌─ Private Subnets ─────────────────────────────────────────────┐  │
│  │                                                                │  │
│  │  ┌──────────────┐   ┌──────────────┐   ┌─────────────────┐   │  │
│  │  │ EC2 t3.micro │   │ ECS Fargate  │   │ RDS PostgreSQL  │   │  │
│  │  │ .NET 9 API   │◄─►│ Python OCR   │   │ 16.x Multi-AZ   │   │  │
│  │  │ (2-4 inst.)  │   │ (2 tasks)    │   │                 │   │  │
│  │  └──────┬───────┘   └──────────────┘   └─────────────────┘   │  │
│  │         │                                                      │  │
│  │         ├──→ Amazon S3 (AES-256 encrypted)                    │  │
│  │         ├──→ Amazon SQS (OCR Queue + VietQR Queue)            │  │
│  │         ├──→ Amazon Cognito (JWT Auth)                        │  │
│  │         └──→ SSM Parameter Store (Secrets)                    │  │
│  └───────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘
```

### Processing Pipeline

```
1. Upload ──→ 2. S3 Storage ──→ 3. SQS OCR Queue ──→ 4. Fargate OCR
                                                            │
                                                     ┌──────▼──────┐
                                                     │ Gemini API  │
                                                     │     OR      │
                                                     │ PaddleOCR + │
                                                     │ VietOCR     │
                                                     └──────┬──────┘
                                                            │
5. Update DB ◄── Backend API ◄── OCR Results ◄─────────────┘
       │
       ▼
6. SQS VietQR Queue ──→ 7. VietQR Validation ──→ 8. Risk Level Update
```

---

## 🛠️ Technology Stack

### Backend — `.NET 9 Web API`

| Category | Technology |
|----------|------------|
| **Runtime** | .NET 9, ASP.NET Core Web API |
| **ORM** | Entity Framework Core 9 (Code-First Migrations) |
| **Database** | PostgreSQL 16 via Npgsql |
| **Auth** | AWS Cognito + JWT Bearer, Custom Claims Transformer |
| **Storage** | AWS S3 (Presigned URLs, AES-256 SSE) |
| **Messaging** | AWS SQS (Standard Queues, Long Polling) |
| **Config** | AWS Systems Manager Parameter Store |
| **Resilience** | Polly (Retry, Circuit Breaker, Timeout) |
| **Export** | ClosedXML (Excel generation) |
| **API Docs** | Swagger / Swashbuckle |
| **Testing** | Bogus (seed data generation) |

### Frontend — `React + TypeScript`

| Category | Technology |
|----------|------------|
| **Framework** | React 18 + TypeScript |
| **Build Tool** | Vite 5 |
| **UI Library** | Ant Design 6 + Shadcn/UI (Radix primitives) |
| **Styling** | Tailwind CSS 3 |
| **State** | TanStack React Query |
| **Forms** | React Hook Form + Zod validation |
| **Charts** | Recharts + Ant Design Charts |
| **Routing** | React Router DOM 6 |
| **Testing** | Vitest + React Testing Library |
| **Deployment** | AWS Amplify (auto-build from GitHub) |

### AI / OCR Service — `Python 3.10+`

| Category | Technology |
|----------|------------|
| **OCR Engines** | PaddleOCR (text detection), VietOCR (Vietnamese recognition) |
| **LLM** | Google Gemini Flash 1.5 (structured extraction) |
| **Pipeline** | Custom `InvoiceExtractionEngine` (Extract → Validate → Schema) |
| **Normalizers** | Date, Money, Tax Code, OCR text normalization |
| **Validators** | VAT cross-checking, business rule validation |
| **Schema** | LayoutLMv3 label mapping (header/table/footer) |
| **API** | Flask / FastAPI |
| **Deployment** | AWS ECS Fargate (2 vCPU, 4 GB RAM) |

### Infrastructure & DevOps

| Category | Technology |
|----------|------------|
| **Cloud** | AWS (`ap-southeast-1`) |
| **Compute** | Elastic Beanstalk (Docker on EC2), ECS Fargate |
| **Networking** | VPC (2 AZ, 4 Subnets), ALB, NAT Gateway, Cloud Map |
| **CDN/HTTPS** | CloudFront (HTTPS Proxy), Amplify CDN |
| **CI/CD** | GitHub Actions (2 pipelines) |
| **Container Registry** | Amazon ECR |
| **Monitoring** | CloudWatch Logs + Alarms, SNS Alerts |

---

## 📁 Project Structure

```
SmartInvoice-Shield/
├── 📂 SmartInvoice.API/            # .NET 9 Backend API
│   ├── Controllers/                # 17 API Controllers
│   │   ├── AuthController.cs       # Login, Register, Email Verification
│   │   ├── InvoicesController.cs   # Upload, CRUD, Search, Filter
│   │   ├── ValidationController.cs # Business rule validation
│   │   ├── PaymentController.cs    # VnPay integration
│   │   ├── DashboardController.cs  # Analytics & metrics
│   │   ├── ExportsController.cs    # Excel/PDF export
│   │   ├── UsersController.cs      # User management
│   │   ├── CompaniesController.cs  # Company/tenant CRUD
│   │   ├── BlacklistController.cs  # Global & local blacklist
│   │   ├── SystemConfigController  # Dynamic system config
│   │   ├── NotificationsController # In-app alerts
│   │   ├── AuditLogController.cs   # Audit trail
│   │   ├── SubscriptionPackages... # SaaS package management
│   │   ├── ExportConfigController  # Export template config
│   │   ├── SettingsController.cs   # Company settings
│   │   └── HealthController.cs     # Health check endpoint
│   │
│   ├── Services/
│   │   ├── Interfaces/             # 20 service contracts
│   │   └── Implementations/       # 22 service implementations
│   │       ├── InvoiceService.cs          # Core invoice logic (~105 KB)
│   │       ├── InvoiceProcessorService.cs # XML parsing & validation (~68 KB)
│   │       ├── OcrWorkerService.cs        # 7-step SQS background worker
│   │       ├── VietQrSqsConsumerService   # Async tax code validation
│   │       ├── VietQrClientService.cs     # VietQR API client + Polly
│   │       ├── VnPayService.cs            # Payment gateway integration
│   │       ├── AuthService.cs             # Cognito auth operations
│   │       ├── ExportService.cs           # Excel/PDF generation
│   │       └── ...                        # 14 more services
│   │
│   ├── Entities/                   # 16 EF Core entities
│   ├── Enums/                      # InvoiceStatus, RiskLevel, UserRole...
│   ├── Repositories/               # Unit of Work pattern
│   ├── DTOs/                       # Request/Response models
│   ├── Constants/                  # Permissions (14), ErrorCodes
│   ├── Security/                   # ClaimsTransformer (RBAC)
│   ├── Middleware/                 # MaintenanceMiddleware
│   ├── Middlewares/                # TenantStatusMiddleware
│   ├── Migrations/                 # EF Core migrations
│   ├── Resources/                  # InvoiceSchema.xsd
│   ├── Template/                   # Excel export templates
│   ├── Program.cs                  # Application entry point & DI config
│   ├── Dockerfile                  # Production Docker image
│   └── Dockerfile.dev              # Development Docker image (hot-reload)
│
├── 📂 SmartInvoice.Frontend/       # React TypeScript SPA
│   ├── src/
│   │   ├── pages/                  # 25 page components
│   │   │   ├── Dashboard.tsx       # Main analytics dashboard
│   │   │   ├── UploadInvoice.tsx   # Multi-file upload with preview
│   │   │   ├── InvoiceList.tsx     # Searchable invoice table
│   │   │   ├── InvoiceDetail.tsx   # Invoice detail + OCR review
│   │   │   ├── ValidationPage.tsx  # Business rule validation UI
│   │   │   ├── ApprovalDashboard   # Pending approvals overview
│   │   │   ├── TeamManagement.tsx  # User & role management
│   │   │   ├── SubscriptionPage    # Pricing & plan management
│   │   │   ├── ReportsPage.tsx     # Financial reporting
│   │   │   ├── SystemConfig.tsx    # SuperAdmin system config
│   │   │   ├── TenantManagement    # SuperAdmin tenant overview
│   │   │   ├── GlobalBlacklist     # SuperAdmin blacklist mgmt
│   │   │   └── ...                 # 12 more pages
│   │   ├── components/             # Reusable UI components
│   │   │   ├── ui/                 # Shadcn/UI primitives
│   │   │   ├── auth/               # Protected routes
│   │   │   ├── dashboard/          # Chart components
│   │   │   └── common/             # Shared components
│   │   ├── services/               # 14 API service modules
│   │   ├── contexts/               # AuthContext (Cognito)
│   │   ├── hooks/                  # Custom React hooks
│   │   ├── layouts/                # AppLayout, SuperAdminLayout
│   │   └── theme/                  # Ant Design theme config
│   ├── Dockerfile / Dockerfile.dev
│   └── nginx.conf                  # Production Nginx config
│
├── 📂 invoice_ocr/                 # Python AI/OCR Service
│   ├── src/
│   │   ├── run_ocr.py              # Main OCR orchestrator
│   │   ├── invoice_schema.py       # Invoice data schema (~20 KB)
│   │   ├── preprocessing.py        # Image preprocessing
│   │   ├── engine/                 # InvoiceExtractionEngine
│   │   │   ├── __init__.py         # Pipeline orchestrator
│   │   │   ├── conflict_resolver   # Multi-engine result merging
│   │   │   ├── table_row_rebuilder # Line-item reconstruction
│   │   │   └── validator.py        # Field validation
│   │   ├── normalizers/            # Data normalization
│   │   │   ├── date_normalizer.py  # Vietnamese date parsing
│   │   │   ├── money_normalizer.py # Currency formatting
│   │   │   └── tax_normalizer.py   # Tax code validation
│   │   ├── validators/             # Business rule validators
│   │   ├── rules/                  # Field extraction rules
│   │   └── schema/                 # LayoutLMv3 label mappings
│   ├── Dockerfile                  # Production image
│   └── requirements.txt            # Python dependencies
│
├── 📂 docs/                        # Technical documentation
│   ├── AWS_ARCHITECTURE_FINAL.md   # Full AWS architecture (765 lines)
│   ├── AWS_DEPLOYMENT_GUIDE_V2.md  # Step-by-step deployment guide
│   ├── SRS_DOCUMENTATION.md        # Software Requirements Specification
│   └── VIETQR_SYSTEM_FLOW.md      # VietQR integration details
│
├── 📂 .github/workflows/          # CI/CD Pipelines
│   ├── deploy-backend.yml          # Backend → ECR → Elastic Beanstalk
│   └── deploy-ocr.yml             # OCR → ECR → ECS Fargate
│
├── docker-compose.yml              # Local development (4 services)
├── Dockerrun.aws.json              # EB Docker deployment descriptor
├── SmartInvoiceShield.sln          # .NET Solution file
└── inject.js                       # Browser extension utility
```

---

## 🔐 Security Model

SmartInvoice Shield implements a **5-layer security architecture**:

| Layer | Mechanism | Details |
|-------|-----------|---------|
| **1. Edge** | HTTPS Everywhere | CloudFront HTTP→HTTPS redirect, ACM certificates |
| **2. Auth** | Cognito JWT + RBAC | 5 roles, 14 granular permissions, Custom Claims Transformer |
| **3. Network** | VPC Isolation | All workloads in Private Subnets, 4 Security Groups (least privilege) |
| **4. Data** | Encryption at Rest | S3 AES-256 SSE, RDS encryption, SSM SecureString for secrets |
| **5. App** | Middleware Guards | CORS policy (Amplify origin only), Maintenance mode, Tenant status checks |

### RBAC Permissions Matrix

```
system:view · system:manage · company:view · company:manage
blacklist:view · blacklist:manage · user:view · user:manage
invoice:view · invoice:upload · invoice:edit · invoice:approve
invoice:reject · invoice:override_risk · report:export
```

---

## 💻 Local Development

### Prerequisites

| Tool | Version | Required |
|------|---------|----------|
| **Docker & Docker Compose** | Latest | ✅ |
| **.NET 9 SDK** | 9.0+ | Optional (if running without Docker) |
| **Node.js** | 18+ | Optional (for frontend dev outside Docker) |
| **Python** | 3.10+ | Optional (for OCR dev outside Docker) |

### Quick Start

1. **Clone the repository**

   ```bash
   git clone https://github.com/tuankiet18-dev/SMARTINVOICE-SHIELD.git
   cd SMARTINVOICE-SHIELD
   ```

2. **Configure environment variables**

   Create a `.env` file in `SmartInvoice.API/`:

   ```env
   # AWS Credentials
   AWS_ACCESS_KEY_ID=your_access_key
   AWS_SECRET_ACCESS_KEY=your_secret_key
   AWS_REGION=ap-southeast-1

   # AWS Cognito
   COGNITO_USER_POOL_ID=ap-southeast-1_XXXXXXX
   COGNITO_CLIENT_ID=your_client_id
   COGNITO_CLIENT_SECRET=your_client_secret

   # AWS SQS
   AWS_SQS_OCR_URL=https://sqs.ap-southeast-1.amazonaws.com/XXXX/smartinvoice-ocr-queue
   AWS_SQS_URL=https://sqs.ap-southeast-1.amazonaws.com/XXXX/smartinvoice-vietqr-queue

   # AWS S3
   AWS_S3_BUCKET_NAME=your-s3-bucket-name
   ```

3. **Launch all services**

   ```bash
   docker-compose up -d --build
   ```

   This spins up **4 containers**:

   | Service | Container | Port |
   |---------|-----------|------|
   | Backend (.NET 9 API) | `smartinvoice-backend` | `localhost:5172` |
   | Frontend (React) | `smartinvoice-frontend` | `localhost:3000` |
   | OCR Service (Python) | `smartinvoice-ocr` | `localhost:5000` |
   | PostgreSQL 16 | `smartinvoice-db` | `localhost:5433` |

4. **Access the application**

   | Service | URL |
   |---------|-----|
   | 🖥️ **Frontend** | [http://localhost:3000](http://localhost:3000) |
   | 📖 **Swagger API Docs** | [http://localhost:5172/swagger](http://localhost:5172/swagger) |
   | ❤️ **Health Check** | [http://localhost:5172/health](http://localhost:5172/health) |

> **Note**: The database is automatically migrated on startup with Polly retry (5 attempts, 3s intervals). Initial `DocumentType` seed data (GTGT, SALE) is also auto-populated.

---

## ☁️ Cloud Deployment

### CI/CD Pipelines

The project uses **GitHub Actions** for fully automated deployments triggered on push to `main`:

```
┌─────────────────────────────────────────────────────────────────┐
│                    Push to main branch                          │
├──────────────────┬──────────────────┬───────────────────────────┤
│ Backend Pipeline │  OCR Pipeline    │  Frontend Pipeline        │
│ (deploy-backend) │  (deploy-ocr)    │  (Amplify auto-build)     │
│                  │                  │                           │
│ Build .NET image │ Build Python img │ Vite build → dist/        │
│ Push to ECR      │ Push to ECR      │ Auto-deploy CDN           │
│ Deploy to EBS    │ Update ECS Svc   │                           │
└──────────────────┴──────────────────┴───────────────────────────┘
```

| Pipeline | Trigger Path | Target |
|----------|-------------|--------|
| **Backend** | `SmartInvoice.API/**` | ECR → Elastic Beanstalk |
| **OCR** | `invoice_ocr/**` | ECR → ECS Fargate (force new deployment) |
| **Frontend** | `SmartInvoice.Frontend/**` | AWS Amplify (auto-detect) |

### Required GitHub Secrets

| Secret | Description |
|--------|-------------|
| `AWS_ACCESS_KEY_ID` | IAM Access Key for deployment |
| `AWS_SECRET_ACCESS_KEY` | IAM Secret Key |
| `AWS_REGION` | Target region (`ap-southeast-1`) |
| `AWS_ACCOUNT_ID` | 12-digit AWS Account ID |

### AWS Services Summary

| Service | Purpose | Configuration |
|---------|---------|---------------|
| **Elastic Beanstalk** | Backend runtime | Docker on EC2, Auto Scaling (2-4 `t3.micro`) |
| **ECS Fargate** | OCR service | 2 tasks, 2 vCPU / 4 GB RAM each |
| **RDS** | Database | PostgreSQL 16, Multi-AZ, `db.t3.micro` |
| **S3** | File storage | AES-256 SSE, Lifecycle → Glacier after 90 days |
| **SQS** | Message queues | 2 Standard Queues (OCR + VietQR) |
| **Cognito** | Authentication | User Pool, JWT, custom attributes |
| **CloudFront** | HTTPS proxy | CachingDisabled, CORS-With-Preflight |
| **Amplify** | Frontend hosting | Auto-build, built-in CDN |
| **Cloud Map** | Service discovery | Private DNS `ocr.smartinvoice.local` |
| **SSM** | Secrets management | Parameter Store (Standard + SecureString) |
| **ECR** | Container images | `smartinvoice-backend`, `smartinvoice-ocr` |

### Estimated Monthly Cost

| Service | Cost (USD) |
|---------|:----------:|
| EC2 (2× t3.micro) | ~$15 |
| RDS Multi-AZ | ~$28 |
| ECS Fargate (2 tasks) | ~$20 |
| NAT Gateway | ~$35 |
| ALB | ~$18 |
| S3 + CloudFront | ~$2 |
| SQS, SSM, Cognito | ~$0 (Free Tier) |
| **Total** | **~$118** |

---

## 📚 Documentation

Detailed technical documentation is available in the [`docs/`](./docs) directory:

| Document | Description |
|----------|-------------|
| [AWS Architecture](./docs/AWS_ARCHITECTURE_FINAL.md) | Complete AWS infrastructure design (VPC, subnets, security groups, IAM roles) |
| [Deployment Guide](./docs/AWS_DEPLOYMENT_GUIDE_V2.md) | Step-by-step AWS deployment instructions |
| [SRS Document](./docs/SRS_DOCUMENTATION.md) | Software Requirements Specification (IEEE 830-1998 based) |
| [VietQR System Flow](./docs/VIETQR_SYSTEM_FLOW.md) | VietQR integration architecture and data flow |

---

## 🗄️ Database Schema

The system uses **PostgreSQL 16** with **15 tables** managed via EF Core Code-First Migrations:

```
┌─────────────────┐    ┌──────────────┐    ┌─────────────────────┐
│     Users        │───→│  Companies   │←───│ SubscriptionPackages│
└────────┬────────┘    └──────┬───────┘    └─────────────────────┘
         │                    │
         │    ┌───────────────▼────────────────┐
         │    │           Invoices              │
         │    │  (ExtractedData JSON, RiskLevel)│
         │    └──┬──────────┬──────────┬───────┘
         │       │          │          │
         │  ┌────▼────┐ ┌───▼───┐ ┌───▼──────────────┐
         │  │AuditLogs│ │ Files │ │InvoiceCheckResults│
         │  └─────────┘ └───────┘ └──────────────────┘
         │
    ┌────▼────────────┐  ┌──────────────────┐  ┌────────────────┐
    │PaymentTransactions│  │  Notifications   │  │ SystemConfigs  │
    └─────────────────┘  └──────────────────┘  └────────────────┘
    
    ┌───────────────────────┐  ┌──────────────┐  ┌──────────────┐
    │LocalBlacklistedCompanies│  │DocumentTypes │  │ExportConfigs │
    └───────────────────────┘  └──────────────┘  └──────────────┘
    
    ┌──────────────────┐  ┌──────────────┐
    │AIProcessingLogs  │  │ExportHistories│
    └──────────────────┘  └──────────────┘
```

---

## 🧪 Testing

### Backend
```bash
# Run from SmartInvoice.API directory
dotnet test
```

### Frontend
```bash
# Run from SmartInvoice.Frontend directory
npm run test          # Single run (Vitest)
npm run test:watch    # Watch mode
```

---

## 📜 Legal Compliance

SmartInvoice Shield is designed to comply with Vietnamese e-invoice regulations:

- **Circular 78/2021/TT-BTC** — Electronic invoice issuance and management
- **Decision 78/QĐ-TCT** — Tax risk assessment criteria
- XML schema validation against GDT standards (`<DLHDon>`, `<TTChung>`, `<NBan>`, `<NMua>`, `<DSHHDVu>`, `<TToan>`, `<DSCKS>`)
- Digital signature verification for invoice authenticity

---

## 👥 Team

This project is developed and maintained by the **SmartInvoice Shield Team**:

| Member | GitHub |
|--------|--------|
| Tuấn Kiệt | [@tuankiet18-dev](https://github.com/tuankiet18-dev) |
| Nhật Anh | [@nhatanh-dev](https://github.com/nhatanh-dev) |
| Philipsgn | [@philipsgn](https://github.com/philipsgn) |
| QuanPM77 | [@QuanPM77](https://github.com/QuanPM77) |

---

## 📄 License

This project is proprietary software. All rights reserved.

---

<p align="center">
  <sub>Built with ❤️ using .NET 9, React, Python, and AWS</sub>
</p>
