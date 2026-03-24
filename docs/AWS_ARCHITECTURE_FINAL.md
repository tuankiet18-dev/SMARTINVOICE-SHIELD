# TÀI LIỆU KIẾN TRÚC AWS TỔNG THỂ - SMARTINVOICE SHIELD

**Dự án:** Smart Invoice - Phần mềm quản lý và rà soát rủi ro hóa đơn
**Mục tiêu thiết kế:** Đảm bảo hệ thống vận hành trơn tru cho luồng xử lý hóa đơn khối lượng lớn (tích hợp AI), tuân thủ nghiêm ngặt 3 tiêu chí: **Tối ưu chi phí (Cost-Optimization)**, **Đa vùng sẵn sàng (Multi-AZ)**, và **Bảo mật lưu trữ dữ liệu (Security)**.

**Công nghệ cốt lõi:** React TS + Vite (Frontend), .NET 9 (Backend API), Python/PaddleOCR/VietOCR/Gemini (AI OCR), PostgreSQL (Database).

---

## 1. SƠ ĐỒ KIẾN TRÚC TỔNG THỂ (EVENT-DRIVEN ARCHITECTURE)

```mermaid
graph TB
    subgraph INTERNET["🌐 INTERNET"]
        USER["👤 Người dùng / Kế toán"]
        VNPAY_GW["💳 VNPay Gateway"]
        VIETQR_API["🏛️ VietQR API<br/>(Tra cứu MST)"]
    end

    subgraph EDGE["Tầng Edge & DNS"]
        R53["Route 53<br/>(DNS Management)"]
        ACM["ACM<br/>(SSL/TLS Certificates)"]
    end

    subgraph FRONTEND["Tầng Frontend"]
        AMPLIFY["AWS Amplify<br/>(React TS + Vite SPA)<br/>Built-in CDN"]
    end

    subgraph AUTH["Tầng Xác Thực"]
        COGNITO["Amazon Cognito<br/>(User Pool + JWT)"]
    end

    subgraph BACKEND["Tầng Backend - Public Subnet"]
        ALB["Application Load Balancer<br/>(Multi-AZ)"]
        subgraph EB["Elastic Beanstalk"]
            EC2_1["EC2 t3.micro<br/>AZ-1a<br/>.NET 9 API (Public IPs)"]
            EC2_2["EC2 t3.micro<br/>AZ-1b<br/>.NET 9 API (Public IPs)"]
        end
    end

    subgraph ASYNC["Tầng Xử Lý - Public Subnet"]
        SQS_OCR["SQS Queue<br/>(OCR Jobs)"]
        SQS_VIETQR["SQS Queue<br/>(VietQR Validation)"]
        subgraph FARGATE["ECS Fargate Spot"]
            CONTAINER["🐳 Python OCR Container<br/>(Auto-assign Public IP)"]
        end
    end

    subgraph STORAGE["Tầng Lưu Trữ - Private Subnet"]
        S3["S3 Bucket<br/>(AES-256 SSE)<br/>invoices/, exports/, raw/"]
        S3_GLACIER["S3 Glacier<br/>Instant Retrieval<br/>(> 90 ngày)"]
        RDS["RDS PostgreSQL<br/>db.t3.micro<br/>(Multi-AZ Standby)"]
    end

    subgraph SECRETS["Tầng Bảo Mật"]
        SSM["SSM Parameter Store<br/>(Connection Strings,<br/>JWT Secret, API Keys)"]
    end

    subgraph MONITOR["Tầng Giám Sát"]
        CW["CloudWatch<br/>(Logs + Alarms)"]
        SNS["SNS<br/>(Email Alerts)"]
        BUDGETS["AWS Budgets<br/>(Billing Alarm)"]
    end

    USER -->|HTTPS| R53
    R53 --> ACM
    ACM --> AMPLIFY
    ACM --> ALB
    AMPLIFY -->|API Calls| ALB
    USER -->|Login/Register| COGNITO
    COGNITO -->|JWT Token| ALB
    ALB --> EC2_1
    ALB --> EC2_2

    EC2_1 & EC2_2 -->|Upload files| S3
    EC2_1 & EC2_2 -->|CRUD| RDS
    EC2_1 & EC2_2 -->|Push OCR job| SQS_OCR
    EC2_1 & EC2_2 -->|Push VietQR job| SQS_VIETQR
    EC2_1 & EC2_2 -->|Read secrets| SSM
    EC2_1 & EC2_2 -->|Payment URL| VNPAY_GW

    SQS_OCR -->|Poll messages| CONTAINER
    CONTAINER -->|Download image| S3
    CONTAINER -->|Update results| RDS
    CONTAINER -->|Trigger VietQR| SQS_VIETQR

    SQS_VIETQR -->|Poll messages| EC2_1
    SQS_VIETQR -->|Poll messages| EC2_2
    EC2_1 & EC2_2 -->|Validate MST| VIETQR_API

    S3 -->|Lifecycle Policy| S3_GLACIER

    EC2_1 & EC2_2 --> CW
    CONTAINER --> CW
    CW -->|Alarm trigger| SNS
    BUDGETS -->|Cost alert| SNS

    classDef edge fill:#1a73e8,stroke:#1557b0,color:white
    classDef frontend fill:#34a853,stroke:#2d8f47,color:white
    classDef auth fill:#ea4335,stroke:#c5221f,color:white
    classDef backend fill:#fbbc04,stroke:#d4a003,color:black
    classDef async fill:#9c27b0,stroke:#7b1fa2,color:white
    classDef storage fill:#ff6d00,stroke:#e65100,color:white
    classDef monitor fill:#607d8b,stroke:#455a64,color:white
    classDef external fill:#78909c,stroke:#546e7a,color:white

    class R53,ACM edge
    class AMPLIFY frontend
    class COGNITO auth
    class ALB,EC2_1,EC2_2 backend
    class SQS_OCR,SQS_VIETQR,CONTAINER async
    class S3,S3_GLACIER,RDS storage
    class CW,SNS,BUDGETS monitor
    class VNPAY_GW,VIETQR_API external
```

