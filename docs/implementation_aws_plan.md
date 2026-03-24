# KẾ HOẠCH DEPLOY TOÀN BỘ HỆ THỐNG LÊN AWS

## Tổng quan

Tài liệu hướng dẫn deploy từng bước hệ thống SmartInvoice Shield lên AWS, bao gồm: tạo VPC/networking, RDS PostgreSQL, ECR repositories, ECS Fargate cho OCR, Elastic Beanstalk cho Backend .NET 9, Route 53 + ACM, và CloudWatch monitoring.

## Những gì đã có sẵn ✅

| Dịch vụ | Trạng thái |
|---------|-----------|
| AWS Amplify (Frontend) | ✅ Kết nối GitHub main, auto-deploy |
| Amazon Cognito | ✅ User Pool + App Client đã cấu hình |
| Amazon S3 + Glacier Lifecycle | ✅ Bucket `smart-invoice-shield-storage` |
| SQS OCR Queue | ✅ Đã tạo |
| SQS VietQR Queue | ✅ Đã tạo |
| SSM Parameter Store | ✅ Prefix `/SmartInvoice/dev/` |

## Những gì cần deploy 🚀

| Dịch vụ | Mục đích |
|---------|---------|
| VPC + Subnets + Security Groups | Mạng riêng cho Backend + Database |
| RDS PostgreSQL | Database production |
| ECR (2 repositories) | Lưu Docker images |
| ECS Fargate | Chạy Python OCR container |
| Elastic Beanstalk | Chạy .NET 9 Backend API |
| Route 53 + ACM | Domain + HTTPS |
| CloudWatch + SNS | Monitoring & Alerts |

---

## BƯỚC 1: TẠO VPC & NETWORKING

> [!IMPORTANT]
> Đây là bước nền tảng. Mọi dịch vụ khác đều phụ thuộc vào VPC này.

### 1.1. Tạo VPC

1. Vào **AWS Console → VPC → Create VPC**
2. Chọn **"VPC and more"** (tạo đầy đủ subnets)
3. Cấu hình:

| Tham số | Giá trị |
|---------|---------|
| Name | `smartinvoice-vpc` |
| IPv4 CIDR | `10.0.0.0/16` |
| Number of AZs | **2** (`ap-southeast-1a`, `ap-southeast-1b`) |
| Public subnets | **2** (cho ALB, Backend, Fargate) |
| Private subnets | **2** (chỉ cho RDS Database) |
| NAT Gateway | **Không có (None)** ❌ (Tiết kiệm ~$32/tháng) |
| VPC Endpoints | **Không cần thiết** (Vì Fargate có Public IP có thể ra Internet) |

> [!TIP]
> **Cost Optimization:** Bỏ hoàn toàn NAT Gateway. Để Fargate và Backend .NET có thể kết nối Internet (kéo image từ ECR, gọi API ngoài, gửi file S3), ta sẽ đặt chúng ở **Public Subnet** và cấp **Public IP**. Chỉ có Database RDS được giữ ở Private Subnet để bảo mật tối đa.

### 1.2. Tạo Security Groups

Tạo **4 Security Groups** trong VPC vừa tạo:

#### SG 1: `sg-alb` (cho Application Load Balancer)

| Rule | Type | Port | Source |
|------|------|------|--------|
| Inbound | HTTPS | 443 | `0.0.0.0/0` (Internet) |
| Inbound | HTTP | 80 | `0.0.0.0/0` (redirect → 443) |
| Outbound | All | All | `0.0.0.0/0` |

#### SG 2: `sg-backend` (cho EC2/Elastic Beanstalk)

| Rule | Type | Port | Source |
|------|------|------|--------|
| Inbound | Custom TCP | 8080 | `sg-alb` (chỉ nhận từ ALB) |
| Outbound | All | All | `0.0.0.0/0` |

#### SG 3: `sg-ocr` (cho ECS Fargate OCR)

| Rule | Type | Port | Source |
|------|------|------|--------|
| Inbound | Custom TCP | 5000 | `sg-backend` (chỉ nhận từ Backend) |
| Outbound | All | All | `0.0.0.0/0` |

#### SG 4: `sg-database` (cho RDS)

| Rule | Type | Port | Source |
|------|------|------|--------|
| Inbound | PostgreSQL | 5432 | `sg-backend` |
| Inbound | PostgreSQL | 5432 | `sg-ocr` |
| Outbound | All | All | `0.0.0.0/0` |

