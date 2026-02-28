# Đề Xuất Kiến Trúc AWS - SmartInvoice Shield
**Phù hợp với Context: Team 5 người, Chưa có kinh nghiệm AWS, Thời gian 2 tháng, .NET 9 & React TS / Antd, Ưu tiên quản lý và tối ưu chi phí**

---

## 1. TỔNG QUAN KIẾN TRÚC TRÊN AWS
Với thời gian chỉ còn **2 tháng** và team **chưa có kinh nghiệm quản trị AWS**, nguyên tắc thiết kế tối thượng là: **Managed Services (PaaS/Serverless) + Giảm thiểu cấu hình Hạ tầng mạng (VPC/EC2)**. 

Tuyệt đối KHÔNG sử dụng các dịch vụ quá phức tạp như Amazon EKS (Kubernetes) hay tự build cụm EC2 từ đầu. Kiến trúc dưới đây tập trung vào sự Đơn giản - Hiệu quả - Tối ưu chi phí.

### Sơ đồ Kiến trúc (Architecture Diagram)

```text
[Người dùng / Kế toán]
       │
     ┌─┴─────────────────────────┐
     ▼                           ▼
 ┌───────────────┐       ┌───────────────┐
 │ AWS Cognito   │       │ AWS Amplify   │ ◄── [Frontend: React TS + Ant Design]
 └───────┬───────┘       └───────┬───────┘
 (Xác thực User)                 │ (REST API qua HTTPS, Token từ Cognito)
                                 ▼
                         ┌───────────────┐
                         │ AWS App Runner│ ◄── [Backend: .NET 9 Web API]
                         └───────┬───────┘     (Hoặc Elastic Beanstalk)
                                 │
    ┌────┼───────────────┬───────┴─────────┐
    ▼    ▼               ▼                 ▼
 ┌─────┐ ┌─────────┐   ┌───────────────┐   ┌───────────────┐
 │ RDS │ │ S3      │   │Internal API   │   │ Systems Mgr   │
 └─────┘ └─────────┘   └───────────────┘   └───────────────┘
  (DB)   (Storage)     (OCR model)         (Parameter Store)
```

---

## 2. CHI TIẾT CÁC DỊCH VỤ AWS ĐƯỢC LỰA CHỌN

### 2.0. Xác thực Người dùng (Authentication): Amazon Cognito
* **Thực trạng**: Mảnh ghép đã được team hoàn thiện xuất sắc.
* **Vai trò**: Quản lý Pool người dùng (User Pool), đăng nhập, đăng ký và phát hành JWT Token phục vụ API bảo mật. Không cần tự code luồng cấp phát/lưu trữ mật khẩu trong DB.

### 2.1. Frontend Hosting: AWS Amplify
* **Tech stack của bạn**: React TS, Ant Design, Vite.
* **Tại sao chọn**: 
  - Hoạt động như Vercel/Netlify của AWS. Chỉ cần kết nối với GitHub Repo là tự động build & deploy mỗi khi có code mới.
  - Tự động cung cấp HTTPS và CDN (phân phối nội dung nhanh).
  - Không cần biết cấu hình server. Rất thích hợp cho sinh viên thực tập.
* **Tối ưu chi phí**: Có Free Tier rộng rãi (15GB Bandwidth/tháng), demo gần như miễn phí.

### 2.2. Backend Hosting: AWS App Runner (Hoặc Elastic Beanstalk - Docker)
* **Tech stack của bạn**: .NET 9 API. (Team đã có Dockerfile/docker-compose).
* **Tại sao chọn AWS App Runner**:
  - Dành riêng cho ứng dụng đã được đóng gói bằng Container (Docker).
  - Hoàn toàn không cần lo lắng về Server, Load Balancer hay Auto-scaling. AWS tự quản lý 100%. Mức độ dễ sử dụng cao nhất cho backend.
* **Fallback (Phương án B - AWS Elastic Beanstalk)**: Nếu App Runner không có ở region mong muốn (vd: ap-southeast-1 đôi khi bị giới hạn), hãy dùng Elastic Beanstalk môi trường Docker. Nó cũng bọc EC2 lại và tự động hoá rất tốt cho người mới.

### 2.3. Database (Cơ sở dữ liệu): Amazon RDS cho PostgreSQL
* **Tech stack của bạn**: PostgreSQL.
* **Tại sao chọn**: Managed DB tự động backup, dễ dàng monitor. Không nên tự cài Postgres lên máy ảo EC2 vì rủi ro mất dữ liệu rất cao đối với team chưa có kinh nghiệm.
* **Tối ưu chi phí chạy giải**: Sử dụng instance `db.t3.micro` nằm trong **AWS Free Tier (750 giờ/tháng)**. Tắt Multi-AZ (vì đây là demo/thực tập, không cần chạy dự phòng quá tốn kém).

