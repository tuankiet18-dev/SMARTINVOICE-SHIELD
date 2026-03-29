# Hướng Dẫn Triển Khai SmartInvoice Shield Trên AWS (End-to-End)

> **Tác giả**: SmartInvoice Shield Team  
> **Phiên bản**: 1.0  
> **Ngày cập nhật**: 27/03/2026  
> **Region**: `ap-southeast-1` (Singapore)

---

## Mục Lục

1. [Tổng Quan Kiến Trúc](#1-tổng-quan-kiến-trúc)
2. [Chọn Region](#2-chọn-region)
3. [Tạo VPC, Subnet, Security Group](#3-tạo-vpc-subnet-security-group)
4. [Tạo IAM Roles](#4-tạo-iam-roles)
5. [Tạo RDS PostgreSQL](#5-tạo-rds-postgresql)
6. [Tạo S3 Bucket](#6-tạo-s3-bucket)
7. [Tạo SQS Queue](#7-tạo-sqs-queue)
8. [Tạo ECR Repository](#8-tạo-ecr-repository)
9. [Triển khai AI Service trên ECS Fargate](#9-triển-khai-ai-service-trên-ecs-fargate)
10. [Triển khai Backend trên Elastic Beanstalk](#10-triển-khai-backend-trên-elastic-beanstalk)
11. [Cấu hình ALB và Target Group](#11-cấu-hình-alb-và-target-group)
12. [Tạo CloudFront (HTTPS cho Backend)](#12-tạo-cloudfront-https-cho-backend)
13. [Triển khai Frontend trên Amplify](#13-triển-khai-frontend-trên-amplify)
14. [Cấu hình CloudWatch Monitoring](#14-cấu-hình-cloudwatch-monitoring)
15. [Kiểm Tra End-to-End](#15-kiểm-tra-end-to-end)

---

## 1. Tổng Quan Kiến Trúc

```
┌──────────────┐     HTTPS      ┌──────────────┐     HTTPS      ┌──────────────┐
│   Frontend   │ ─────────────► │  CloudFront  │ ─────────────► │   Backend    │
│  (Amplify)   │                │   (CDN/SSL)  │                │    (EBS)     │
└──────────────┘                └──────────────┘                └──────┬───────┘
                                                                       │
                                              ┌────────────────────────┼────────────────────┐
                                              │                        │                    │
                                         ┌────▼─────┐           ┌─────▼────┐         ┌─────▼────┐
                                         │   RDS    │           │   S3     │         │   SQS    │
                                         │(Postgres)│           │(Storage) │         │ (Queue)  │
                                         └──────────┘           └──────────┘         └─────┬────┘
                                                                                           │
                                                                                     ┌─────▼────┐
                                                                                     │   ECS    │
                                                                                     │(OCR/AI)  │
                                                                                     └──────────┘
```

**Danh sách dịch vụ AWS sử dụng:**

| STT | Dịch vụ | Vai trò |
|-----|---------|---------|
| 1 | VPC | Mạng nội bộ ảo |
| 2 | IAM | Phân quyền dịch vụ |
| 3 | RDS | Cơ sở dữ liệu PostgreSQL |
| 4 | S3 | Lưu trữ file hóa đơn |
| 5 | SQS | Hàng đợi xử lý OCR |
| 6 | ECR | Lưu Docker Image |
| 7 | ECS Fargate | Chạy AI OCR Service |
| 8 | Elastic Beanstalk | Chạy .NET Backend API |
| 9 | CloudFront | HTTPS & CDN cho Backend |
| 10 | Amplify | Hosting Frontend React |
| 11 | CloudWatch | Giám sát & Cảnh báo |
| 12 | Cognito | Xác thực người dùng |
| 13 | SSM Parameter Store | Quản lý cấu hình |

---

## 2. Chọn Region

> **Region được chọn: `ap-southeast-1` (Singapore)**

**Lý do**:
- Gần Việt Nam nhất → độ trễ (latency) thấp nhất cho người dùng.
- Hỗ trợ đầy đủ tất cả dịch vụ cần thiết (ECS, EBS, RDS, Cognito, etc.)
- Chi phí hợp lý so với các region khác tại châu Á.

**Cách thiết lập:**
1. Đăng nhập vào **AWS Console**: https://console.aws.amazon.com
2. Ở góc **trên bên phải**, click vào tên Region hiện tại.
3. Chọn **Asia Pacific (Singapore) `ap-southeast-1`**.

> ⚠️ **LƯU Ý**: Tất cả các bước tiếp theo đều phải thực hiện trong Region `ap-southeast-1`. Hãy kiểm tra lại Region trước mỗi bước.

---

## 3. Tạo VPC, Subnet, Security Group

### 3.1. Tạo VPC

**Bước 1**: Vào **VPC Dashboard** → **Your VPCs** → **Create VPC**.

**Bước 2**: Điền thông tin:

| Trường | Giá trị |
|--------|---------|
| Name tag | `smartinvoice-vpc` |
| IPv4 CIDR block | `10.0.0.0/16` |
| IPv6 CIDR block | No IPv6 CIDR block |
| Tenancy | Default |

**Bước 3**: Click **Create VPC**.

### 3.2. Tạo Subnets

> Cần tối thiểu **2 subnet ở 2 Availability Zone (AZ) khác nhau** để RDS và ALB hoạt động (yêu cầu Multi-AZ).

#### Subnet 1 (Public - AZ a):

**Bước 1**: VPC Dashboard → **Subnets** → **Create subnet**.

| Trường | Giá trị |
|--------|---------|
| VPC | `smartinvoice-vpc` |
| Subnet name | `smartinvoice-public-subnet-1a` |
| Availability Zone | `ap-southeast-1a` |
| IPv4 CIDR block | `10.0.1.0/24` |

#### Subnet 2 (Public - AZ b):

| Trường | Giá trị |
|--------|---------|
| VPC | `smartinvoice-vpc` |
| Subnet name | `smartinvoice-public-subnet-1b` |
| Availability Zone | `ap-southeast-1b` |
| IPv4 CIDR block | `10.0.2.0/24` |

#### Subnet 3 (Private - AZ a) — cho RDS:

| Trường | Giá trị |
|--------|---------|
| VPC | `smartinvoice-vpc` |
| Subnet name | `smartinvoice-private-subnet-1a` |
| Availability Zone | `ap-southeast-1a` |
| IPv4 CIDR block | `10.0.3.0/24` |

#### Subnet 4 (Private - AZ b) — cho RDS:

| Trường | Giá trị |
|--------|---------|
| VPC | `smartinvoice-vpc` |
| Subnet name | `smartinvoice-private-subnet-1b` |
| Availability Zone | `ap-southeast-1b` |
| IPv4 CIDR block | `10.0.4.0/24` |

### 3.3. Tạo Internet Gateway

**Bước 1**: VPC Dashboard → **Internet Gateways** → **Create internet gateway**.

| Trường | Giá trị |
|--------|---------|
| Name tag | `smartinvoice-igw` |

**Bước 2**: Sau khi tạo xong, click **Actions** → **Attach to VPC** → Chọn `smartinvoice-vpc` → **Attach**.

**Bước 3**: Tab **Subnet associations** → **Edit subnet associations** → Chọn 2 Public Subnet (`1a` và `1b`) → **Save**.

### 3.5. Tạo NAT Gateway (Cho Private Subnets đi ra ngoài)

> **Lưu ý**: NAT Gateway phát sinh chi phí (~$32/tháng). Nếu chỉ demo, bạn có thể cân nhắc bỏ qua bước này và dùng Public Subnet.

**Bước 1**: VPC Dashboard → **NAT Gateways** → **Create NAT gateway**.

| Trường | Giá trị |
|--------|---------|
| Name | `smartinvoice-nat-gw` |
| Subnet | `smartinvoice-public-subnet-1a` (Phải nằm ở Public Subnet) |
| Connectivity type | Public |
| Elastic IP allocation ID | Click **Allocate Elastic IP** |

**Bước 2**: Click **Create NAT gateway**. Chờ trạng thái thành `Available`.

### 3.6. Tạo Route Table (Private)

**Bước 1**: VPC Dashboard → **Route Tables** → **Create route table**.

| Trường | Giá trị |
|--------|---------|
| Name | `smartinvoice-private-rt` |
| VPC | `smartinvoice-vpc` |

**Bước 2**: Chọn Route Table vừa tạo (`private-rt`) → Tab **Routes** → **Edit routes** → **Add route**:

| Destination | Target | Mô tả |
|-------------|--------|-------|
| `0.0.0.0/0` | `smartinvoice-nat-gw` | Cho phép Private Subnet ra internet qua NAT |

**Bước 3**: Tab **Subnet associations** → **Edit subnet associations** → Chọn 2 Private Subnet (`1a` và `1b`) → **Save**.

### 3.7. Bật Auto-assign Public IP cho Public Subnets

**Bước 3**: Lặp lại cho `smartinvoice-public-subnet-1b`.

### 3.8. Tạo VPC Endpoints (Tối ưu bảo mật & chi phí)

> Giúp các service trong Private Subnet kết nối trực tiếp với AWS Services mà không cần đi qua Internet/NAT Gateway.

**Bước 1**: VPC Dashboard → **Endpoints** → **Create endpoint**.

**Bước 2**: Tạo lần lượt các Endpoints sau:

| Service | Type | Name | Mục đích |
|---------|------|------|----------|
| `s3` | Gateway | `com.amazonaws.ap-southeast-1.s3` | Đọc/ghi hóa đơn |
| `ecr.api` | Interface | `com.amazonaws.ap-southeast-1.ecr.api` | Pull image (API) |
| `ecr.dkr` | Interface | `com.amazonaws.ap-southeast-1.ecr.dkr` | Pull image (Data) |
| `logs` | Interface | `com.amazonaws.ap-southeast-1.logs` | Gửi log lên CloudWatch |

*Lưu ý cho Gateway Endpoint (S3)*: Chọn VPC và chọn Route Table `smartinvoice-private-rt`.
*Lưu ý cho Interface Endpoints*: Chọn 2 Private Subnet và gắn Security Group cho phép port 443.

### 3.9. Tạo Security Groups

#### A. Security Group cho Backend (EBS):

**Bước 1**: VPC → **Security Groups** → **Create security group**.

| Trường | Giá trị |
|--------|---------|
| Name | `smartinvoice-backend-sg` |
| Description | SG cho Backend API |
| VPC | `smartinvoice-vpc` |

**Inbound Rules:**

| Type | Protocol | Port Range | Source | Mô tả |
|------|----------|------------|--------|--------|
| HTTP | TCP | 80 | `0.0.0.0/0` | Cho ALB health check |
| HTTPS | TCP | 443 | `0.0.0.0/0` | Traffic từ CloudFront |
| Custom TCP | TCP | 8080 | `0.0.0.0/0` | Port ứng dụng .NET |
| All Traffic | All | All | `smartinvoice-backend-sg` (self) | Giao tiếp nội bộ |

**Outbound Rules:**

| Type | Protocol | Port Range | Destination | Mô tả |
|------|----------|------------|-------------|--------|
| All Traffic | All | All | `0.0.0.0/0` | Cho phép gọi ra ngoài (S3, SQS, Cognito, OCR) |

#### B. Security Group cho Database (RDS):

| Trường | Giá trị |
|--------|---------|
| Name | `smartinvoice-rds-sg` |
| Description | SG cho PostgreSQL RDS |
| VPC | `smartinvoice-vpc` |

**Inbound Rules:**

| Type | Protocol | Port Range | Source | Mô tả |
|------|----------|------------|--------|--------|
| PostgreSQL | TCP | 5432 | `smartinvoice-backend-sg` | Chỉ cho phép Backend truy cập |

**Outbound Rules:**

| Type | Protocol | Port Range | Destination | Mô tả |
|------|----------|------------|-------------|--------|
| All Traffic | All | All | `0.0.0.0/0` | Mặc định |

#### C. Security Group cho OCR Service (ECS):

| Trường | Giá trị |
|--------|---------|
| Name | `smartinvoice-ocr-sg` |
| Description | SG cho Python OCR Service |
| VPC | `smartinvoice-vpc` |

**Inbound Rules:**

| Type | Protocol | Port Range | Source | Mô tả |
|------|----------|------------|--------|--------|
| Custom TCP | TCP | 5000 | `smartinvoice-backend-sg` | Chỉ cho phép Backend gọi vào port 5000 |

**Outbound Rules:**

| Type | Protocol | Port Range | Destination | Mô tả |
|------|----------|------------|-------------|--------|
| All Traffic | All | All | `0.0.0.0/0` | Tải model AI, gọi Gemini API |

---

## 4. Tạo IAM Roles

### 4.1. Tạo EC2 Role cho Elastic Beanstalk

> Role này gắn vào EC2 Instance để Backend có quyền truy cập S3, SQS, Cognito, etc.

**Bước 1**: Vào **IAM** → **Roles** → **Create role**.

**Bước 2**:
- Trusted entity type: **AWS service**
- Use case: **EC2**
- Click **Next**.

**Bước 3**: Tìm và chọn (tick) các policy sau:

| Policy | Mục đích |
|--------|----------|
| `AWSElasticBeanstalkWebTier` | Quyền cơ bản cho EBS |
| `AWSElasticBeanstalkMulticontainerDocker` | Chạy Docker trên EBS |
| `AmazonS3FullAccess` | Đọc/ghi file S3 |
| `AmazonSQSFullAccess` | Đọc/ghi SQS Queue |
| `AmazonCognitoPowerUser` | Quản lý Cognito Users |
| `AmazonSSMReadOnlyAccess` | Đọc Parameter Store |
| `AmazonEC2ContainerRegistryReadOnly` | Kéo Docker Image từ ECR |
| `CloudWatchLogsFullAccess` | Ghi log lên CloudWatch |

**Bước 4**: Role name: `aws-elasticbeanstalk-ec2-role` → **Create role**.

### 4.2. Tạo Service Role cho Elastic Beanstalk

**Bước 1**: IAM → Roles → Create role.

**Bước 2**:
- Trusted entity type: **AWS service**
- Use case: Chọn **Elastic Beanstalk** → `Elastic Beanstalk`
- Click **Next**.

**Bước 3**: AWS sẽ tự động gắn các policies:
- `AWSElasticBeanstalkEnhancedHealth`
- `AWSElasticBeanstalkManagedUpdatesCustomerRolePolicy`

**Bước 4**: Role name: `aws-elasticbeanstalk-service-role` → **Create role**.

### 4.3. Tạo ECS Task Execution Role

**Bước 1**: IAM → Roles → Create role.

**Bước 2**:
- Trusted entity type: **AWS service**
- Use case: **Elastic Container Service** → `Elastic Container Service Task`

**Bước 3**: Gắn policy:

| Policy | Mục đích |
|--------|----------|
| `AmazonECSTaskExecutionRolePolicy` | Pull image, ghi log |
| `CloudWatchLogsFullAccess` | Ghi log container lên CloudWatch |

**Bước 4**: Role name: `ecsTaskExecutionRole` → **Create role**.

---

## 5. Tạo RDS PostgreSQL

### 5.1. Tạo DB Subnet Group

**Bước 1**: Vào **RDS** → **Subnet groups** → **Create DB subnet group**.

| Trường | Giá trị |
|--------|---------|
| Name | `smartinvoice-db-subnet-group` |
| Description | Subnet group for SmartInvoice RDS |
| VPC | `smartinvoice-vpc` |
| Add subnets | Chọn **2 private subnets**: `smartinvoice-private-subnet-1a`, `smartinvoice-private-subnet-1b` |

**Bước 2**: Click **Create**.

### 5.2. Tạo Database Instance

**Bước 1**: RDS → **Databases** → **Create database**.

**Bước 2**: Cấu hình chi tiết:

| Trường | Giá trị |
|--------|---------|
| **Engine** | PostgreSQL |
| **Engine version** | PostgreSQL 16.x (bản mới nhất) |
| **Templates** | Free tier (nếu có) hoặc Dev/Test |
| **DB instance identifier** | `smartinvoice-db` |
| **Master username** | `postgres` |
| **Master password** | (Đặt password mạnh, ghi nhớ) |
| **DB instance class** | `db.t3.micro` (Free tier) hoặc `db.t3.small` |
| **Storage type** | General Purpose SSD (gp3) |
| **Allocated storage** | `20` GB |
| **Storage autoscaling** | ✅ Enable (Max: 100 GB) |
| **VPC** | `smartinvoice-vpc` |
| **DB subnet group** | `smartinvoice-db-subnet-group` |
| **Public access** | **No** |
| **VPC security group** | `smartinvoice-rds-sg` |
| **Database port** | `5432` |
| **Initial database name** | `SmartInvoiceDb` |

**Bước 3**: Click **Create database**. Chờ khoảng 5-10 phút để RDS khởi tạo xong.

**Bước 4**: Sau khi tạo xong, ghi lại **Endpoint** (ví dụ: `smartinvoice-db.xxxx.ap-southeast-1.rds.amazonaws.com`). Đây là địa chỉ HOST cần cấu hình trong Backend.

---

## 6. Tạo S3 Bucket

**Bước 1**: Vào **S3** → **Create bucket**.

| Trường | Giá trị |
|--------|---------|
| Bucket name | `smartinvoice-storage-{account-id}` (chọn tên duy nhất toàn cầu) |
| Region | `ap-southeast-1` |
| Object Ownership | ACLs disabled (recommended) |
| Block all public access | ✅ **Block all** (file chỉ truy cập qua Backend) |
| Bucket Versioning | Disable |
| Default encryption | SSE-S3 (mặc định) |

**Bước 2**: Click **Create bucket**.

> ⚠️ Không bật Public Access. File hóa đơn phải được truy cập thông qua Backend API (Presigned URL) để đảm bảo bảo mật.

---

## 7. Tạo SQS Queue

### 7.1. Tạo Dead Letter Queue (DLQ)

**Bước 1**: Vào **SQS** → **Create queue**.

| Trường | Giá trị |
|--------|---------|
| Type | Standard |
| Name | `smartinvoice-ocr-dlq` |
| Visibility timeout | `450` seconds |
| Message retention period | `14` days |
| Receive message wait time | `0` seconds |

**Bước 2**: Click **Create queue**.

### 7.2. Tạo Queue chính

**Bước 1**: SQS → **Create queue**.

| Trường | Giá trị |
|--------|---------|
| Type | Standard |
| Name | `smartinvoice-ocr-queue` |
| Visibility timeout | `450` seconds |
| Message retention period | `4` days |
| Receive message wait time | `20` seconds |
| **Dead-letter queue** | ✅ Enabled |
| Queue ARN (DLQ) | Chọn `smartinvoice-ocr-dlq` |
| Maximum receives | `3` |

**Bước 2**: Click **Create queue**.

**Bước 3**: Sau khi tạo xong, ghi lại **Queue URL** (ví dụ: `https://sqs.ap-southeast-1.amazonaws.com/212208750923/smartinvoice-ocr-queue`). Đây là giá trị cho biến `AWS_SQS_OCR_URL` trong Parameter Store.

---

## 8. Tạo ECR Repository

> ECR là nơi lưu trữ Docker Image để ECS và EBS có thể lấy về.

### 8.1. Tạo Repository cho Backend

**Bước 1**: Vào **ECR** → **Repositories** → **Create repository**.

| Trường | Giá trị |
|--------|---------|
| Visibility | Private |
| Repository name | `smartinvoice-api` |
| Image tag mutability | Mutable |
| Scan on push | Enabled |

**Bước 2**: Click **Create repository**.

### 8.2. Tạo Repository cho OCR Service

Lặp lại thao tác trên với:

| Trường | Giá trị |
|--------|---------|
| Repository name | `smartinvoice-ocr` |

### 8.3. Push Docker Image lên ECR (CLI)

**Bước 1**: Login vào ECR:
```bash
aws ecr get-login-password --region ap-southeast-1 | docker login --username AWS --password-stdin 212208750923.dkr.ecr.ap-southeast-1.amazonaws.com
```

**Bước 2**: Build và push Backend image:
```bash
cd SmartInvoice.API
docker build -t smartinvoice-api .
docker tag smartinvoice-api:latest 212208750923.dkr.ecr.ap-southeast-1.amazonaws.com/smartinvoice-api:latest
docker push 212208750923.dkr.ecr.ap-southeast-1.amazonaws.com/smartinvoice-api:latest
```

**Bước 3**: Build và push OCR image:
```bash
cd invoice_ocr
docker build -t smartinvoice-ocr .
docker tag smartinvoice-ocr:latest 212208750923.dkr.ecr.ap-southeast-1.amazonaws.com/smartinvoice-ocr:latest
docker push 212208750923.dkr.ecr.ap-southeast-1.amazonaws.com/smartinvoice-ocr:latest
```

---

## 9. Triển Khai AI Service Trên ECS Fargate

### 9.1. Tạo ECS Cluster

**Bước 1**: Vào **ECS** → **Clusters** → **Create cluster**.

| Trường | Giá trị |
|--------|---------|
| Cluster name | `smartinvoice-cluster` |
| Infrastructure | **AWS Fargate (Serverless)** |

**Bước 2**: Click **Create**.

### 9.2. Tạo Task Definition

**Bước 1**: ECS → **Task definitions** → **Create new task definition**.

**Bước 2**: Cấu hình Task:

| Trường | Giá trị |
|--------|---------|
| Task definition family | `smartinvoice-ocr-task` |
| Launch type | **AWS Fargate** |
| OS/Architecture | Linux/X86_64 |
| Task size - CPU | `1 vCPU` |
| Task size - Memory | `3 GB` |
| Task role | (để trống hoặc tạo role nếu cần truy cập S3) |
| Task execution role | `ecsTaskExecutionRole` |

**Bước 3**: Cấu hình Container:

| Trường | Giá trị |
|--------|---------|
| Container name | `ocr-container` |
| Image URI | `212208750923.dkr.ecr.ap-southeast-1.amazonaws.com/smartinvoice-ocr:latest` |
| Essential | ✅ Yes |
| Port mappings | `5000` (TCP) |

**Environment Variables:**

| Key | Value |
|-----|-------|
| `DEVICE` | `cpu` |
| `HOST` | `0.0.0.0` |
| `PORT` | `5000` |

**Log Configuration:**

| Trường | Giá trị |
|--------|---------|
| Log driver | `awslogs` |
| Log group | `/ecs/smartinvoice-ocr-task` |
| Region | `ap-southeast-1` |
| Stream prefix | `ecs` |

**Bước 4**: Click **Create**.

### 9.3. Tạo ECS Service

**Bước 1**: Vào **Clusters** → `smartinvoice-cluster` → Tab **Services** → **Create**.

| Trường | Giá trị |
|--------|---------|
| Launch type | **FARGATE** |
| Task definition (Family) | `smartinvoice-ocr-task` |
| Service name | `smartinvoice-ocr-task-service` |
| Desired tasks | `1` |

**Networking:**

| Trường | Giá trị |
|--------|---------|
| VPC | `smartinvoice-vpc` |
| Subnets | Chọn các **private subnets** (`1a` và `1b`) |
| Security groups | `smartinvoice-ocr-sg` |
| Auto-assign public IP | **TURNED OFF** (Bảo mật: Container dùng NAT GW hoặc Endpoints để ra ngoài) |

**Deployment:**

| Trường | Giá trị |
|--------|---------|
| Deployment type | Rolling update |
| Min running tasks % | `100` |
| Max running tasks % | `200` |

**Bước 4**: Click **Create**.

### 9.4. Cấu hình Service Auto Scaling (Xử lý song song)

Để hệ thống có thể xử lý hàng trăm hóa đơn cùng lúc, chúng ta cần cấu hình tự động tăng số lượng Task dựa trên SQS.

**Bước 1**: Trong ECS Service `smartinvoice-ocr-task-service` → Tab **Configuration** → **Service auto scaling** → **Update**.

**Bước 2**: Cấu hình Service Auto Scaling:
- **Min number of tasks**: `1`
- **Max number of tasks**: `10`
- **Scaling policy type**: `Target tracking` (Hoặc chọn **Step Scaling** với SQS).
- **Policy name**: `ScaleBySQS`

**Cách tối ưu nhất (Custom Metric với SQS)**:
1. Tạo **CloudWatch Alarm** dựa trên metric `ApproximateNumberOfMessagesVisible` của SQS `smartinvoice-ocr-queue`.
2. Nếu `MessagesVisible > 5` trong 1 phút → Trigger Auto Scaling tăng thêm 1-2 Task.
3. Nếu `MessagesVisible == 0` trong 5 phút → Giảm về 1 Task để tiết kiệm chi phí.

---

## 10. Triển Khai Backend Trên Elastic Beanstalk

### 10.1. Tạo Application

**Bước 1**: Vào **Elastic Beanstalk** → **Create application**.

| Trường | Giá trị |
|--------|---------|
| Application name | `Smartinvoice-api` |

### 10.2. Tạo Environment

**Bước 1**: Trong Application, chọn **Create environment**.

| Trường | Giá trị |
|--------|---------|
| Environment tier | **Web server environment** |
| Environment name | `Smartinvoice-api-env` |
| Platform | **Docker** |
| Platform branch | Docker running on 64bit Amazon Linux 2023 |

**Bước 2**: Chọn **Upload your code** → Upload file `Dockerrun.aws.json`:

```json
{
  "AWSEBDockerrunVersion": "1",
  "Image": {
    "Name": "212208750923.dkr.ecr.ap-southeast-1.amazonaws.com/smartinvoice-api:latest",
    "Update": "true"
  },
  "Ports": [
    {
      "ContainerPort": 8080,
      "HostPort": 80
    }
  ]
}
```

**Bước 3**: Cấu hình Service access:

| Trường | Giá trị |
|--------|---------|
| Service role | `aws-elasticbeanstalk-service-role` |
| EC2 key pair | (tạo hoặc chọn nếu cần SSH) |
| EC2 instance profile | `aws-elasticbeanstalk-ec2-role` |

**Bước 4**: Cấu hình Networking:

| Trường | Giá trị |
|--------|---------|
| VPC | `smartinvoice-vpc` |
| Public IP | ✅ Enabled |
| Instance subnets | Chọn **public subnets** |
| Instance security groups | `smartinvoice-backend-sg` |

**Bước 5**: Cấu hình Instance:

| Trường | Giá trị |
|--------|---------|
| Instance type | `t3.medium` (hoặc `t3.small`) |
| Root volume type | General Purpose SSD |
| Size | `20` GB |

**Bước 6**: Click **Submit** → Chờ khoảng 5-10 phút.

### 10.3. Cấu hình Environment Variables (qua SSM Parameter Store)

Backend sử dụng **AWS Systems Manager Parameter Store** để lấy cấu hình. Bạn cần tạo các parameter sau:

**Bước 1**: Vào **Systems Manager** → **Parameter Store** → **Create parameter**.

Tạo lần lượt các parameter sau (tất cả đều đặt prefix `/SmartInvoice/prod/`):

| Parameter name | Type | Value |
|----------------|------|-------|
| `/SmartInvoice/prod/POSTGRES_HOST` | String | `smartinvoice-db.xxxx.ap-southeast-1.rds.amazonaws.com` |
| `/SmartInvoice/prod/POSTGRES_PORT` | String | `5432` |
| `/SmartInvoice/prod/POSTGRES_DB` | String | `SmartInvoiceDb` |
| `/SmartInvoice/prod/POSTGRES_USER` | String | `postgres` |
| `/SmartInvoice/prod/POSTGRES_PASSWORD` | SecureString | (mật khẩu RDS) |
| `/SmartInvoice/prod/AWS_REGION` | String | `ap-southeast-1` |
| `/SmartInvoice/prod/COGNITO_USER_POOL_ID` | String | (ID User Pool) |
| `/SmartInvoice/prod/COGNITO_CLIENT_ID` | String | (ID App Client) |
| `/SmartInvoice/prod/COGNITO_CLIENT_SECRET` | SecureString | (Secret App Client) |
| `/SmartInvoice/prod/AWS_SQS_OCR_URL` | String | (URL Queue OCR) |
| `/SmartInvoice/prod/OCR_API_ENDPOINT` | String | `http://<ECS_PRIVATE_IP>:5000` |
| `/SmartInvoice/prod/AWS_S3_BUCKET_NAME` | String | `smartinvoice-storage-{account-id}` |
| `/SmartInvoice/prod/ALLOWED_ORIGINS` | String | `https://main.d3nvvjzg8ojoqd.amplifyapp.com` |

---

## 11. Cấu Hình ALB Và Target Group

> Elastic Beanstalk có thể tự tạo Application Load Balancer (ALB) nếu bạn chọn "Load balanced" khi tạo environment. Nếu không, bạn có thể tự tạo.

### 11.1. Tạo Target Group

**Bước 1**: Vào **EC2** → **Target Groups** → **Create target group**.

| Trường | Giá trị |
|--------|---------|
| Target type | Instances |
| Target group name | `smartinvoice-backend-tg` |
| Protocol / Port | HTTP / `80` |
| VPC | `smartinvoice-vpc` |
| Health check path | `/swagger/index.html` hoặc `/` |
| Healthy threshold | `3` |
| Unhealthy threshold | `3` |
| Interval | `30` seconds |

**Bước 2**: Register target → Chọn EC2 Instance của EBS → **Include as pending** → **Create target group**.

### 11.2. Tạo Application Load Balancer (nếu chưa có)

**Bước 1**: EC2 → **Load Balancers** → **Create Load Balancer** → **Application Load Balancer**.

| Trường | Giá trị |
|--------|---------|
| Name | `smartinvoice-alb` |
| Scheme | Internet-facing |
| IP address type | IPv4 |
| VPC | `smartinvoice-vpc` |
| Mappings | Chọn 2 AZ (1a, 1b) với public subnets |
| Security groups | `smartinvoice-backend-sg` |
| Listener - Protocol/Port | HTTP / 80 |
| Default action | Forward to `smartinvoice-backend-tg` |

**Bước 2**: Click **Create load balancer**.

### 11.3. Kết nối giữa các thành phần

```
Frontend (Amplify)
   │
   │ HTTPS (qua CloudFront)
   ▼
ALB (smartinvoice-alb)
   │
   │ HTTP :80
   ▼
EC2 Instance (EBS Backend, port 8080)
   │
   ├──► RDS PostgreSQL (port 5432, qua private subnet)
   ├──► S3 (qua AWS SDK, internal network)
   ├──► SQS (qua AWS SDK, internal network)
   └──► ECS OCR Service (port 5000, qua VPC internal IP)
```

**Cách Backend gọi OCR Service:**
- Backend gọi OCR qua **Private IP** của ECS Task (ví dụ: `http://10.0.1.xxx:5000`).
- Lấy Private IP: Vào **ECS** → Cluster → Service → Tasks → Click vào task → Copy **Private IP**.
- Cập nhật `OCR_API_ENDPOINT` trong Parameter Store.

---

## 12. Tạo CloudFront (HTTPS Cho Backend)

> CloudFront hoạt động như một lớp proxy HTTPS, giúp Frontend (HTTPS) có thể giao tiếp với Backend (HTTP) mà không bị lỗi Mixed Content.

### 12.1. Tạo Distribution

**Bước 1**: Vào **CloudFront** → **Create distribution**.

| Trường | Giá trị |
|--------|---------|
| **Origin domain** | DNS name của ALB (ví dụ: `smartinvoice-alb-xxxx.ap-southeast-1.elb.amazonaws.com`) |
| **Protocol** | HTTP only |
| **HTTP port** | `80` |
| **Name** | `smartinvoice-backend-origin` |

**Bước 2**: Default Cache Behavior:

| Trường | Giá trị |
|--------|---------|
| Viewer protocol policy | **Redirect HTTP to HTTPS** |
| Allowed HTTP methods | **GET, HEAD, OPTIONS, PUT, POST, PATCH, DELETE** |
| Cache policy | **CachingDisabled** (API không nên cache) |
| Origin request policy | **AllViewerExceptHostHeader** |

**Bước 3**: Settings:

| Trường | Giá trị |
|--------|---------|
| Price class | Use only North America and Europe (hoặc All edge locations) |
| WAF | Do not enable |

**Bước 4**: Click **Create distribution**. Chờ khoảng 5-15 phút để deploy.

**Bước 5**: Ghi lại **Distribution domain name** (ví dụ: `d3xxxxxxx.cloudfront.net`). Đây là URL API cho Frontend.

### 12.2. Cập nhật Frontend

Trong cấu hình Frontend (Amplify), chỉnh biến môi trường:
```
VITE_API_URL=https://d3xxxxxxx.cloudfront.net/api
```

---

## 13. Triển Khai Frontend Trên Amplify

### 13.1. Tạo App trên Amplify

**Bước 1**: Vào **AWS Amplify** → **Create new app**.

**Bước 2**: Chọn **GitHub** → Authorize AWS truy cập GitHub.

**Bước 3**:

| Trường | Giá trị |
|--------|---------|
| Repository | `tuankiet18-dev/SMARTINVOICE-SHIELD` |
| Branch | `main` |
| App name | `SmartInvoice-Frontend` |
| Monorepo | ✅ Tick nếu cần |
| Root directory | `SmartInvoice.Frontend` |

**Bước 4**: Build settings (Amplify thường tự nhận diện Vite):

```yaml
version: 1
frontend:
  phases:
    preBuild:
      commands:
        - npm ci
    build:
      commands:
        - npm run build
  artifacts:
    baseDirectory: dist
    files:
      - '**/*'
  cache:
    paths:
      - node_modules/**/*
```

### 13.2. Cấu hình Environment Variables

Trong Amplify → App settings → **Environment variables**:

| Variable | Value |
|----------|-------|
| `VITE_API_URL` | `https://d3xxxxxxx.cloudfront.net/api` |

### 13.3. Cấu hình Rewrites cho SPA

Amplify → App settings → **Rewrites and redirects** → Add rule:

| Source | Target | Type |
|--------|--------|------|
| `</^[^.]+$\|\.(?!(css\|gif\|ico\|jpg\|js\|png\|txt\|svg\|woff\|woff2\|ttf\|map\|json\|webp)$)([^.]+$)/>` | `/index.html` | 200 (Rewrite) |

> Điều này đảm bảo React Router hoạt động, không bị lỗi 404 khi refresh trang.

---

## 14. Cấu Hình CloudWatch Monitoring

### 14.1. Tạo SNS Topic (Kênh nhận thông báo)

**Bước 1**: Vào **SNS** → **Topics** → **Create topic**.

| Trường | Giá trị |
|--------|---------|
| Type | Standard |
| Name | `smartinvoice-alerts` |

**Bước 2**: Sau khi tạo → **Create subscription**:

| Trường | Giá trị |
|--------|---------|
| Protocol | Email |
| Endpoint | (email cá nhân của bạn) |

**Bước 3**: Mở email và click **Confirm Subscription**.

### 14.2. Tạo Alarm: SQS Queue Depth

**Bước 1**: Vào **CloudWatch** → **Alarms** → **Create alarm** → **Select metric**.

**Bước 2**: Chọn **SQS** → **Queue Metrics** → Tìm `smartinvoice-ocr-queue` → Chọn metric `ApproximateNumberOfMessagesVisible` → **Select metric**.

**Bước 3**: Cấu hình condition:

| Trường | Giá trị |
|--------|---------|
| Statistic | Maximum |
| Period | 5 minutes |
| Threshold type | Static |
| Condition | Greater than `10` |

**Bước 4**: Notification → In alarm → Chọn SNS Topic `smartinvoice-alerts`.

**Bước 5**: Alarm name: `SmartInvoice-SQS-QueueDepth-High` → **Create alarm**.

### 14.3. Tạo Alarm: DLQ Messages Present

Lặp lại quy trình trên với:

| Trường | Giá trị |
|--------|---------|
| Queue | `smartinvoice-ocr-dlq` |
| Metric | `ApproximateNumberOfMessagesVisible` |
| Condition | Greater than `0` |
| Alarm name | `SmartInvoice-SQS-DLQ-Messages-Present` |

### 14.4. Tạo Alarm: ECS OCR Service Down

**Bước 1**: CloudWatch → Create alarm → Select metric.

**Bước 2**: Chọn **ECS** → **ClusterName, ServiceName** → Tìm `smartinvoice-cluster` / `smartinvoice-ocr-task-service` → Metric `RunningTaskCount`.

| Trường | Giá trị |
|--------|---------|
| Statistic | Minimum |
| Period | 2 minutes |
| Condition | Less than `1` |
| Alarm name | `SmartInvoice-ECS-OCR-Service-Down` |

### 14.5. Tạo Alarm: SQS Oldest Message Age

| Trường | Giá trị |
|--------|---------|
| Queue | `smartinvoice-ocr-queue` |
| Metric | `ApproximateAgeOfOldestMessage` |
| Condition | Greater than `1800` (30 phút) |
| Alarm name | `SmartInvoice-SQS-OldestMessage-Age-High` |

### 14.6. Tạo Alarm: ECS CPU Overload

| Trường | Giá trị |
|--------|---------|
| Service | `smartinvoice-ocr-task-service` |
| Metric | `CPUUtilization` |
| Condition | Greater than `90` |
| Period | 5 minutes |
| Alarm name | `SmartInvoice-ECS-OCR-CPU-Overload` |

### 14.7. Tạo Alarm: RDS Storage Low

| Trường | Giá trị |
|--------|---------|
| DB Instance | `smartinvoice-db` |
| Metric | `FreeStorageSpace` |
| Condition | Less than `5368709120` (5 GB in bytes) |
| Alarm name | `SmartInvoice-RDS-Storage-Low` |

---

## 15. Kiểm Tra End-to-End

Sau khi hoàn tất triển khai, thực hiện các bước kiểm tra sau:

### 15.1. Kiểm tra Backend

```bash
# Health check
curl https://d3xxxxxxx.cloudfront.net/api/invoices/debug-config

# Kết quả mong đợi: JSON với HasSqsUrl = true
```

### 15.2. Kiểm tra Frontend

1. Truy cập URL Amplify: `https://main.d3nvvjzg8ojoqd.amplifyapp.com`
2. Đăng nhập bằng tài khoản thử nghiệm.
3. Kiểm tra trang Dashboard, xem danh sách hóa đơn.

### 15.3. Kiểm tra luồng OCR

1. Trên Frontend, chọn **Upload hóa đơn** → Upload 1 file ảnh.
2. Hóa đơn sẽ xuất hiện với trạng thái **Processing**.
3. Chờ 30-60 giây, refresh trang → Trạng thái chuyển sang **Pending** hoặc **Error**.
4. Kiểm tra CloudWatch Logs (`/ecs/smartinvoice-ocr-task`) để xem log OCR.

### 15.4. Kiểm tra CloudWatch Alarms

1. Vào **CloudWatch** → **Alarms**.
2. Xác nhận tất cả Alarms ở trạng thái **OK** (màu xanh).
3. Kiểm tra email xác nhận SNS đã được subscribe thành công.

---

> **Hoàn tất!** Hệ thống SmartInvoice Shield đã được triển khai thành công trên AWS. Nếu gặp sự cố, hãy kiểm tra CloudWatch Logs của từng service để xác định nguyên nhân.
