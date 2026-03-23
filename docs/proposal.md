# Đề Xuất Bổ Sung Kiến Trúc AWS cho Dự Án SmartInvoice Shield

**Kính gửi:** Leader Dự Án SmartInvoice Shield
**Ngày lập:** 20 Tháng 3, 2026

Dựa trên bản thiết kế kiến trúc sơ bộ (`AWS_ARCHITECTURE_PROPOSAL.md` và `aws_architecture_overview.md`), kiến trúc của ứng dụng hiện tại đã rất xuất sắc trong việc sử dụng các Managed Services và Serverless (Cognito, Elastic Beanstalk, S3 Lifecycle, ECS Fargate) để tinh gọn quá trình vận hành và tối ưu chi phí hạ tầng.

Tuy nhiên, nhằm nâng cấp độ an toàn dữ liệu, khả năng giám sát hệ thống mạng, độ ổn định và tính bảo mật của toàn bộ hệ thống ở một cấp độ chuyên nghiệp hơn (mà không hề làm tăng đáng kể chi phí hay độ phức tạp của nhóm). Dưới góc độ AWS Security & Operations, em đề xuất dự án nên bổ sung thêm **4 thành phần/cấu hình AWS** sau đây vào vòng đời triển khai (Tuần 7-Tuần 8):

---

## 1. Amazon Route 53 & AWS Certificate Manager (ACM)
- **Vấn đề cấu trúc hiện tại:** Elastic Beanstalk (ALB) và Amplify sẽ cấp phát cho chúng ta các đường dẫn domain do Amazon sinh ra tự động - rất khó nhớ và thiếu tính chuyên nghiệp để demo. Hơn nữa, API trên Beanstalk mặc định chưa được bọc HTTPS bằng chứng chỉ danh tính tin cậy.
- **Giá trị đem lại (Enhancement):**
  - **Route 53 (DNS):** Mảnh ghép cho phép map các tên miền tùy chỉnh (ví dụ: `api.smartinvoice.vn` và `app.smartinvoice.vn`) trỏ thẳng tới Frontend và Backend. Hệ thống còn có khả năng Routing (định tuyến) cực mượt để kiểm soát lưu lượng lúc nâng cấp ứng dụng.
  - **AWS ACM:** Cấp phát chứng chỉ SSL/TLS **miễn phí 100%** và tự động gia hạn mãi mãi. Khi ACM được đính vào Application Load Balancer (ALB), dữ liệu giao tiếp giữa phía Frontend User và Backend API (.NET) sẽ được mã hóa chuẩn HTTPS (In-Transit Encryption), bảo vệ tuyệt đối các payload JSON nhạy cảm liên quan tới hóa đơn.
- **Độ phức tạp / Effort team:** Rất thấp (Cấu hình qua giao diện AWS Console mất 15 phút).
- **Chi phí dự kiến:** Khoảng **$0.5 - $1/tháng** (Route53 thu phí duy trì $0.5/tháng cho 1 tên miền, lượng truy vấn rất rẻ; chứng chỉ ACM hoàn toàn miễn phí).

## 2. AWS WAF (Web Application Firewall)
- **Vấn đề cấu trúc hiện tại:** API (.NET) của chúng ta đang đứng sau ALB. Dù chúng ta có dùng Security Groups để chặn port rác, nhưng Security Groups không thể đọc được nội dung gói HTTP Request. Nếu bị kẻ gian gửi các gói tin tấn công hàm chứa lệnh SQL Injection hoặc Botnet nháy liên spam server, Backend vẫn phải đau đầu xử lý.
- **Giá trị đem lại (Enhancement):**
  - AWS WAF đóng vai trò như một **tấm khiên chắn thép (L7 Firewall)** túc trực ngay phía trước cổng Application Load Balancer.
  - Bằng cách bật tính năng **"AWS Managed Rules"**, Firewall sẽ tự động lọc, chặn và drop các kết nối có dấu hiệu tấn công lỗ hổng bảo mật phổ biến (OWASP Top 10) trước khi nó kịp chạm tới Server Backend. Giảm hẳn tải rác mệt mỏi cho Beanstalk, ngăn chặn nguy cơ làm quá tải hoặc rò rỉ cơ sở dữ liệu.
- **Độ phức tạp / Effort team:** Cực thấp so với việc tự filter ở cấp độ code Backend C#. Chỉ việc bật Rule là dùng chạy ngay.
- **Chi phí dự kiến:** Khoảng **$6 - $10/tháng** ($5 phí duy trì Web ACL, $1 cho bộ luật Managed Rule cơ bản, và $0.60/1 triệu requests. Rất rẻ để bảo vệ hệ thống so với thiệt hại rò rỉ dữ liệu).

