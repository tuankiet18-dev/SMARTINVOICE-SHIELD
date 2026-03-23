# TÀI LIỆU KIẾN TRÚC AWS TỔNG THỂ - SMARTINVOICE SHIELD

**Dự án:** Smart Invoice - Phần mềm quản lý và rà soát rủi ro hóa đơn
**Mục tiêu thiết kế:** Đảm bảo hệ thống vận hành trơn tru cho luồng xử lý hóa đơn khối lượng lớn (tích hợp AI), tuân thủ nghiêm ngặt 3 tiêu chí: **Tối ưu chi phí (Cost-Optimization)**, **Đa vùng sẵn sàng (Multi-AZ)**, và **Bảo mật lưu trữ dữ liệu (Security)**.

**Công nghệ cốt lõi:** React TS (Frontend), .NET 9 (Backend API), Python/PaddleOCR/VietOCR (AI), PostgreSQL (Database).

---

## 1. SƠ ĐỒ KIẾN TRÚC TỔNG THỂ (EVENT-DRIVEN ARCHITECTURE)

```text
[Người Dùng / Kế Toán]
         │ (HTTPS)
         ▼
  ┌─────────────────────────────┐
  │ Route 53 (Domain) + ACM     │
  └──────────────┬──────────────┘
                 │
      ┌──────────┴──────────┐
      ▼                     ▼
┌───────────┐         ┌───────────┐
│  Amplify  │         │  Cognito  │
│(React TS) │         │ (JWT Auth)│
└─────┬─────┘         └─────┬─────┘
      │                     │
      └─────────┐ ┌─────────┘
                ▼ ▼
      ┌─────────────────────┐
      │ ALB (Load Balancer) │
      └─────────┬───────────┘
                ▼
      ┌─────────────────────┐
      │ Elastic Beanstalk   │
      │ (.NET 9 Web API)    │
      └────┬───────────┬────┘
           │           │
     ┌─────▼─────┐ ┌───▼───┐
     │ S3 Bucket │ │  RDS  │
     │(Files/PDF)│ │(PGSQL)│
     └───────────┘ └───────┘
           │           ▲
     ┌─────┴─────┐     │
     │ Amazon SQS├─────┘ (Update Status)
     │(Message Queue)
     └─────┬─────┘
           │
     ┌─────▼──────────────────┐
     │ ECS Fargate Spot       │
     │ (Python OCR - CPU Mode)│
     └────────────────────────┘
```

2. CHI TIẾT CÁC TẦNG DỊCH VỤ (SERVICE BREAKDOWN)
   A. Tầng Giao Diện & Điều Hướng (Frontend & Edge)
   AWS Amplify: Môi trường Hosting hiện đại cho mã nguồn giao diện React SPA. Tự động phân phối file qua mạng biên (CDN) giúp tăng tốc độ tải trang.

Amazon Route 53 & AWS Certificate Manager (ACM): Quản lý tên miền tùy chỉnh và cấp phát chứng chỉ SSL/TLS tự động. Toàn bộ giao tiếp giữa Frontend và Backend được đảm bảo mã hóa chuẩn HTTPS.

B. Tầng Xác Thực & Bảo Mật (Auth & Security)
Amazon Cognito: Tổ hợp định danh (Identity Provider) chuyên nghiệp xử lý đăng nhập, đăng ký và phát hành Token bảo mật. Backend .NET không cần lưu trữ mật khẩu người dùng, giảm thiểu tối đa rủi ro bảo mật.

AWS Systems Manager Parameter Store: Két sắt nội bộ lưu trữ Connection String và JWT Secret khóa. Chống lại rủi ro lộ mật khẩu trong mã nguồn.

VPC & Security Groups: Cơ sở dữ liệu (RDS) và lõi AI (Fargate) được đặt trong Private Subnet. Giao thông mạng bị khóa chặt, chỉ cho phép traffic hợp lệ đi qua Application Load Balancer (ALB).

C. Tầng Ứng Dụng Cốt Lõi (Core Backend)
AWS Elastic Beanstalk: Môi trường vận hành cốt lõi vòng đời ứng dụng .NET Core API.