---

## 2. LUỒNG XỬ LÝ HÓA ĐƠN CHI TIẾT (SEQUENCE DIAGRAM)

```mermaid
sequenceDiagram
    autonumber
    participant U as 👤 Người dùng
    participant FE as React SPA (Amplify)
    participant CG as Amazon Cognito
    participant API as .NET 9 API (EB)
    participant S3 as Amazon S3
    participant SQS1 as SQS OCR Queue
    participant OCR as Fargate Spot (Python OCR)
    participant DB as RDS PostgreSQL
    participant SQS2 as SQS VietQR Queue
    participant VQR as VietQR API

    Note over U,VQR: ═══ PHASE 1: UPLOAD & QUEUE ═══

    U->>FE: Chọn file hóa đơn (PDF/PNG/JPG)
    FE->>CG: Xác thực JWT Token
    CG-->>FE: Token hợp lệ ✅
    FE->>API: POST /api/invoices/upload (multipart)
    API->>S3: Upload file (AES-256 SSE)
    S3-->>API: S3 Key
    API->>DB: Tạo Invoice (Status: Processing)
    API->>SQS1: Gửi OcrJobMessage {invoiceId, s3Key}
    API-->>FE: 202 Accepted — "Đang xử lý"
    FE-->>U: Hiển thị trạng thái "Đang xử lý"

    Note over U,VQR: ═══ PHASE 2: AI OCR EXTRACTION ═══

    SQS1->>OCR: Long-poll nhận job (20s wait)
    OCR->>S3: Download ảnh hóa đơn
    S3-->>OCR: Image bytes

    alt Gemini Mode (AI extraction)
        OCR->>OCR: Gọi Gemini API trích xuất dữ liệu
    else PaddleOCR + VietOCR Mode
        OCR->>OCR: PaddleOCR detect text regions
        OCR->>OCR: VietOCR recognize Vietnamese text
        OCR->>OCR: InvoiceExtractionEngine xử lý fields
    end

    OCR->>DB: Cập nhật Invoice (ExtractedData, Status: Draft)
    OCR->>SQS1: Xóa message khỏi queue ✅

    Note over U,VQR: ═══ PHASE 3: VIETQR TAX VALIDATION ═══

    OCR->>SQS2: Publish VietQrValidationMessage {taxCode}
    SQS2->>API: Background Worker poll message
    API->>VQR: GET /v2/business/{taxCode}
    VQR-->>API: Company info + status
    API->>DB: Cập nhật RiskLevel & Notes
    API->>SQS2: Xóa message ✅

    Note over U,VQR: ═══ PHASE 4: USER REVIEW ═══

    U->>FE: Refresh danh sách hóa đơn
    FE->>API: GET /api/invoices
    API->>DB: Query invoices
    DB-->>API: List<Invoice>
    API-->>FE: Dữ liệu hóa đơn đã trích xuất
    FE-->>U: Hiển thị kết quả + RiskLevel (Green/Yellow/Red)
```