---

## BƯỚC 2: TẠO RDS POSTGRESQL

### 2.1. Tạo DB Subnet Group

1. **RDS Console → Subnet Groups → Create**
2. Name: `smartinvoice-db-subnet-group`
3. VPC: `smartinvoice-vpc`
4. Chọn **2 Private Subnets** (AZ-1a + AZ-1b)

### 2.2. Tạo RDS Instance

1. **RDS Console → Create Database**
2. Cấu hình:

| Tham số | Giá trị |
|---------|---------|
| Engine | PostgreSQL 16 |
| Template | **Free Tier** hoặc Dev/Test |
| Instance | `db.t3.micro` (hoặc `db.t4g.micro` cho ARM, rẻ hơn) |
| DB name | `SmartInvoiceDb` |
| Master username | `postgres` |
| Master password | **(tạo password mạnh, lưu vào SSM)** |
| VPC | `smartinvoice-vpc` |
| Subnet group | `smartinvoice-db-subnet-group` |
| Public access | **No** ❌ |
| Security Group | `sg-database` |
| Storage | 20 GB gp3 |
| Backup retention | 7 ngày |
| Enable deletion protection | ✅ Yes |

### 2.3. Lưu Connection String vào SSM Parameter Store

```bash
aws ssm put-parameter \
  --name "/SmartInvoice/dev/POSTGRES_HOST" \
  --value "smartinvoice-db.xxxxx.ap-southeast-1.rds.amazonaws.com" \
  --type "SecureString"

aws ssm put-parameter \
  --name "/SmartInvoice/dev/POSTGRES_PORT" \
  --value "5432" \
  --type "String"

aws ssm put-parameter \
  --name "/SmartInvoice/dev/POSTGRES_DB" \
  --value "SmartInvoiceDb" \
  --type "String"

aws ssm put-parameter \
  --name "/SmartInvoice/dev/POSTGRES_USER" \
  --value "postgres" \
  --type "String"

aws ssm put-parameter \
  --name "/SmartInvoice/dev/POSTGRES_PASSWORD" \
  --value "YOUR_STRONG_PASSWORD" \
  --type "SecureString"
```

> [!CAUTION]
> **KHÔNG BAO GIỜ** commit password vào source code. Luôn dùng SSM Parameter Store (`SecureString`).

---

## BƯỚC 3: TẠO ECR REPOSITORIES & PUSH DOCKER IMAGES

### 3.1. Tạo 2 ECR Repositories

```bash
# Repository cho Backend .NET 9
aws ecr create-repository \
  --repository-name smartinvoice-backend \
  --region ap-southeast-1

# Repository cho OCR Python
aws ecr create-repository \
  --repository-name smartinvoice-ocr \
  --region ap-southeast-1
```

### 3.2. Build & Push Backend Image

```bash
# Login ECR
aws ecr get-login-password --region ap-southeast-1 | \
  docker login --username AWS --password-stdin <ACCOUNT_ID>.dkr.ecr.ap-southeast-1.amazonaws.com

# Build image
cd SmartInvoice.API
docker build -t smartinvoice-backend .

# Tag
docker tag smartinvoice-backend:latest \
  <ACCOUNT_ID>.dkr.ecr.ap-southeast-1.amazonaws.com/smartinvoice-backend:latest

# Push
docker push \
  <ACCOUNT_ID>.dkr.ecr.ap-southeast-1.amazonaws.com/smartinvoice-backend:latest
```

### 3.3. Build & Push OCR Image

```bash
cd invoice_ocr
docker build -t smartinvoice-ocr .

docker tag smartinvoice-ocr:latest \
  <ACCOUNT_ID>.dkr.ecr.ap-southeast-1.amazonaws.com/smartinvoice-ocr:latest

docker push \
  <ACCOUNT_ID>.dkr.ecr.ap-southeast-1.amazonaws.com/smartinvoice-ocr:latest
```

> [!NOTE]
> OCR image khá nặng (~2-3 GB) do PyTorch + PaddleOCR. Push lần đầu sẽ mất 10-15 phút.

---

## BƯỚC 4: DEPLOY OCR SERVICE (ECS FARGATE)

### 4.1. Tạo ECS Cluster

1. **ECS Console → Create Cluster**

