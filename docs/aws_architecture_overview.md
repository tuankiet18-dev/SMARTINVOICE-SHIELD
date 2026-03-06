# Phân Tích Kiến Trúc Điện Toán Đám Mây AWS (SmartInvoice Shield)

Tài liệu này tổng hợp toàn bộ các dịch vụ AWS được sử dụng trong dự án SmartInvoice. Các thiết kế dưới đây đều tuân thủ chặt chẽ 3 tiêu chí trọng yếu của dự án: **Đa vùng sẵn sàng (Multi-AZ 2 zones)**, **Bảo mật lưu trữ**, và **Tối ưu chi phí**.

---

## 1. Amazon RDS for PostgreSQL (Cơ Sở Dữ Liệu)
- **Vai trò:** Lưu trữ toàn vẹn và có quan hệ toàn bộ dữ liệu cấu trúc (User, Company, Metadata hóa đơn, Audit Log).
- **Đáp ứng 2 AZ:** Được kích hoạt tùy chọn **Multi-AZ Deployment**. Nếu Zone 1 xảy ra sự cố phần cứng, AWS tự động chuyển đổi sang bảng sao lưu đồng bộ (Standby Replica) nằm ở Zone 2 trong vài chục giây mà không làm gián đoạn hay mất mát dữ liệu.
- **Lưu trữ bảo mật:** Máy chủ RDS được đặt ẩn toàn toàn trong nhánh **Private Subnet**, chặn dứt điểm các kết nối rác từ public internet. Dữ liệu trên ổ cứng lưu trữ tự động được Amazon mã hóa bằng chìa khóa bảo mật **KMS**.
- **Tối ưu chi phí:** Cấp phát các Instance dòng `t3` (như `db.t3.micro`) - đây là phiên bản tối ưu, cấp xung nhịp lớn khi hệ thống tải nặng, giúp tiết kiệm chi phí mà vẫn đảm bảo hiệu suất xử lý query.

## 2. Amazon S3 (Lưu Trữ File Hóa Đơn)
- **Vai trò:** Hầm chứa vĩnh cửu đối với các tài liệu tĩnh và dung lượng lớn (XML gốc, PDF hóa đơn, hình ảnh tải lên).
- **Đáp ứng 2 AZ:** Lưu trữ của S3 theo chuẩn Standard Class mặc định nhân bản và trải đều ra tối thiểu **3 AZs** trong khu vực, tính bền bỉ dữ liệu đạt ngưỡng 99.999999999% (11 số 9).
- **Lưu trữ bảo mật:** 
  - File đẩy lên sẽ được mã hóa chuẩn Quân đội **AES-256 (Server-Side Encryption)** trước khi hạ cánh xuống ổ lưu trữ. 
  - Áp dụng nguyên tắc **Block Public Access**, mọi yêu cầu đọc/ghi phải đi qua Backend .NET bằng IAM Credential.
- **Tối ưu chi phí:** Ứng dụng vòng đời thông minh (Lifecycle Configuration). Các hóa đơn cũ (> 3 tháng) sau khi kiểm toán tự động chuyển xuống lớp **Glacier Deep Archive** siêu rẻ (0.00099 USD/GB/tháng).

## 3. AWS Elastic Beanstalk & EC2 (Hosting Backend API)
- **Vai trò:** Môi trường vận hành cốt lõi vòng đời ứng dụng .NET Core API của dự án.
- **Đáp ứng 2 AZ:** Elastic Beanstalk quản lý Auto Scaling Group tự động duy trì số lượng tối thiểu *2 EC2 Instances*, rãi đều 1 node ở Subnet AZ-A và 1 node ở Subnet AZ-B để phân bổ rủi ro vật lý.
- **Tối ưu chi phí:** Thay vì chi đắt đỏ cho công nghệ NAT Gateway, các EC2 instances này được đặt ở mạng Public. Khóa chặt bảo mật hoàn toàn bằng hệ thống tường lửa **Security Groups** (Chỉ cho phép duy nhất Traffic hợp lệ từ ALB rẽ vào).