---

## 3. CHI TIẾT CÁC TẦNG DỊCH VỤ (SERVICE BREAKDOWN)

### A. Tầng Giao Diện & Điều Hướng (Frontend & Edge)

| Dịch vụ | Vai trò | Chi tiết |
|---------|---------|----------|
| **AWS Amplify** | Hosting SPA | React TS + Vite, tự động phân phối qua CDN biên toàn cầu |
| **Route 53** | DNS | Quản lý domain tùy chỉnh, health check routing |
| **ACM** | SSL/TLS | Cấp chứng chỉ HTTPS tự động, zero-downtime renewal |

### B. Tầng Xác Thực & Bảo Mật (Auth & Security)

| Dịch vụ | Vai trò | Chi tiết |
|---------|---------|----------|
| **Amazon Cognito** | Identity Provider | SignUp/Login/VerifyEmail, JWT phát hành, custom attributes (`company_id`, `role`) |
| **SSM Parameter Store** | Quản lý bí mật | Connection strings, Cognito secrets, VnPay keys, SQS URLs |
| **VPC + Security Groups** | Mạng riêng | RDS trong Private Subnet. ALB, Backend, Fargate ở Public Subnet (giảm chi phí NAT) |

### C. Tầng Ứng Dụng Cốt Lõi (Core Backend)

| Dịch vụ | Vai trò | Chi tiết |
|---------|---------|----------|
| **Elastic Beanstalk** | Runtime | .NET 9 Web API, 14 Controllers, auto-deploy |
| **ALB** | Load Balancer | Phân phối traffic, health check, SSL termination |
| **Auto Scaling** | Khả dụng cao | Min 2× `t3.micro` rải đều 2 AZ, scale theo CPU |

**14 API Controllers đã triển khai:**

```mermaid
mindmap
  root((.NET 9 API))
    Auth
      AuthController
        Login / Register / VerifyEmail
        Cognito Integration
    Invoice Management
      InvoicesController
        Upload XML/PDF/Image
        CRUD & Search & Filter
      ValidationController
        Business Logic Validation
        Database Constraints Check
    User & Company
      UsersController
        CRUD Users
        Permission Management
      SettingsController
        Company Settings
    Finance
      PaymentController
        VnPay Integration
      SubscriptionPackagesController
        CRUD Packages
    Reports & Monitoring
      DashboardController
        Statistics & Metrics
      ExportsController
        Excel/PDF Export
      AuditLogController
        Audit Trail
      NotificationsController
        In-app Notifications
    Admin
      SystemConfigController
        Dynamic Configuration
      BlacklistController
        Global/Local Blacklist
      ExportConfigController
        Export Templates
```

### D. Tầng Xử Lý Bất Đồng Bộ (Event-Driven Async)

```mermaid
graph LR
    subgraph SQS_QUEUES["Amazon SQS (2 Queues)"]
        Q1["📦 smartinvoice-ocr-queue<br/>(OCR Jobs)"]
        Q2["📦 smartinvoice-vietqr-queue<br/>(Tax Validation)"]
    end

    subgraph PRODUCERS["Producers"]
        API1["InvoicesController<br/>(Upload endpoint)"]
        OCR_W["OcrWorkerService<br/>(Step 7/7)"]
    end

    subgraph CONSUMERS["Consumers (Background Services)"]
        C1["OcrWorkerService<br/>(BackgroundService)<br/>Long-poll 20s, batch 5"]
        C2["VietQrSqsConsumerService<br/>(BackgroundService)<br/>Long-poll 20s, batch 10"]
    end

    API1 -->|OcrJobMessage| Q1
    OCR_W -->|VietQrValidationMessage| Q2
    Q1 -->|Poll| C1
    Q2 -->|Poll| C2

    C1 -->|"7-step pipeline"| DB[(RDS)]
    C2 -->|"Update RiskLevel"| DB

    style Q1 fill:#9c27b0,color:white
    style Q2 fill:#9c27b0,color:white
```