| Tham số | Giá trị |
|---------|---------|
| Cluster name | `smartinvoice-cluster` |
| Infrastructure | **AWS Fargate** (serverless) |

### 4.2. Tạo Task Definition

1. **ECS → Task Definitions → Create new**
2. Cấu hình:

| Tham số | Giá trị |
|---------|---------|
| Family | `smartinvoice-ocr-task` |
| Launch type | **Fargate** |
| OS | Linux/X86_64 |
| CPU | **1 vCPU** |
| Memory | **4 GB** (PaddleOCR + VietOCR cần ~3GB RAM) |
| Task role | `ecsTaskRole` (cần quyền S3, SQS, RDS) |
| Task execution role | `ecsTaskExecutionRole` (cần quyền ECR pull) |

**Container definition:**

| Tham số | Giá trị |
|---------|---------|
| Container name | `ocr-container`  |
| Image | `<ACCOUNT_ID>.dkr.ecr.ap-southeast-1.amazonaws.com/smartinvoice-ocr:latest` |
| Port mappings | 5000 (TCP) |
| CPU | 1024 |
| Memory hard limit | 4096 |
| Environment | `DEVICE=cpu`, `HOST=0.0.0.0`, `PORT=5000` |
| Log driver | `awslogs` → CloudWatch log group `/ecs/smartinvoice-ocr` |

### 4.3. Tạo IAM Role cho ECS Task

Tạo role `smartinvoice-ecs-task-role` với các policy:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": ["s3:GetObject", "s3:PutObject"],
      "Resource": "arn:aws:s3:::smart-invoice-shield-storage/*"
    },
    {
      "Effect": "Allow",
      "Action": ["sqs:*"],
      "Resource": [
        "arn:aws:sqs:ap-southeast-1:<ACCOUNT_ID>:smartinvoice-ocr-queue",
        "arn:aws:sqs:ap-southeast-1:<ACCOUNT_ID>:smartinvoice-vietqr-queue"
      ]
    },
    {
      "Effect": "Allow",
      "Action": ["ssm:GetParameter", "ssm:GetParameters", "ssm:GetParametersByPath"],
      "Resource": "arn:aws:ssm:ap-southeast-1:<ACCOUNT_ID>:parameter/SmartInvoice/*"
    }
  ]
}
```

### 4.4. Tạo ECS Service

1. **ECS → Cluster → Create Service**

| Tham số | Giá trị |
|---------|---------|
| Service name | `smartinvoice-ocr-service` |
| Task definition | `smartinvoice-ocr-task` (latest) |
| Desired tasks | **1** (scale lên khi cần) |
| Capacity provider | **FARGATE_SPOT** (tiết kiệm ~70%) |
| VPC | `smartinvoice-vpc` |
| Subnets | **Public subnets** ⚠️ |
| Security group | `sg-ocr` |
| Public IP | **ENABLED** ⚠️ (Bắt buộc vì không có NAT Gateway) |
| Service Discovery | Enable (tạo namespace `smartinvoice.local`, service name [ocr](file:///d:/Documents/code/CSharp/InvoiceManagement/invoice_ocr/src/run_ocr.py#272-283)) |

> [!IMPORTANT]
> Vì chúng ta **không dùng NAT Gateway** để tiết kiệm chi phí, bắt buộc phải chọn **Public subnets** và cấp **Public IP** cho ECS Tasks. Nếu không, Fargate sẽ không thể kéo Docker Image từ ECR hay gọi các dịch vụ AWS khác (S3, SQS).

### 4.5. Cấu hình Auto Scaling cho Fargate

1. **ECS Service → Auto Scaling → Update**
2. Cấu hình:

| Tham số | Giá trị |
|---------|---------|
| Min tasks | **0** (Scale-to-Zero khi idle) |
| Max tasks | **5** |
| Scaling policy | **Target tracking** |
| Target metric | SQS `ApproximateNumberOfMessagesVisible` > 2 |
| Scale-in cooldown | 300s |
| Scale-out cooldown | 60s |

---

## BƯỚC 5: DEPLOY BACKEND API (ELASTIC BEANSTALK)

### 5.1. Tạo Elastic Beanstalk Application

1. **EB Console → Create Application**

| Tham số | Giá trị |
|---------|---------|
| Application name | `smartinvoice-api` |
| Platform | **Docker** (running on 64bit Amazon Linux 2023) |

### 5.2. Tạo Environment

1. **Create Environment → Web Server**
2. Cấu hình:

| Tham số | Giá trị |
|---------|---------|
| Environment name | `smartinvoice-api-prod` |
| Domain | `smartinvoice-api-prod.ap-southeast-1.elasticbeanstalk.com` |
| Platform | Docker on Amazon Linux 2023 |
| Instance type | `t3.micro` (2× cho Multi-AZ) |
| Min instances | **2** |
| Max instances | **4** |
| VPC | `smartinvoice-vpc` |
| Instance subnets | **Public subnets** (Do không có NAT, cấp Public IPv4 cho EC2) |
| Load Balancer | **Application Load Balancer** |
| LB subnets | **Public subnets** |
| LB Security Group | `sg-alb` |
| Instance Security Group | `sg-backend` |

### 5.3. Tạo file `Dockerrun.aws.json` (đặt ở root dự án)

Tạo file mới `SmartInvoice.API/Dockerrun.aws.json`:

```json
{
  "AWSEBDockerrunVersion": "1",
  "Image": {
    "Name": "<ACCOUNT_ID>.dkr.ecr.ap-southeast-1.amazonaws.com/smartinvoice-backend:latest",
    "Update": "true"
  },
  "Ports": [
    {
      "ContainerPort": 8080,
      "HostPort": 8080
    }
  ]
}
```

### 5.4. Cấu hình Environment Variables

**EB Console → Configuration → Software → Environment properties:**

| Key | Value |
|-----|-------|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `AWS_REGION` | `ap-southeast-1` |

**Cấu hình dynamic domains trong SSM (Mới):**
Để tránh hardcode, hãy thêm các parameter sau vào SSM Parameter Store:
1. **/SmartInvoice/prod/ALLOWED_ORIGINS**: `https://main.d3nvvjzg8ojoqd.amplifyapp.com,https://yourdomain.com` (Danh sách các domain FE được phép gọi API, cách nhau bởi dấu phẩy).
2. **/SmartInvoice/prod/OCR_API_ENDPOINT**: `http://ocr.smartinvoice.local:5000` (URL nội bộ gọi sang service AI).