## 4. Application Load Balancer - ALB (Cân Bằng Tải)
- **Vai trò:** Nhận tín hiệu từ Internet (Frontend/Mobile/Nền tảng khác) để đẩy vào và giải quyết tắc nghẽn cho cụm Backend API.
- **Đáp ứng 2 AZ:** Tính năng **Cross-Zone Load Balancing** kích hoạt sẵn. Khi một AZ có máy chủ backend bị crash/quá tải, ALB biết để né trích dẫn traffic sang AZ bên cạnh ngay lập tức (Health checked).

## 5. Amazon Cognito (Xác Thực & Bảo Mật Người Dùng)
- **Vai trò:** Tổ hợp định danh (Identity Provider) chuyên nghiệp của dự án, xử lý Đăng ký/Đăng nhập, bảo quản Mật khẩu và Token/Session Access quản trị.
- **Lưu trữ bảo mật:** Backend API Không được phép chứa Mật khẩu của khách. AWS Cognito chịu trách nhiệm bảo vệ lõi danh tính khắt khe chuẩn bảo mật y tế/ngân hàng quốc tế (HIPAA / PCI DSS).
- **Tối ưu chi phí:** Free tier cực lớn, hoàn toàn miễn phí nếu dự án nằm quanh mốc 50k MAUs (Người dùng active hàng tháng).

## 6. AWS Systems Manager Parameter Store
- **Vai trò:** Két sắt nội bộ của hệ thống AWS giấu kín Connection String rds, mật khẩu, JWT secret khóa... 
- **Lưu trữ bảo mật:** Chống lại rủi ro Developer sơ suất push file [.env](file:///d:/Documents/code/CSharp/InvoiceManagement/SmartInvoice.API/.env) chứa mật khẩu lên Github. Ở Run-time khởi chạy, IAM Role của EC2 gọi API AWS tự động kéo biến SecureString mật xuống RAM phân giải chạy dự án.
- **Tối ưu chi phí:** Dịch vụ tiêu chuẩn (Standard params) được AWS tặng kèm miễn phí.

## 7. AWS Amplify (Frontend Hosting & Phân Phối)
- **Vai trò:** Môi trường Hosting hiện đại cho mã nguồn giao diện (React SPA / Vite CSS/JS build).
- **Mở rộng & Tốc độ:** Kết nối ngầm cực sâu với nền tảng Amazon CloudFront. Tự động mang file giao diện gửi tại kho mạng biên CDN gần người dùng cuối nhất (độ trễ vào trang siêu nhỏ).
- **Tối ưu chi phí:** Phí Data-in (đẩy source vào) miễn phí. Cước lưu trữ SPA không tốn kén (ít MBs). Rất rẻ giai đoạn Build.

## 8. Amazon ECS & AWS Fargate (OCR Service: PaddleOCR + VietOCR)
- **Vai trò:** Trái tim AI (Machine Learning) hỗ trợ API "soi" dữ liệu trích xuất từ file (PDF, PNG gốc). Module riêng được code bằng Python bọc lõi của dự án thư viện đỉnh cao như PaddleOCR, VietOCR.
- **Yêu cầu 2 AZ & Bảo mật:**
  - **Docker Container:** Đóng gói hoàn chỉnh service rườm rà Python vào ECR container Image.
  - Lõi AI này chứa thuật toán cốt lõi, bắt buộc phải nhốt kín bưng trong **Private Subnet** cùng nhà với RDS (Mô hình kín).
  - Không cho kết nối Public Internet. Khi Frontend muốn trích nội dung hóa đơn, sẽ nhờ Backend gọi điện cầu nối bằng Internal URL tới Fargate OCR.
- **Tối ưu chi phí:** 
  - AI đòi hỏi cấu hình máy nặng ram mạnh. Dùng **AWS Fargate** tức là không chạy máy ảo 24/7 (Serverless Compute). 
  - Khi nào Backend API nhờ đọc Hóa đơn thì Fargate mới cất công tính toán múc RAM. Trả tiền thật sát theo số mili-giây tài nguyên CPU thực tế nháy sinh lúc OCR chạy (Pay-As-You-Go). Không lãng phí 1 xu thuê Server tĩnh chạy nhàn rỗi.