| Thành phần | Chi tiết kỹ thuật |
|-----------|-------------------|
| **SQS OCR Queue** | Long-polling 20s, batch 5 messages, visibility timeout auto-retry |
| **SQS VietQR Queue** | Long-polling 20s, batch 10 messages, Polly retry (3×, exponential backoff) + Circuit Breaker (5 failures → 1 min break) |
| **OcrWorkerService** | 7-step pipeline: Download S3 → Call OCR API → Validate Logic → Extract Data → Create FileStorage → Update DB → Publish VietQR |
| **VietQrSqsConsumerService** | DI scope isolation per message, tax code validation, risk level escalation |
| **ECS Fargate Spot** | `python:3.10-slim`, CPU mode, Pay-As-You-Go, Scale-to-Zero khi idle |

### E. Tầng Lưu Trữ & Cơ Sở Dữ Liệu (Storage & Database)

```mermaid
graph LR
    subgraph S3_BUCKET["Amazon S3 (AES-256 SSE)"]
        RAW["📁 raw/<br/>Presigned Upload files"]
        INV["📁 invoices/{companyId}/{yyyy-MM}/<br/>OCR Images"]
        EXP["📁 exports/{companyId}/{yyyy-MM}/<br/>Excel/PDF Reports"]
    end

    subgraph LIFECYCLE["S3 Lifecycle Policy"]
        STD["Standard Storage<br/>(0-90 ngày)"]
        GLA["Glacier Instant Retrieval<br/>(> 90 ngày)"]
    end

    subgraph RDS_PG["RDS PostgreSQL (Multi-AZ)"]
        T1["Users"]
        T2["Companies"]
        T3["Invoices"]
        T4["InvoiceCheckResults"]
        T5["InvoiceAuditLogs"]
        T6["FileStorages"]
        T7["PaymentTransactions"]
        T8["SubscriptionPackages"]
        T9["SystemConfigurations"]
        T10["Notifications"]
        T11["LocalBlacklistedCompanies"]
        T12["DocumentTypes"]
        T13["ExportHistories"]
        T14["ExportConfigs"]
        T15["AIProcessingLogs"]
    end

    STD -->|"Auto-transition"| GLA

    style S3_BUCKET fill:#ff6d00,color:white
    style RDS_PG fill:#1565c0,color:white
```

| Dịch vụ | Chi tiết |
|---------|----------|
| **Amazon S3** | Mã hóa AES-256 (SSE), Presigned URLs (15-60 phút), 3 folder tổ chức theo companyId/date |
| **S3 Lifecycle** | Standard → Glacier Instant Retrieval sau 90 ngày |
| **RDS PostgreSQL** | Instance `db.t3.micro`, Automated Backups, PITR, 15 tables chính |

### F. Tầng Giám Sát & Quản Lý Chi Phí (Monitor & Governance)

| Dịch vụ | Vai trò |
|---------|---------|
| **CloudWatch** | Logs tập trung, Alarms (HTTP 500, CPU > threshold, SQS queue depth) |
| **SNS** | Email cảnh báo tự động cho team khi alarm trigger |
| **AWS Budgets** | Cảnh báo khẩn cấp khi chi phí vượt ngân sách cho phép |

---

## 4. TÍCH HỢP BÊN NGOÀI (EXTERNAL INTEGRATIONS)

```mermaid
graph TB
    subgraph API[".NET 9 Backend"]
        VNPAY_SVC["VnPayService"]
        VIETQR_SVC["VietQrClientService"]
        AUTH_SVC["AuthService"]
        OCR_SVC["OcrClientService"]
    end

    subgraph EXTERNAL["Dịch Vụ Bên Ngoài"]
        VNPAY["💳 VNPay<br/>Payment Gateway<br/>HMAC-SHA512 signing"]
        VIETQR["🏛️ VietQR API<br/>api.vietqr.io/v2/business<br/>Tax code validation"]
        GEMINI["🤖 Google Gemini<br/>AI Data Extraction"]
    end

    subgraph RESILIENCE["Polly Resilience Policies"]
        RETRY["🔄 Retry<br/>3 attempts<br/>Exponential backoff<br/>(1s, 2s, 4s)"]
        CB["⚡ Circuit Breaker<br/>5 failures → Open<br/>1 min cooldown"]
        TIMEOUT["⏱️ Timeout<br/>5s per request"]
    end

    VNPAY_SVC -->|"Build payment URL"| VNPAY
    VIETQR_SVC --> RETRY --> VIETQR
    VIETQR_SVC --> CB
    VIETQR_SVC --> TIMEOUT
    AUTH_SVC -->|"Tax code check"| VIETQR
    OCR_SVC -->|"Invoice extraction"| GEMINI

    style VNPAY fill:#00897b,color:white
    style VIETQR fill:#5c6bc0,color:white
    style GEMINI fill:#e91e63,color:white
```