### 2.4. Lưu trữ Hóa đơn (Storage): Amazon S3 + S3 Lifecycle
* **Yêu cầu của bạn**: *Lưu trữ và quản lý hóa đơn tốt, TỐI ƯU CHI PHÍ.*
* **Tại sao chọn**:
  - Lưu mọi file hóa đơn (XML, PDF, PNG/JPG). Giới hạn lưu trữ là vô tận, API gắn kết với .NET 9 rất dễ (qua thư viện AWSSDK.S3).
* **Chiến lược TỐI ƯU CHI PHÍ CHUYÊN SÂU**: 
  - Khai báo **S3 Lifecycle Rules**. Hoá đơn 1-3 vạn cái sau một thời gian sẽ rất tốn kém. S3 Lifecycle cho phép cấu hình:
    - *0 - 90 ngày đầu*: Lưu ở S3 Standard (truy xuất tức thì, giá chuẩn).
    - *Sau 90 ngày*: Tự động chuyển data sang **Amazon S3 Glacier Instant Retrieval** hoặc **Glacier Deep Archive** (Giá rẻ hơn tới 90%, cực kì tối ưu cho hóa đơn cũ chỉ lưu để đối phó thanh tra).

### 2.5. Xử lý Trí tuệ Nhân tạo (AI): API OCR Nội bộ do team định nghĩa (PaddleOCR + VietOCR)
* **Đề bài**: Tích hợp module AI nhận diện để bóc tách thông tin tự động.
* **Tại sao chọn**:
  - Tận dụng nguồn lực chuyên biệt của team (có 1 thành viên AI chuyên trách).
  - Tối ưu được độ chính xác (Accuracy) cho văn bản tiếng Việt nhờ sử dụng bộ model VietOCR (Textract hỗ trợ kém).
  - Tránh được chi phí phát sinh tính theo số trang (per-page) của các giải pháp Cloud trả phí. Backend C# chỉ cần call tới 1 endpoint API nội bộ.

### 2.6. Quản lý cấu hình & Bảo mật: AWS Systems Manager (Parameter Store)
* **Tại sao chọn**:
  - Để lưu trữ chuỗi kết nối Database (DB Connection String), Secret Key cho JWT. Đừng hardcode trong source code!
  - Thay vì xài AWS Secrets Manager (tốn phí ~$0.4/secret/tháng), sinh viên có thể dùng **SSM Parameter Store** (Hoàn toàn MIỄN PHÍ) mà độ bảo mật vẫn chuẩn enterprise.

---

## 3. LỘ TRÌNH THỰC HIỆN DÀNH CHO TEAM 5 NGƯỜI (TRONG 8 TUẦN)

**Team Setup (Phân vai)**:
- 2 Bạn làm Frontend (React TS + Antd, UI/UX).
- 2 Bạn làm Backend & Database (.NET 9, EF Core, Postgres, tích hợp API ngoài).
- 1 Bạn làm AI & Cloud (Build mô hình OCR, tạo API Python và hỗ trợ S3/CI/CD).

**Timeline 2 Tháng**:
- **Tuần 1-2**: Xây dựng UI tĩnh bằng Ant Design. Backend thiết kế Db, viết API CRUD. Upload file cục bộ. Bạn AI bắt đầu setup PaddleOCR/VietOCR.
- **Tuần 3-4**: 
  - Cloud role: Thay thế local upload bằng AWS S3. Tích hợp IAM Policy cơ bản.
  - Backend role: Làm logic validate hoá đơn XML (cấu trúc).
- **Tuần 5-6**: Đỉnh cao AI Journey. Backend dùng thư viện `HttpClient` gọi HTTP POST đến endpoint API OCR của bạn AI (truyền file URL). Nhận lại JSON và map vào Database.
- **Tuần 7**: Triển khai hệ thống lên AWS. Frontend lên Amplify, Backend lên App Runner/Beanstalk. DB đưa lên RDS. API OCR lên một server/container riêng.
- **Tuần 8**: Load test, vá lỗi, làm file Slide & Report Demo.

---

## 4. TỔNG KẾT MỨC VƯỢT TRỘI CỦA KIẾN TRÚC NÀY
1. **Low Learning Curve**: Team không phải cấu hình Linux hay Kubernetes phức tạp cho môi trường Backend (.NET).
2. **True cost-optimization**: Tối ưu cực độ chi phí bằng S3 Lifecycle và System Manager thay vì dùng Secret Manager. Việc đổi sang Internal API thay vì dùng AWS Textract giúp xóa bỏ phí pay-per-use OCR.
3. **Thoả mãn Context AI**: Được tự tay rèn luyện và expose giải pháp AI chuyên biệt (PaddleOCR + VietOCR) chạy ngon lành cho văn bản tiếng Việt. Thể hiện năng lực Technical cao.