Application Load Balancer (ALB): Nhận tín hiệu từ Internet và giải quyết tắc nghẽn cho cụm Backend API.

Auto Scaling: Duy trì tối thiểu 2 EC2 Instances (t3.micro), rải đều trên 2 Availability Zones (Multi-AZ 2 zones) để phân bổ rủi ro vật lý.

D. Tầng Xử Lý Bất Đồng Bộ (Asynchronous Event-Driven)
Amazon SQS (Simple Queue Service): Trạm trung chuyển đóng vai trò "giảm xóc" cho hệ thống. Backend API gửi file lên S3, đẩy tin nhắn vào SQS rồi phản hồi ngay cho người dùng, đảm bảo không bị quá tải khi nhiều người dùng upload cùng lúc.

Amazon ECS với AWS Fargate Spot (AI OCR Worker):

Module AI bọc lõi thư viện PaddleOCR và VietOCR.

Tối ưu hiệu năng & chi phí: Sử dụng Docker image siêu nhẹ (python:3.10-slim) chạy trên chế độ CPU để tương thích với Serverless Fargate. Chạy dưới dạng Fargate Spot tận dụng tài nguyên dư thừa của AWS, tối ưu cực độ chi phí. Tính phí theo từng mili-giây xử lý (Pay-As-You-Go).

Auto Scaling: Fargate tự động nhân bản (scale-out) số lượng container dựa trên số lượng tin nhắn ùn ứ trong SQS và tự động tắt hoàn toàn (Scale to Zero) khi không có yêu cầu.

E. Tầng Lưu Trữ & Cơ Sở Dữ Liệu (Storage & Database)
Amazon RDS for PostgreSQL:

Lưu trữ toàn vẹn dữ liệu cấu trúc (User, Company, Metadata hóa đơn).

Sử dụng instance db.t3.micro.

Bảo vệ dữ liệu bằng Automated Backups và Point-In-Time Recovery (PITR).

Amazon S3 & S3 Glacier:

Hầm chứa vĩnh cửu đối với tài liệu tĩnh (XML gốc, PDF, hình ảnh). Dữ liệu được mã hóa chuẩn Quân đội AES-256 (Server-Side Encryption).

S3 Lifecycle: Hóa đơn cũ (> 90 ngày) tự động chuyển xuống lớp Glacier Instant Retrieval để tiết kiệm tối đa chi phí lưu trữ.

F. Tầng Giám Sát & Quản Lý Chi Phí (Monitor & Governance)
Amazon CloudWatch & SNS: Thiết lập Alarms cảnh báo lỗi HTTP 500 hoặc CPU quá tải. Kết hợp Amazon SNS để gửi email cảnh báo chủ động cho toàn team xử lý sự cố.

AWS Budgets (Billing Alarm): Hệ thống tự động gửi cảnh báo khẩn cấp qua email nếu dự phóng chi phí hạ tầng AWS vượt quá ngân sách cho phép, ngăn chặn triệt để rủi ro phát sinh cước phí ngoài ý muốn.

3. LUỒNG XỬ LÝ HÓA ĐƠN TIÊU CHUẨN (HAPPY PATH)
   Upload: Người dùng xác thực qua Cognito, chọn tập tin (PDF/PNG) và tải lên qua giao diện Amplify.

Lưu trữ thô: Request được mã hóa HTTPS truyền qua ALB vào Backend API (.NET). API lưu file vào Amazon S3 (mã hóa AES-256).

Tạo Queue: Backend tạo một tin nhắn chứa URL S3 của file, đưa vào hàng đợi Amazon SQS và lập tức phản hồi "Đang xử lý" cho Frontend.

Kích hoạt AI: ECS Fargate Spot tự động scale up số lượng container dựa trên độ dài hàng đợi SQS. Các container kéo file từ S3 và chạy nhận diện quang học (OCR).

Cập nhật: Hoàn tất trích xuất dữ liệu, Fargate container cập nhật trực tiếp trạng thái và nội dung hóa đơn (JSON) vào RDS PostgreSQL.