---

## 5. SƠ ĐỒ BẢO MẬT NHIỀU TẦNG (SECURITY LAYERS)

```mermaid
graph TB
    subgraph L1["Layer 1: Edge Security"]
        HTTPS["HTTPS Everywhere<br/>(ACM Certificates)"]
        CDN["CDN + DDoS Protection<br/>(Amplify Built-in)"]
    end

    subgraph L2["Layer 2: Authentication"]
        JWT["Cognito JWT Tokens<br/>(Access + ID + Refresh)"]
        CLAIMS["Custom Claims Transformer<br/>(Role + Permissions → Claims)"]
        RBAC["Role-Based Access Control<br/>(SuperAdmin, CompanyAdmin,<br/>Accountant, Viewer)"]
    end

    subgraph L3["Layer 3: Network"]
        VPC_L["VPC Isolation"]
        SG["Security Groups<br/>(Strict Inbound Rules)"]
        PRIV["Private Subnets<br/>(Only RDS is fully private)"]
    end

    subgraph L4["Layer 4: Data"]
        S3_ENC["S3 AES-256 SSE"]
        RDS_ENC["RDS Encryption at Rest"]
        SSM_L["SSM Parameter Store<br/>(No secrets in code)"]
        PRESIGN["Presigned URLs<br/>(Time-limited access)"]
    end

    subgraph L5["Layer 5: Application"]
        MAINT["Maintenance Middleware"]
        CORS_L["CORS Policy<br/>(AllowAmplify origin)"]
        PERM["Permission-based Policies<br/>(14 granular permissions)"]
    end

    L1 --> L2 --> L3 --> L4 --> L5

    style L1 fill:#1a73e8,color:white
    style L2 fill:#ea4335,color:white
    style L3 fill:#34a853,color:white
    style L4 fill:#fbbc04,color:black
    style L5 fill:#9c27b0,color:white
```

---

## 6. LUỒNG XỬ LÝ HÓA ĐƠN TIÊU CHUẨN (HAPPY PATH)

| Bước | Hành động | Dịch vụ AWS |
|------|-----------|-------------|
| **1. Upload** | Người dùng xác thực qua Cognito, chọn file (PDF/PNG) tải lên qua Amplify | Cognito, Amplify |
| **2. Lưu trữ thô** | Request mã hóa HTTPS qua ALB → Backend API lưu file vào S3 (AES-256) | ALB, S3 |
| **3. Tạo Queue** | Backend tạo OcrJobMessage chứa S3 Key, đẩy vào SQS, phản hồi ngay "Đang xử lý" | SQS |
| **4. AI OCR** | Fargate Spot scale-up, kéo file từ S3, chạy PaddleOCR+VietOCR hoặc Gemini | ECS Fargate, S3 |
| **5. Validation** | OCR Worker validate business logic (trùng lặp, chủ sở hữu), tạo CheckResult | RDS |
| **6. VietQR** | Publish VietQR message → Background worker gọi API xác thực MST → cập nhật RiskLevel | SQS, VietQR API |
| **7. Kết quả** | Frontend poll hoặc refresh, hiển thị dữ liệu trích xuất + mức rủi ro (Green/Yellow/Red) | Amplify, RDS |

---

## 7. CHIẾN LƯỢC TỐI ƯU CHI PHÍ (COST OPTIMIZATION)

| Chiến lược | Mô tả | Tiết kiệm ước tính |
|-----------|-------|---------------------|
| **Fargate Spot** | AI OCR chạy trên tài nguyên dư thừa AWS | ~70% so với On-Demand |
| **Scale-to-Zero** | Fargate tự tắt hoàn toàn khi không có job trong SQS | 100% khi idle |
| **SQS Long Polling** | Giảm số lượng API calls (20s wait thay vì short poll) | ~90% SQS API costs |
| **S3 Lifecycle** | Auto-transition sang Glacier sau 90 ngày | ~68% storage costs |
| **t3.micro** | Burstable instances cho workload không đều | Phù hợp startup |
| **SSM Parameter Store** | Free tier (Standard parameters) | $0 cho secrets |
| **Cognito** | Free tier 50,000 MAU | $0 cho auth |
| **Amplify Free Tier** | 1000 build minutes/tháng + 15GB hosting | $0 cho frontend |
