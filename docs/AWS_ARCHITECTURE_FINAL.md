# TÀI LIỆU KIẾN TRÚC AWS - SMARTINVOICE SHIELD

## MỤC LỤC

1. [Tổng quan hệ thống](#1-tổng-quan-hệ-thống)
2. [Sơ đồ kiến trúc tổng thể](#2-sơ-đồ-kiến-trúc-tổng-thể)
3. [Thiết kế mạng (VPC & Networking)](#3-thiết-kế-mạng-vpc--networking)
4. [Chi tiết các tầng dịch vụ](#4-chi-tiết-các-tầng-dịch-vụ)
5. [Luồng xử lý hóa đơn (Sequence Diagram)](#5-luồng-xử-lý-hóa-đơn-sequence-diagram)
6. [Mô hình bảo mật nhiều tầng](#6-mô-hình-bảo-mật-nhiều-tầng)
7. [Tích hợp bên ngoài & Resilience](#7-tích-hợp-bên-ngoài--resilience)
8. [CI/CD Pipeline](#8-cicd-pipeline)
9. [Giám sát & Cảnh báo](#9-giám-sát--cảnh-báo)
10. [Chiến lược tối ưu chi phí](#10-chiến-lược-tối-ưu-chi-phí)
11. [Ước tính chi phí hàng tháng](#11-ước-tính-chi-phí-hàng-tháng)

---

## 1. TỔNG QUAN HỆ THỐNG

### 1.1. Mục tiêu thiết kế

| Tiêu chí                               | Mô tả                                                                                            |
| -------------------------------------- | ------------------------------------------------------------------------------------------------ |
| **Tối ưu chi phí (Cost-Optimization)** | Sử dụng Cloud Map thay ALB nội bộ, Free Tier tối đa, Fargate pay-as-you-go, S3 Lifecycle         |
| **Đa vùng sẵn sàng (Multi-AZ)**        | RDS Multi-AZ Standby, Backend rải 2 AZ, ALB health check tự động                                 |
| **Bảo mật (Security)**                 | Toàn bộ workload trong Private Subnet, NAT Gateway cho outbound, SSM Parameter Store, S3 AES-256 |
| **Khả năng mở rộng (Scalability)**     | Auto Scaling cho Backend (2-4 instances), ECS Fargate scale theo nhu cầu                         |

### 1.2. Công nghệ cốt lõi

| Tầng                  | Công nghệ                          | Dịch vụ AWS                       |
| --------------------- | ---------------------------------- | --------------------------------- |
| **Frontend**          | React TypeScript + Vite            | AWS Amplify (Built-in CDN)        |
| **Backend API**       | .NET 9 Web API                     | Elastic Beanstalk (Docker on EC2) |
| **AI / OCR**          | Python, PaddleOCR, VietOCR, Gemini | ECS Fargate                       |
| **Database**          | PostgreSQL 16.x                    | Amazon RDS (Multi-AZ)             |
| **Object Storage**    | —                                  | Amazon S3 (AES-256 SSE)           |
| **Message Queue**     | —                                  | Amazon SQS (Standard)             |
| **Authentication**    | JWT                                | Amazon Cognito (User Pool)        |
| **Service Discovery** | —                                  | AWS Cloud Map (Private DNS)       |
| **HTTPS Proxy**       | —                                  | Amazon CloudFront                 |

---

## 2. SƠ ĐỒ KIẾN TRÚC TỔNG THỂ

```mermaid
graph TB
    subgraph INTERNET["🌐 INTERNET"]
        USER["👤 Người dùng"]
        VNPAY_GW["💳 VNPay Gateway"]
        VIETQR_API["🏛️ VietQR API"]
    end

    subgraph AWS["☁️ AWS Cloud (ap-southeast-1)"]
        AMPLIFY["AWS Amplify<br/>React TS + Vite<br/>(Built-in CDN)"]
        CF["CloudFront<br/>HTTPS Proxy<br/>(CachingDisabled)"]
        COGNITO["Amazon Cognito<br/>User Pool + JWT"]

        subgraph VPC["VPC 10.0.0.0/16"]
            IGW["Internet Gateway"]

            subgraph AZ1["Availability Zone 1a"]
                subgraph PUB1["Public Subnet 10.0.1.0/24"]
                    ALB_1["ALB (Public)"]
                    NATGW["NAT Gateway<br/>+ Elastic IP"]
                end
                subgraph PRIV1["Private Subnet 10.0.3.0/24"]
                    EC2_1["EC2 t3.micro<br/>.NET 9 Backend"]
                    RDS_P["RDS Primary<br/>PostgreSQL 16.x"]
                    FARGATE_1["ECS Fargate<br/>Python OCR"]
                end
            end

            subgraph AZ2["Availability Zone 1b"]
                subgraph PUB2["Public Subnet 10.0.2.0/24"]
                    ALB_2["ALB (Public)"]
                end
                subgraph PRIV2["Private Subnet 10.0.4.0/24"]
                    EC2_2["EC2 t3.micro<br/>.NET 9 Backend"]
                    RDS_S["RDS Standby<br/>(Multi-AZ Sync)"]
                end
            end
        end

        S3["S3 Bucket<br/>(AES-256 SSE)<br/>smart-invoice-shield-storage"]
        SQS["SQS Queues<br/>(OCR + VietQR)"]
        SSM["SSM Parameter Store<br/>(Secrets & Config)"]
        ECR["ECR Registry<br/>(Docker Images)"]
        CLOUDMAP["Cloud Map<br/>ocr.smartinvoice.local<br/>(Private DNS)"]
    end

    USER -->|HTTPS| AMPLIFY
    USER -->|HTTPS| CF
    CF -->|HTTP:80| ALB_1 & ALB_2
    ALB_1 & ALB_2 --> EC2_1 & EC2_2
    USER -->|Login/Register| COGNITO
    COGNITO -->|JWT Token| CF

    EC2_1 & EC2_2 -->|Cloud Map DNS| CLOUDMAP
    CLOUDMAP -->|A Record| FARGATE_1

    EC2_1 & EC2_2 --> RDS_P
    EC2_1 & EC2_2 --> S3
    EC2_1 & EC2_2 -->|Push Job| SQS
    EC2_1 & EC2_2 -->|Read Config| SSM
    EC2_1 & EC2_2 -->|Payment URL| VNPAY_GW

    SQS -->|Long-poll| FARGATE_1
    FARGATE_1 --> S3
    FARGATE_1 -->|via Backend API| EC2_1

    EC2_1 & EC2_2 -->|Validate MST| VIETQR_API
    FARGATE_1 -->|Outbound via NAT| NATGW
    NATGW --> IGW

    RDS_P ---|Multi-AZ Sync| RDS_S

    classDef edge fill:#1a73e8,stroke:#1557b0,color:white
    classDef frontend fill:#34a853,stroke:#2d8f47,color:white
    classDef auth fill:#ea4335,stroke:#c5221f,color:white
    classDef backend fill:#fbbc04,stroke:#d4a003,color:black
    classDef async fill:#9c27b0,stroke:#7b1fa2,color:white
    classDef storage fill:#ff6d00,stroke:#e65100,color:white
    classDef monitor fill:#607d8b,stroke:#455a64,color:white
    classDef network fill:#00897b,stroke:#00695c,color:white

    class AMPLIFY frontend
    class COGNITO auth
    class ALB_1,ALB_2,EC2_1,EC2_2 backend
    class SQS,FARGATE_1 async
    class S3,RDS_P,RDS_S storage
    class CF,CLOUDMAP edge
    class NATGW,IGW network
```

---

## 3. THIẾT KẾ MẠNG (VPC & NETWORKING)

### 3.1. Tổng quan VPC

| Thành phần             | Giá trị                      |
| ---------------------- | ---------------------------- |
| **VPC Name**           | `smartinvoice-vpc`           |
| **CIDR Block**         | `10.0.0.0/16` (65,536 IPs)   |
| **Region**             | `ap-southeast-1` (Singapore) |
| **Availability Zones** | 2 AZs (`1a`, `1b`)           |
| **Subnets**            | 4 (2 Public + 2 Private)     |

### 3.2. Bảng Subnet chi tiết

| Subnet                    | CIDR          | AZ  | Loại        | Workload                              |
| ------------------------- | ------------- | --- | ----------- | ------------------------------------- |
| `smartinvoice-public-1a`  | `10.0.1.0/24` | 1a  | **Public**  | ALB, NAT Gateway                      |
| `smartinvoice-public-1b`  | `10.0.2.0/24` | 1b  | **Public**  | ALB                                   |
| `smartinvoice-private-1a` | `10.0.3.0/24` | 1a  | **Private** | EC2 Backend, RDS Primary, ECS Fargate |
| `smartinvoice-private-1b` | `10.0.4.0/24` | 1b  | **Private** | EC2 Backend, RDS Standby              |

### 3.3. Sơ đồ mạng chi tiết

```mermaid
graph TB
    subgraph VPC["VPC 10.0.0.0/16"]
        IGW["🌐 Internet Gateway<br/>smartinvoice-igw"]

        subgraph PUBLIC_RT["Public Route Table (smartinvoice-public-rt)"]
            direction LR
            RT_PUB["0.0.0.0/0 → IGW"]
        end

        subgraph PRIVATE_RT["Private Route Table (smartinvoice-private-rt)"]
            direction LR
            RT_PRIV["0.0.0.0/0 → NAT Gateway"]
        end

        subgraph AZ1["AZ: ap-southeast-1a"]
            subgraph PUB_1A["Public Subnet 10.0.1.0/24"]
                ALB_NODE1["ALB Endpoint"]
                NAT["NAT Gateway<br/>+ Elastic IP"]
            end
            subgraph PRIV_1A["Private Subnet 10.0.3.0/24"]
                BE1["EC2 Backend"]
                RDS1["RDS Primary"]
                ECS1["ECS Fargate OCR"]
            end
        end

        subgraph AZ2["AZ: ap-southeast-1b"]
            subgraph PUB_1B["Public Subnet 10.0.2.0/24"]
                ALB_NODE2["ALB Endpoint"]
            end
            subgraph PRIV_1B["Private Subnet 10.0.4.0/24"]
                BE2["EC2 Backend"]
                RDS2["RDS Standby"]
            end
        end
    end

    IGW --- PUB_1A & PUB_1B
    NAT --- PRIV_1A & PRIV_1B

    style PUB_1A fill:#c8e6c9,stroke:#2e7d32
    style PUB_1B fill:#c8e6c9,stroke:#2e7d32
    style PRIV_1A fill:#ffcdd2,stroke:#c62828
    style PRIV_1B fill:#ffcdd2,stroke:#c62828
```

### 3.4. Security Groups

| Security Group                | Inbound Rules                                 | Mô tả                                     |
| ----------------------------- | --------------------------------------------- | ----------------------------------------- |
| **`smartinvoice-alb-sg`**     | TCP 80, 443 từ `0.0.0.0/0`                    | ALB tiếp nhận traffic từ Internet         |
| **`smartinvoice-backend-sg`** | TCP 80, 8080 từ `alb-sg`; All Traffic từ Self | EC2 Backend chỉ nhận traffic từ ALB       |
| **`smartinvoice-rds-sg`**     | TCP 5432 từ `backend-sg`, `ocr-sg`            | RDS chỉ cho phép Backend và OCR kết nối   |
| **`smartinvoice-ocr-sg`**     | TCP 5000 từ `backend-sg`                      | OCR Container chỉ nhận request từ Backend |

> **Tất cả Security Groups**: Outbound = All traffic → `0.0.0.0/0`

### 3.5. Route Tables

| Route Table               | Routes                         | Associated Subnets         |
| ------------------------- | ------------------------------ | -------------------------- |
| `smartinvoice-public-rt`  | `0.0.0.0/0` → Internet Gateway | `public-1a`, `public-1b`   |
| `smartinvoice-private-rt` | `0.0.0.0/0` → NAT Gateway      | `private-1a`, `private-1b` |

---

## 4. CHI TIẾT CÁC TẦNG DỊCH VỤ

### 4.1. Tầng Edge & CDN

| Dịch vụ               | Vai trò                       | Cấu hình                                                                                                                                                                             |
| --------------------- | ----------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **AWS Amplify**       | Hosting SPA (React TS + Vite) | Auto-build từ GitHub `main` branch, Built-in CDN toàn cầu                                                                                                                            |
| **Amazon CloudFront** | HTTPS Proxy cho Backend API   | Origin: EB ALB (HTTP:80), Viewer: Redirect HTTP→HTTPS, Cache: `CachingDisabled`, Origin Request: `AllViewerExceptHostHeader`, Response: `CORS-With-Preflight`, Rate limiting enabled |

### 4.2. Tầng Xác Thực

| Dịch vụ            | Vai trò           | Cấu hình                                                                                                                                             |
| ------------------ | ----------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Amazon Cognito** | Identity Provider | User Pool, Email sign-in, Self-registration, Custom attributes (`company_id`, `role`), `ALLOW_USER_PASSWORD_AUTH`, JWT Token (Access + ID + Refresh) |

### 4.3. Tầng Backend (Application)

| Dịch vụ               | Vai trò                | Cấu hình                                                                                                      |
| --------------------- | ---------------------- | ------------------------------------------------------------------------------------------------------------- |
| **Elastic Beanstalk** | .NET 9 Web API Runtime | Docker on 64bit Amazon Linux 2023, Single Instance → Auto Scaling (2-4 `t3.micro`), Private Subnet deployment |
| **ALB**               | Load Balancer          | Multi-AZ, Health check, Public Subnet, Traffic distribution                                                   |
| **Auto Scaling**      | High Availability      | Min: 2 instances, Max: 4 instances, rải đều 2 AZs                                                             |

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

### 4.4. Tầng AI / OCR (ECS Fargate)

| Thành phần            | Cấu hình                                                                      |
| --------------------- | ----------------------------------------------------------------------------- |
| **ECS Cluster**       | `smartinvoice-cluster` (Fargate only)                                         |
| **Task Definition**   | `smartinvoice-ocr-task`, Linux/X86_64, 2 vCPU, 4 GB RAM                       |
| **Service**           | `smartinvoice-ocr-task-service`, Desired: 2 tasks, Rolling update             |
| **Container**         | `ocr-container`, Port 5000, `DEVICE=cpu`, `HOST=0.0.0.0`                      |
| **Networking**        | Private Subnets, No Public IP, `smartinvoice-ocr-sg`                          |
| **Service Discovery** | Cloud Map namespace `smartinvoice.local`, Service `ocr`, A record, TTL 15-60s |
| **Task Role**         | `smartinvoice-ecs-task-role` (S3, SQS, SSM)                                   |
| **Execution Role**    | `ecsTaskExecutionRole` (ECR pull, CloudWatch logs)                            |
| **Logs**              | CloudWatch `awslogs` → `/ecs/smartinvoice-ocr-task`                           |

#### Giao tiếp Backend ↔ OCR (Cloud Map Service Discovery)

```mermaid
graph LR
    subgraph PRIVATE_NETWORK["Private Subnets (VPC)"]
        BE["EC2 Backend<br/>.NET 9 API"]
        CM["AWS Cloud Map<br/>smartinvoice.local"]
        OCR1["Fargate Task 1<br/>10.0.3.x:5000"]
        OCR2["Fargate Task 2<br/>10.0.4.x:5000"]
    end

    BE -->|"DNS: ocr.smartinvoice.local"| CM
    CM -->|"A Record → Private IP"| OCR1
    CM -->|"A Record → Private IP"| OCR2

    style CM fill:#1a73e8,color:white
    style BE fill:#fbbc04,color:black
    style OCR1 fill:#9c27b0,color:white
    style OCR2 fill:#9c27b0,color:white
```

> **Lý do chọn Cloud Map thay vì Internal ALB**: Tiết kiệm ~$18/tháng chi phí Load Balancer nội bộ. Backend gọi trực tiếp OCR qua DNS nội bộ `http://ocr.smartinvoice.local:5000`.

### 4.5. Tầng Xử Lý Bất Đồng Bộ (Event-Driven)

```mermaid
graph LR
    subgraph PRODUCERS["Producers"]
        API1["InvoicesController<br/>(Upload endpoint)"]
        OCR_W["OcrWorkerService<br/>(Step 7/7)"]
    end

    subgraph SQS_QUEUES["Amazon SQS (2 Standard Queues)"]
        Q1["📦 smartinvoice-ocr-queue<br/>Visibility: 450s<br/>Long-poll: 20s"]
        Q2["📦 smartinvoice-vietqr-queue<br/>Visibility: 30s<br/>Long-poll: 20s"]
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

| Thành phần                   | Chi tiết kỹ thuật                                                                                                             |
| ---------------------------- | ----------------------------------------------------------------------------------------------------------------------------- |
| **SQS OCR Queue**            | Standard, Visibility timeout 450s, Long-polling 20s, batch 5 messages                                                         |
| **SQS VietQR Queue**         | Standard, Visibility timeout 30s, Long-polling 20s, batch 10 messages                                                         |
| **OcrWorkerService**         | 7-step pipeline: Download S3 → Call OCR API → Validate Logic → Extract Data → Create FileStorage → Update DB → Publish VietQR |
| **VietQrSqsConsumerService** | DI scope isolation per message, Polly retry (3×, exponential backoff) + Circuit Breaker (5 failures → 1 min break)            |

### 4.6. Tầng Lưu Trữ & Database

```mermaid
graph LR
    subgraph S3_BUCKET["Amazon S3 — smart-invoice-shield-storage"]
        RAW["📁 raw/<br/>Presigned Upload files"]
        INV["📁 invoices/{companyId}/{yyyy-MM}/<br/>OCR Images"]
        EXP["📁 exports/{companyId}/{yyyy-MM}/<br/>Excel/PDF Reports"]
    end

    subgraph LIFECYCLE["S3 Lifecycle Policy"]
        STD["Standard Storage<br/>(0-90 ngày)"]
        GLA["Glacier Instant Retrieval<br/>(> 90 ngày)"]
    end

    subgraph RDS_PG["RDS PostgreSQL 16.x (Multi-AZ)"]
        T1["Users"] --- T2["Companies"]
        T3["Invoices"] --- T4["InvoiceCheckResults"]
        T5["InvoiceAuditLogs"] --- T6["FileStorages"]
        T7["PaymentTransactions"] --- T8["SubscriptionPackages"]
        T9["SystemConfigurations"] --- T10["Notifications"]
        T11["LocalBlacklistedCompanies"] --- T12["DocumentTypes"]
        T13["ExportHistories"] --- T14["ExportConfigs"]
        T15["AIProcessingLogs"]
    end

    STD -->|"Auto-transition"| GLA

    style S3_BUCKET fill:#ff6d00,color:white
    style RDS_PG fill:#1565c0,color:white
```

| Dịch vụ             | Cấu hình                                                                                                                                           |
| ------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Amazon S3**       | Bucket: `smart-invoice-shield-storage`, Encryption: AES-256 (SSE-S3), **Block all public access**, Presigned URLs (15-60 phút), 3 folder prefix    |
| **S3 Lifecycle**    | Standard → Glacier Instant Retrieval sau 90 ngày                                                                                                   |
| **RDS PostgreSQL**  | Instance: `db.t3.micro`, Engine: PostgreSQL 16.x, Multi-AZ Standby, Private Subnet, Automated Backup 7 ngày, PITR, DB: `SmartInvoiceDb`, 15 tables |
| **DB Subnet Group** | `smartinvoice-db-subnet-group` (2 Private Subnets)                                                                                                 |

### 4.7. Tầng Quản Lý Cấu Hình (SSM Parameter Store)

| Parameter                                  | Type             | Mô tả                                |
| ------------------------------------------ | ---------------- | ------------------------------------ |
| `/SmartInvoice/prod/COGNITO_USER_POOL_ID`  | String           | Cognito User Pool ID                 |
| `/SmartInvoice/prod/COGNITO_CLIENT_ID`     | String           | Cognito App Client ID                |
| `/SmartInvoice/prod/COGNITO_CLIENT_SECRET` | **SecureString** | Cognito App Client Secret            |
| `/SmartInvoice/prod/AWS_SQS_OCR_URL`       | String           | SQS OCR Queue URL                    |
| `/SmartInvoice/prod/AWS_SQS_URL`           | String           | SQS VietQR Queue URL                 |
| `/SmartInvoice/prod/POSTGRES_HOST`         | String           | RDS Endpoint                         |
| `/SmartInvoice/prod/POSTGRES_PORT`         | String           | `5432`                               |
| `/SmartInvoice/prod/POSTGRES_DB`           | String           | `SmartInvoiceDb`                     |
| `/SmartInvoice/prod/POSTGRES_USER`         | String           | `postgres`                           |
| `/SmartInvoice/prod/POSTGRES_PASSWORD`     | **SecureString** | RDS Master Password                  |
| `/SmartInvoice/prod/AWS_REGION`            | String           | `ap-southeast-1`                     |
| `/SmartInvoice/prod/AWS_S3_BUCKET_NAME`    | String           | `smart-invoice-shield-storage`       |
| `/SmartInvoice/prod/OCR_API_ENDPOINT`      | String           | `http://ocr.smartinvoice.local:5000` |
| `/SmartInvoice/prod/ALLOWED_ORIGINS`       | String           | Amplify domain URL                   |

---

## 5. LUỒNG XỬ LÝ HÓA ĐƠN (SEQUENCE DIAGRAM)

```mermaid
sequenceDiagram
    autonumber
    participant U as 👤 Người dùng
    participant FE as React SPA (Amplify)
    participant CG as Amazon Cognito
    participant CF as CloudFront (HTTPS)
    participant API as .NET 9 API (EB)
    participant S3 as Amazon S3
    participant SQS1 as SQS OCR Queue
    participant OCR as ECS Fargate (Python OCR)
    participant DB as RDS PostgreSQL
    participant SQS2 as SQS VietQR Queue
    participant VQR as VietQR API

    Note over U,VQR: ═══ PHASE 1: AUTHENTICATION & UPLOAD ═══

    U->>FE: Truy cập ứng dụng
    FE->>CG: Đăng nhập (Email + Password)
    CG-->>FE: JWT Token (Access + ID + Refresh)

    U->>FE: Chọn file hóa đơn (PDF/PNG/JPG)
    FE->>CF: POST /api/invoices/upload (HTTPS)
    CF->>API: Forward (HTTP:80 qua ALB)
    API->>S3: Upload file (AES-256 SSE)
    S3-->>API: S3 Key
    API->>DB: Tạo Invoice (Status: Processing)
    API->>SQS1: Gửi OcrJobMessage {invoiceId, s3Key}
    API-->>CF: 202 Accepted
    CF-->>FE: "Đang xử lý"
    FE-->>U: Hiển thị trạng thái Processing

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

    OCR->>API: POST /api/ocr/results (qua Cloud Map DNS)
    API->>DB: Cập nhật Invoice (ExtractedData, Status: Draft)
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
    FE->>CF: GET /api/invoices (HTTPS)
    CF->>API: Forward qua ALB
    API->>DB: Query invoices
    DB-->>API: List<Invoice>
    API-->>CF: JSON response
    CF-->>FE: Dữ liệu hóa đơn
    FE-->>U: Hiển thị kết quả + RiskLevel (Green/Yellow/Red)
```

### Luồng xử lý tóm tắt (Happy Path)

| Bước                     | Hành động                                                                            | Dịch vụ AWS         |
| ------------------------ | ------------------------------------------------------------------------------------ | ------------------- |
| **1. Xác thực**          | Người dùng đăng nhập, nhận JWT Token                                                 | Cognito             |
| **2. Upload**            | File tải lên qua CloudFront → ALB → Backend → S3 (AES-256)                           | CloudFront, ALB, S3 |
| **3. Queue Job**         | Backend tạo OcrJobMessage, đẩy vào SQS, phản hồi 202 "Đang xử lý"                    | SQS                 |
| **4. AI OCR**            | Fargate pull job, download file S3, chạy PaddleOCR+VietOCR hoặc Gemini               | ECS Fargate, S3     |
| **5. Cập nhật kết quả**  | OCR gọi Backend API (qua Cloud Map DNS) để cập nhật DB                               | Cloud Map, RDS      |
| **6. VietQR Validation** | Publish VietQR message → Background worker gọi API xác thực MST → cập nhật RiskLevel | SQS, VietQR API     |
| **7. Hiển thị**          | Frontend poll/refresh, hiển thị dữ liệu + mức rủi ro                                 | Amplify             |

---

## 6. MÔ HÌNH BẢO MẬT NHIỀU TẦNG

```mermaid
graph TB
    subgraph L1["Layer 1: Edge Security"]
        HTTPS["HTTPS Everywhere<br/>(CloudFront + ACM)"]
        CDN["CDN + DDoS Protection<br/>(Amplify Built-in)"]
        RATE["Rate Limiting<br/>(CloudFront WAF)"]
    end

    subgraph L2["Layer 2: Authentication & Authorization"]
        JWT["Cognito JWT Tokens<br/>(Access + ID + Refresh)"]
        CLAIMS["Custom Claims Transformer<br/>(Role + Permissions → Claims)"]
        RBAC["Role-Based Access Control<br/>(SuperAdmin, CompanyAdmin,<br/>Accountant, Viewer)"]
    end

    subgraph L3["Layer 3: Network Isolation"]
        VPC_L["VPC Isolation<br/>(10.0.0.0/16)"]
        SG["Security Groups<br/>(4 SGs, Strict Inbound Rules)"]
        PRIV["Private Subnets<br/>(All workloads: Backend, RDS, ECS)"]
        NAT_L["NAT Gateway<br/>(Controlled Outbound Only)"]
    end

    subgraph L4["Layer 4: Data Protection"]
        S3_ENC["S3 AES-256 SSE<br/>(Block All Public Access)"]
        RDS_ENC["RDS Encryption at Rest"]
        SSM_L["SSM Parameter Store<br/>(SecureString for secrets)"]
        PRESIGN["Presigned URLs<br/>(Time-limited S3 access)"]
    end

    subgraph L5["Layer 5: Application Security"]
        MAINT["Maintenance Middleware"]
        CORS_L["CORS Policy<br/>(Amplify origin only)"]
        PERM["Permission-based Policies<br/>(14 granular permissions)"]
    end

    L1 --> L2 --> L3 --> L4 --> L5

    style L1 fill:#1a73e8,color:white
    style L2 fill:#ea4335,color:white
    style L3 fill:#34a853,color:white
    style L4 fill:#fbbc04,color:black
    style L5 fill:#9c27b0,color:white
```

### Điểm nổi bật về bảo mật

| Tầng        | Biện pháp        | Chi tiết                                                  |
| ----------- | ---------------- | --------------------------------------------------------- |
| **Edge**    | HTTPS bắt buộc   | CloudFront redirect HTTP → HTTPS, ACM certificates        |
| **Edge**    | Rate Limiting    | CloudFront WAF chống DDoS/Spam API                        |
| **Auth**    | JWT + RBAC       | 4 roles, 14 permissions, Custom Claims Transformer        |
| **Network** | Private Subnets  | Backend, RDS, ECS Fargate **tất cả** trong Private Subnet |
| **Network** | Security Groups  | 4 SGs với inbound rules nghiêm ngặt (least privilege)     |
| **Network** | NAT Gateway      | Outbound-only Internet access cho Private Subnets         |
| **Data**    | Encryption       | S3 AES-256, RDS encryption at rest                        |
| **Data**    | No Public S3     | Block all public access, chỉ Presigned URLs               |
| **Data**    | SSM SecureString | Secrets (passwords, client secrets) mã hóa KMS            |
| **App**     | CORS strict      | Chỉ cho phép Amplify domain                               |

---

## 7. TÍCH HỢP BÊN NGOÀI & RESILIENCE

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

| Tích hợp          | Giao thức              | Resilience Pattern                                           |
| ----------------- | ---------------------- | ------------------------------------------------------------ |
| **VNPay**         | HMAC-SHA512 signed URL | Payment URL generation, IPN callback                         |
| **VietQR API**    | REST (HTTPS)           | Polly Retry 3×, Circuit Breaker (5 fail → 1 min), Timeout 5s |
| **Google Gemini** | REST (HTTPS)           | Fallback to PaddleOCR + VietOCR khi quota exceeded           |

---

## 8. CI/CD PIPELINE

```mermaid
graph LR
    subgraph DEV["Developer"]
        GIT["Git Push<br/>to main branch"]
    end

    subgraph GITHUB["GitHub Actions"]
        subgraph BACKEND_CI["Backend Pipeline"]
            B0["✅ Unit Tests<br/>(xUnit + InMemory)"]
            B1["Build .NET 9<br/>Docker Image"]
            B2["Push to ECR<br/>(smartinvoice-backend)"]
            B3["Deploy to<br/>Elastic Beanstalk"]
        end
        subgraph OCR_CI["OCR Pipeline"]
            O1["Build Python<br/>Docker Image"]
            O2["Push to ECR<br/>(smartinvoice-ocr)"]
            O3["Update ECS<br/>Service (force)"]
        end
        subgraph FE_CI["Frontend Pipeline"]
            F1["Amplify Auto-Build<br/>(on push to main)"]
        end
    end

    subgraph AWS_TARGET["AWS"]
        ECR_BE["ECR: smartinvoice-backend"]
        ECR_OCR["ECR: smartinvoice-ocr"]
        EB_ENV["Elastic Beanstalk<br/>Smartinvoice-api-env"]
        ECS_SVC["ECS Service<br/>smartinvoice-ocr-task-service"]
        AMP["Amplify App"]
    end

    GIT --> B0 & O1 & F1
    B0 -->|Pass| B1
    B1 --> B2 --> B3 --> EB_ENV
    O1 --> O2 --> O3 --> ECS_SVC
    F1 --> AMP
    B2 --> ECR_BE
    O2 --> ECR_OCR

    style GITHUB fill:#24292e,color:white
    style B0 fill:#34a853,stroke:#2d8f47,color:white
```

### 8.1. Đảm bảo chất lượng (Quality Assurance)

- **Unit Testing Layer**: Áp dụng bộ test tự động (48 test cases) bao phủ các logic phức tạp về tính toán Hạn ngạch (Quota), Xác thực (Auth) và xử lý Hóa đơn (Invoice).
- **Database Isolation**: Sử dụng `Microsoft.EntityFrameworkCore.InMemory` trong pipeline để giả lập database, giúp chạy test nhanh mà không cần khởi tạo RDS PostgreSQL thật.
- **Blocking Deployment**: Mọi lỗi logic phát hiện bởi Unit Test sẽ kích hoạt lệnh `exit 1`, ngăn chặn triệt để việc đẩy code lỗi lên ECR và Elastic Beanstalk.

### 8.2. GitHub Secrets cần thiết

| Secret                  | Giá trị             |
| ----------------------- | ------------------- |
| `AWS_ACCESS_KEY_ID`     | IAM Access Key      |
| `AWS_SECRET_ACCESS_KEY` | IAM Secret Key      |
| `AWS_REGION`            | `ap-southeast-1`    |
| `AWS_ACCOUNT_ID`        | 12-digit Account ID |

### Deployment Artifacts

| Service      | Artifact             | Mô tả                                              |
| ------------ | -------------------- | -------------------------------------------------- |
| **Backend**  | `Dockerrun.aws.json` | EB Docker deployment descriptor, trỏ đến ECR image |
| **OCR**      | ECS Task Definition  | Force new deployment via `aws ecs update-service`  |
| **Frontend** | Amplify auto-detect  | Vite build → `dist/` → CDN distribution            |

---

## 9. GIÁM SÁT & CẢNH BÁO

### 9.1. CloudWatch Logs

| Log Group                    | Source              |
| ---------------------------- | ------------------- |
| `/aws/elasticbeanstalk/...`  | Backend .NET 9 logs |
| `/ecs/smartinvoice-ocr-task` | OCR Container logs  |

### 9.2. CloudWatch Alarms

| Alarm           | Metric                | Condition      | Action          |
| --------------- | --------------------- | -------------- | --------------- |
| OCR Tasks Down  | `RunningTaskCount`    | < 1 task       | SNS Email Alert |
| API 5xx Errors  | `HTTPCode_Target_5XX` | > 5 errors/min | SNS Email Alert |
| RDS Storage Low | `FreeStorageSpace`    | < 5 GB         | SNS Email Alert |

### 9.3. SNS Topic

| Thành phần   | Giá trị                  |
| ------------ | ------------------------ |
| **Topic**    | `smartinvoice-alerts`    |
| **Protocol** | Email subscription       |
| **Trigger**  | CloudWatch Alarm actions |

---

## 10. CHIẾN LƯỢC TỐI ƯU CHI PHÍ

| #   | Chiến lược                       | Mô tả                                                      | Tiết kiệm ước tính     |
| --- | -------------------------------- | ---------------------------------------------------------- | ---------------------- |
| 1   | **Cloud Map thay Internal ALB**  | Sử dụng DNS nội bộ thay vì ALB cho giao tiếp Backend ↔ OCR | ~$18/tháng             |
| 2   | **Private Subnet + NAT Gateway** | 1 NAT Gateway chung cho cả VPC thay vì NAT per AZ          | Network costs hợp lý   |
| 3   | **ECS Fargate (Pay-as-you-go)**  | Chỉ trả tiền khi có OCR job thực tế                        | Linh hoạt theo nhu cầu |
| 4   | **SQS Long Polling**             | Giảm API calls (20s wait thay vì short poll)               | ~90% SQS API costs     |
| 5   | **S3 Lifecycle**                 | Auto-transition sang Glacier Instant Retrieval sau 90 ngày | ~68% storage costs     |
| 6   | **t3.micro (Burstable)**         | CPU credits cho workload không đều                         | Phù hợp startup/SME    |
| 7   | **SSM Parameter Store**          | Free tier (Standard parameters)                            | $0 cho config/secrets  |
| 8   | **Cognito Free Tier**            | 50,000 MAU miễn phí                                        | $0 cho auth            |
| 9   | **Amplify Free Tier**            | 1,000 build minutes/tháng + 15GB hosting                   | $0 cho frontend        |
| 10  | **CloudFront Free Tier**         | 1TB transfer out/tháng miễn phí                            | Giảm bandwidth cost    |

---

## 11. ƯỚC TÍNH CHI PHÍ HÀNG THÁNG

| Dịch vụ                 | Cấu hình               | Chi phí (USD) |
| ----------------------- | ---------------------- | :-----------: |
| EC2 (Elastic Beanstalk) | 2× `t3.micro`          |     ~$15      |
| RDS PostgreSQL          | `db.t3.micro` Multi-AZ |     ~$28      |
| ECS Fargate             | 2 tasks (2 vCPU, 4GB)  |     ~$20      |
| NAT Gateway             | 1 AZ + data transfer   |     ~$35      |
| ALB (Public)            | 1 ALB                  |     ~$18      |
| S3 + CloudFront         | Storage + CDN          |      ~$2      |
| SQS + SSM + Cognito     | Free tier              |      ~$0      |
| **TỔNG CỘNG**           |                        |   **~$118**   |

> **Ghi chú**: Chi phí thực tế có thể thay đổi tùy theo lượng traffic, dung lượng S3, và data transfer qua NAT Gateway. Sử dụng **AWS Budgets** để thiết lập cảnh báo khi chi phí vượt ngân sách.

---

## IAM ROLES

| Role                                | Trusted Entity    | Policies                               | Mục đích                              |
| ----------------------------------- | ----------------- | -------------------------------------- | ------------------------------------- |
| `aws-elasticbeanstalk-ec2-role`     | EC2 (EB Compute)  | S3, SQS, Cognito, SSM, ECR, CloudWatch | EC2 Backend truy cập các dịch vụ AWS  |
| `aws-elasticbeanstalk-service-role` | Elastic Beanstalk | EnhancedHealth, ManagedUpdates         | EB quản lý môi trường                 |
| `ecsTaskExecutionRole`              | ECS Task          | ECSTaskExecution, CloudWatch           | Fargate pull ECR image, ghi logs      |
| `smartinvoice-ecs-task-role`        | ECS Task          | S3, SQS, SSM                           | OCR Container truy cập tài nguyên AWS |