## 3. Amazon CloudWatch Alarms & Amazon SNS
- **Vấn đề cấu trúc hiện tại:** Hiện tại hạ tầng Backend, RDS chưa có tính năng cảnh báo chủ động. Nếu Backend .NET bị nhồi tải đến sụp RAM, hoặc xuất hiện tràn lan lỗi 500 Internal, không một ai trong team 5 người biết được cho đến khi có người vào test giao diện và thấy app bị hỏng.
- **Giá trị đem lại (Enhancement):**
  - Đem lại tư duy DevOps **giám sát chủ động và phục hồi lỗi nhanh (Proactive Monitoring & Fault Tolerance)** cực kì chuyên nghiệp cho dự án sinh viên.
  - Cụ thể: Bố trí **CloudWatch Alarms** canh chừng các Metrics sống còn (ví dụ: CPU > 85%, hoặc tỷ lệ mã HTTP 500 liên tiếp cao). Khi chạm ngưỡng bất thường, CloudWatch lập tức đánh thức dịch vụ **SNS (Simple Notification Service)**. SNS ngay lập tức gửi luồng tin cảnh báo màu đỏ thẳng đến thư Email của cả team hoặc bắn vào kênh liên lạc Slack. DEV sẽ được thức giấc để vào sửa server trước khi khách hàng/giáo viên phát hiện lỗi.
- **Độ phức tạp / Effort team:** Thấp (Dễ dàng cấu hình kéo thả các rule cảnh báo trên AWS Console trong vòng 10 phút).
- **Chi phí dự kiến:** **$0/tháng** (Dưới ngưỡng Free Tier: CloudWatch miễn phí 10 Alarms và 5GB Logs cơ bản, SNS miễn phí 1.000 Email notifications mỗi tháng - hoàn toàn đủ cho team sinh viên).

## 4. RDS Automated Backups (Khắc phục thảm họa với Point-In-Time Recovery)
- **Vấn đề cấu trúc hiện tại:** Dù thiết kế hiện tại dùng tính năng RDS Multi-AZ để chống lại trường hợp xui xẻo gãy phần cứng máy ở AWS Zone 1 (Failover sang Zone 2). Nhưng bản thân Multi-AZ **không hề có chức năng bảo vệ CSDL khỏi lỗi logic (Human Error)**. Ví dụ: Kỹ sư Backend lỡ tay chạy nhầm lệnh SQL `DROP TABLE Users` trên Production. Ngay lập tức DB sẽ bị điêu đứng và xóa rớt cạn.
- **Giá trị đem lại (Enhancement):**
  - Việc đơn giản bật cơ chế **Automated Backups** sinh ra bản snapshot lưu CSDL và transaction logs liên tục.
  - Khi thảm họa "xóa nhầm" ập tới, tính năng **Point-In-Time Recovery (PITR)** của RDS cho phép bạn chỉ với 1 cú nhấp chuột "tua ngược kim đồng hồ" thiết lập lại Database nguyên vẹn trở về đúng thời điểm 1 phút cách đây bị xóa. Mang lại khả năng bảo lưu tối đa tài sản cốt lõi của ứng dụng (Dữ liệu Hoá đơn - Data Integrity).
- **Độ phức tạp / Effort team:** Cấu hình tự động lưu thông qua AWS Console ngay khi nhấn nút Create Database (Zero code).
- **Chi phí dự kiến:** **$0/tháng** (AWS hào phóng cấp miễn phí không gian lưu trữ backup bằng đúng 100% dung lượng ổ đĩa của Database hiện tại. Vì dữ liệu dự án không quá lớn nên chi phí đội thêm bằng 0).

---

### Kết luận & Lộ trình Tích hợp Đề Xuất
Các nâng cấp nêu trên hoàn toàn thuộc tầng Kiến Trúc Đám Mây Mạng - Tức là việc này **sẽ không đòi hỏi bất cứ thành viên code Frontend React hay Backend .NET phải sửa chữa lại code hay thay đổi dòng logic nào**. 

Có thể thêm nhẹ nhàng vào cấu hình hạ tầng trong Phase cuối của Tuần 7 - Tuần 8 đi kèm với quá trình Deploy môi trường:
- Ngay khi dựng Database, cài **RDS Automated Backups** và 1 chuông **CloudWatch + SNS** nhỏ báo vào Email leader.
- Cài **ACM & Route 53** khi kết nối DNS thành công sau khi App lên.
- Gắn khiên **AWS WAF** vào tuần 8 chốt sổ dự án demo để báo cáo tính chuyên nghiệp.

Kính mong Leader dự án xem xét mở rộng thêm 4 dịch vụ này vào bản Kiến Thiết Thực thi cuối cùng!