> [!IMPORTANT]
> Backend tự đọc DB credentials từ SSM (`/SmartInvoice/dev/POSTGRES_*`), Cognito config (`COGNITO_*`), SQS URLs (`AWS_SQS_*`), và VnPay keys. Không cần set thủ công trong EB.

### 5.5. Cấu hình IAM Role cho EB Instance

Đảm bảo Instance Profile của EB có các quyền:

- `AmazonS3FullAccess` (hoặc scoped policy cho bucket cụ thể)
- `AmazonSQSFullAccess` (hoặc scoped cho 2 queue)
- `AmazonCognitoPowerUser`
- `AmazonSSMReadOnlyAccess`
- `AmazonECR-ContainerRegistryReadOnly`
- `CloudWatchLogsFullAccess`

### 5.6. Deploy

```bash
# Option A: EB CLI (đơn giản nhất)
cd SmartInvoice.API
eb init smartinvoice-api --region ap-southeast-1 --platform docker
eb deploy smartinvoice-api-prod

# Option B: ZIP upload qua Console
# Zip toàn bộ SmartInvoice.API/ bao gồm Dockerfile + Dockerrun.aws.json
# Upload qua EB Console → Upload and deploy
```

---

## BƯỚC 6: CẤU HÌNH SSM PARAMETER STORE (BỔ SUNG)

Đảm bảo tất cả parameters sau đã tồn tại trong `/SmartInvoice/dev/`:

```bash
# ─── Database ───
/SmartInvoice/dev/POSTGRES_HOST          # RDS endpoint
/SmartInvoice/dev/POSTGRES_PORT          # 5432
/SmartInvoice/dev/POSTGRES_DB            # SmartInvoiceDb
/SmartInvoice/dev/POSTGRES_USER          # postgres
/SmartInvoice/dev/POSTGRES_PASSWORD      # (SecureString)

# ─── Cognito ───
/SmartInvoice/dev/COGNITO_USER_POOL_ID   # ap-southeast-1_xxxxx
/SmartInvoice/dev/COGNITO_CLIENT_ID      # xxxxx
/SmartInvoice/dev/COGNITO_CLIENT_SECRET  # (SecureString)

# ─── SQS ───
/SmartInvoice/dev/AWS_SQS_URL            # VietQR queue URL
/SmartInvoice/dev/AWS_SQS_OCR_URL        # OCR queue URL

# ─── VnPay ───
/SmartInvoice/dev/VnPay:TmnCode          # merchant code
/SmartInvoice/dev/VnPay:HashSecret       # (SecureString)
/SmartInvoice/dev/VnPay:Url              # https://pay.vnpay.vn/... (production)
/SmartInvoice/dev/VnPay:ReturnUrl        # https://yourdomain.com/app/payment/result

# ─── AWS ───
/SmartInvoice/dev/AWS_REGION             # ap-southeast-1
/SmartInvoice/dev/AWS:BucketName         # smart-invoice-shield-storage
```

---

## BƯỚC 7: CẤU HÌNH DOMAIN & HTTPS (ROUTE 53 + ACM)

### 7.1. Đăng ký / Chuyển domain vào Route 53

- Nếu chưa có domain: **Route 53 → Register Domain** (ví dụ: `smartinvoice.vn`)
- Nếu đã có: tạo **Hosted Zone** và trỏ NS records

### 7.2. Tạo SSL Certificate (ACM)

1. **ACM Console → Request certificate**
2. Domain: `*.smartinvoice.vn` + `smartinvoice.vn`
3. Validation: **DNS validation** (ACM tự tạo CNAME trong Route 53)
4. Đợi status → **Issued** ✅

### 7.3. Gắn Certificate vào ALB

1. **EC2 → Load Balancers → chọn ALB của Elastic Beanstalk**
2. Listeners:
   - **HTTPS:443** → Forward to target group (backend instances)
   - **HTTP:80** → Redirect to HTTPS:443
3. Security Policy: `ELBSecurityPolicy-TLS13-1-2-2021-06`

### 7.4. Tạo DNS Records

| Record | Type | Value |
|--------|------|-------|
| `api.smartinvoice.vn` | **A (Alias)** | ALB DNS name |
| `smartinvoice.vn` | **A (Alias)** | Amplify app URL |
| `www.smartinvoice.vn` | **CNAME** | Amplify app URL |

### 7.5. Cập nhật Amplify Frontend

1. **Amplify Console → Domain management → Add domain**
2. Thêm `smartinvoice.vn` và `www.smartinvoice.vn`
3. Cập nhật biến môi trường Amplify:

| Key | Value |
|-----|-------|
| `VITE_API_URL` | `https://api.smartinvoice.vn/api` |

### 7.6. Cập nhật CORS trong Backend

Cập nhật [Program.cs](file:///d:/Documents/code/CSharp/InvoiceManagement/SmartInvoice.API/Program.cs) — CORS policy cho production domain:

```diff
- builder.WithOrigins("http://localhost:3000")
+ builder.WithOrigins(
+     "http://localhost:3000",
+     "https://smartinvoice.vn",
+     "https://www.smartinvoice.vn"
+ )
```

### 7.7. Cập nhật VnPay Return URL

Trong SSM Parameter Store, cập nhật:

```
/SmartInvoice/dev/VnPay:ReturnUrl = https://smartinvoice.vn/app/payment/result
```

---

## BƯỚC 8: CLOUDWATCH MONITORING & ALERTS

### 8.1. Tạo CloudWatch Log Groups

```bash
# Backend logs (EB tự tạo, nhưng verify)
/aws/elasticbeanstalk/smartinvoice-api-prod

# OCR Fargate logs
/ecs/smartinvoice-ocr

# RDS logs
/aws/rds/instance/smartinvoice-db/postgresql
```

### 8.2. Tạo SNS Topic cho Alerts

```bash
aws sns create-topic --name smartinvoice-alerts
aws sns subscribe \
  --topic-arn arn:aws:sns:ap-southeast-1:<ACCOUNT_ID>:smartinvoice-alerts \
  --protocol email \
  --notification-endpoint your-team@email.com
```

### 8.3. Tạo CloudWatch Alarms

| Alarm | Metric | Threshold | Action |
|-------|--------|-----------|--------|
| Backend CPU High | EB CPU Utilization | > 80% for 5 min | SNS alert |
| Backend 5xx Errors | ALB HTTPCode_ELB_5XX | > 10 in 5 min | SNS alert |
| RDS CPU High | RDS CPUUtilization | > 80% for 5 min | SNS alert |
| RDS Free Storage Low | RDS FreeStorageSpace | < 2 GB | SNS alert |
| OCR Queue Depth | SQS ApproximateNumberOfMessagesVisible | > 50 for 10 min | SNS alert |
| VietQR Queue Depth | SQS ApproximateNumberOfMessagesVisible | > 100 for 10 min | SNS alert |

### 8.4. AWS Budgets

1. **Billing → Budgets → Create Budget**
2. Budget amount: theo ngân sách team (ví dụ: $50/tháng)
3. Alert at: **80%** và **100%** → email notification

---

## BƯỚC 9: DATABASE MIGRATION

Sau khi RDS đã chạy, Backend sẽ **tự động migrate** khi khởi động nhờ code trong [Program.cs](file:///d:/Documents/code/CSharp/InvoiceManagement/SmartInvoice.API/Program.cs):

```csharp
// Auto-migrate Database (đã có trong code)
await context.Database.MigrateAsync();
```

Verify bằng cách xem EB logs:
```
Database migration applied successfully.
Seeded initial DocumentTypes.
```

> [!NOTE]
> Backend tự retry 5 lần (mỗi 3 giây) nếu DB chưa sẵn sàng, nên không cần lo về timing.

---

## CHECKLIST DEPLOY CUỐI CÙNG

| # | Task | Status |
|---|------|--------|
| 1 | Tạo VPC + 4 Subnets (Bỏ NAT Gateway để giảm chi phí) | ⬜ |
| 2 | Tạo 4 Security Groups (ALB, Backend, OCR, Database) | ⬜ |
| 3 | Tạo RDS PostgreSQL (Private Subnet) | ⬜ |
| 4 | Lưu DB credentials vào SSM Parameter Store | ⬜ |
| 5 | Tạo 2 ECR Repositories | ⬜ |
| 6 | Build & Push Backend Docker image | ⬜ |
| 7 | Build & Push OCR Docker image | ⬜ |
| 8 | Tạo ECS Cluster + Task Definition + Service (Fargate Spot) | ⬜ |
| 9 | Cấu hình ECS Auto Scaling (0→5 tasks) | ⬜ |
| 10 | Tạo Elastic Beanstalk environment (Docker + ALB) | ⬜ |
| 11 | Cấu hình EB environment variables | ⬜ |
| 12 | Deploy Backend lên EB | ⬜ |
| 13 | Verify SSM Parameter Store đầy đủ tất cả keys | ⬜ |
| 14 | Đăng ký domain + tạo SSL certificate (ACM) | ⬜ |
| 15 | Gắn certificate vào ALB + tạo DNS records | ⬜ |
| 16 | Cấu hình Amplify custom domain | ⬜ |
| 17 | Cập nhật CORS + VnPay ReturnUrl cho production | ⬜ |
| 18 | Tạo CloudWatch Alarms + SNS Topic | ⬜ |
| 19 | Tạo AWS Budget alert | ⬜ |
| 20 | **SMOKE TEST** — Upload hóa đơn end-to-end | ⬜ |

---

## SMOKE TEST SAU DEPLOY

Thực hiện test end-to-end theo thứ tự:

1. ✅ Truy cập `https://smartinvoice.vn` → Landing page load thành công
2. ✅ Đăng ký tài khoản mới → Cognito gửi email xác thực
3. ✅ Xác thực email → Đăng nhập thành công → JWT token hoạt động
4. ✅ Upload file XML hóa đơn → Invoice tạo thành công trong DB
5. ✅ Upload file ảnh (PNG/JPG) → SQS OCR queue nhận message → Fargate scale up → OCR xử lý → Invoice cập nhật status "Draft"
6. ✅ Check RiskLevel → VietQR validation xong → Yellow/Green hiển thị
7. ✅ Export Excel/PDF → File download từ S3 thành công
8. ✅ Thanh toán VnPay → Redirect đúng → Subscription upgrade thành công

## Verification Plan

### Automated Tests
- Không áp dụng — đây là tài liệu hướng dẫn deploy, không phải code changes.

### Manual Verification
- Sau khi deploy xong, thực hiện **Smoke Test** 8 bước ở trên để xác nhận toàn bộ hệ thống hoạt động end-to-end.
