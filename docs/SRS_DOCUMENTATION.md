# TÀI LIỆU ĐẶC TẢ YÊU CẦU PHẦN MỀM (SRS) - SMARTINVOICE SHIELD

> **Dự án**: SmartInvoice Shield (SIS)  
> **Phiên bản**: 1.0  
> **Trạng thái**: Draft  
> **Ngày lập**: 01/04/2026

---

## 1. GIỚI THIỆU (INTRODUCTION)

### 1.1. Mục đích (Purpose)
Tài liệu này đặc tả các yêu cầu nghiệp vụ, chức năng và phi chức năng cho hệ thống **SmartInvoice Shield (SIS)**. Hệ thống được thiết kế để tự động hóa quy trình quản lý hóa đơn đầu vào, trích xuất dữ liệu bằng AI (OCR) và đánh giá rủi ro hóa đơn dựa trên các tiêu chí của Tổng cục Thuế Việt Nam.

### 1.2. Phạm vi dự án (Scope)
Hệ thống SIS bao gồm các module chính:
- **Module OCR**: Trích xuất dữ liệu từ các định dạng XML, PDF, và hình ảnh.
- **Module Quản trị Rủi ro**: Phân tích rủi ro nhà cung cấp và tính hợp lệ của hóa đơn.
- **Module Tài chính**: Tích hợp thanh toán VnPay, tra cứu doanh nghiệp VietQR.
- **Module Báo cáo**: Phân tích dữ liệu, xuất file Excel/PDF và dự báo thuế VAT.
- **Module Quản lý**: Quản lý người dùng, phân quyền (RBAC), và đăng ký gói dịch vụ (SaaS).

### 1.3. Định nghĩa và Thuật ngữ (Definitions & Acronyms)
| Thuật ngữ | Định nghĩa |
|-----------|-----------|
| **XML** | Định dạng dữ liệu chuẩn của hóa đơn điện tử tại Việt Nam. |
| **OCR** | Optical Character Recognition - Nhận dạng ký tự quang học. |
| **MST** | Mã số thuế. |
| **VnPay** | Cổng thanh toán tích hợp trong hệ thống. |
| **VietQR** | Dịch vụ tra cứu thông tin doanh nghiệp và định danh ngân hàng. |
| **RBAC** | Role-Based Access Control - Quản lý truy cập dựa trên vai trò. |

---

## 2. MÔ TẢ TỔNG QUAN (OVERALL DESCRIPTION)

### 2.1. Phân cấp hệ thống (Product Perspective)
SmartInvoice Shield là một hệ thống SaaS độc lập, hoạt động trên nền tảng đám mây (AWS). Hệ thống tương tác với:
- **Tổng cục Thuế**: Tra cứu danh sách doanh nghiệp rủi ro (Blacklist).
- **Cổng thanh toán VnPay**: Xử lý giao dịch mua gói dịch vụ.
- **Dịch vụ VietQR/API bên thứ ba**: Xác thực thông tin doanh nghiệp.

### 2.2. Chức năng hệ thống (Product Functions)
1. **Xử lý Hóa đơn**: Tải lên, trích xuất, và lưu trữ hóa đơn điện tử.
2. **Đánh giá Rủi ro**: Tự động chấm điểm rủi ro cho từng hóa đơn theo 3 nhóm tiêu chí.
3. **Quản lý phê duyệt**: Quy trình hóa đơn từ "Chờ xử lý" -> "Nháp" -> "Đã phê duyệt".
4. **Phân tích Dashboard**: Biểu đồ phân tích chi phí và thuế suất theo thời gian.
5. **Thông báo**: Cảnh báo rủi ro qua hệ thống và email.

### 2.3. Các lớp người dùng (User Classes & Characteristics)
- **Kế toán (Accountant)**: Upload hóa đơn, kiểm tra dữ liệu OCR, đối soát rủi ro.
- **Quản trị Công ty (Company Admin)**: Phê duyệt hóa đơn, quản lý đội ngũ kế toán, quản lý gói dịch vụ.
- **Quản trị Hệ thống (System Admin)**: Quản lý cấu hình toàn hệ thống, danh mục rủi ro toàn cầu.

### 2.4. Môi trường vận hành (Operating Environment)
- **Frontend**: Hoạt động trên các trình duyệt hiện đại (Chrome, Edge, Safari).
- **Backend/Infrastructure**: Triển khai trên AWS ap-southeast-1, sử dụng Docker, .NET 9, PostgreSQL.

---

## 3. CÁC TÍNH NĂNG HỆ THỐNG (SYSTEM FEATURES)

### 3.1. Tính năng trích xuất dữ liệu bằng AI (AI-Powered OCR)
- **Mô tả**: Tự động nhận diện và trích xuất thông tin từ file ảnh/PDF hoặc phân tích file XML.
- **Yêu cầu chi tiết**:
  - Hỗ trợ tải lên đa tệp (Batch upload).
  - Sử dụng AI (Gemini/PaddleOCR) để trích xuất: Số hóa đơn, Ngày lập, MST người bán/mua, Danh sách hàng hóa, Tổng tiền, Tiền thuế.
  - Tự động kiểm tra tính toàn vẹn của chữ ký số (Voucher Validation).

### 3.2. Module Quản trị Rủi ro (Risk Assessment Framework)
Hệ thống thiết kế theo khung 3 tầng rủi ro của **Quyết định 78/QĐ-TCT**. Ở phiên bản Phase 1 hiện tại, dự án tập trung hoàn thiện Tầng 1:
1. **Nhóm I - Rủi ro Định tính (Đã triển khai)**: Tự động tra cứu mã số thuế qua API VietQR và đối soát danh sách doanh nghiệp rủi ro/bỏ trốn.
2. **Nhóm II - Rủi ro Định lượng (Roadmap)**: Phân tích sự bất thường về quy mô vốn và tần suất xuất hóa đơn.
3. **Nhóm III - Rủi ro Tham chiếu (Roadmap)**: Đánh giá quá trình tuân thủ thuế lịch sử từ cơ quan nhà nước.

### 3.3. Dashboard & Phân tích tài chính
- **Mô tả**: Cung cấp cái nhìn tổng quan về tình hình hóa đơn của doanh nghiệp.
- **Tính năng**:
  - Tổng hợp VAT đầu vào theo kỳ.
  - Phân tích chi phí theo nhà cung cấp lớn nhất.
  - Dự báo ngưỡng rủi ro bị cơ quan thuế thanh tra.

### 3.4. Quản lý Thanh toán & Gói dịch vụ (SaaS Management)
- **Tích hợp VnPay**: Thanh toán gia hạn các gói Free, Basic, Pro, Enterprise.
- **Quản lý Tenancy**: Mỗi công ty là một tenant riêng biệt, dữ liệu được cô lập hoàn toàn.

---

## 4. YÊU CẦU VỀ DỮ LIỆU (DATA REQUIREMENTS)

### 4.1. Cấu trúc Schema XML Hóa đơn Điện tử
Dựa trên cấu trúc chuẩn của Tổng cục Thuế Việt Nam, hệ thống bóc tách các trường:
- **`<DLHDon>`**: Dữ liệu hóa đơn chính.
- **`<TTChung>`**: Ký hiệu, mẫu số, số hóa đơn, ngày lập.
- **`<NBan>` / `<NMua>`**: Tên đơn vị, MST, địa chỉ, số tài khoản.
- **`<DSHHDVu>`**: Danh sách hàng hóa, chi tiết đơn giá, thành tiền, thuế suất.
- **`<TToan>`**: Tổng hợp số liệu thanh toán, tiền thuế theo từng loại thuế suất.
- **`<DSCKS>`**: Xác thực chữ ký số của người bán và Token cơ quan Thuế.

### 4.2. Cơ sở dữ liệu (Database Schema)
Hệ thống sử dụng PostgreSQL với các bảng chính:
- `Invoices`: Lưu trữ metadata và kết quả trích xuất.
- `Companies`: Thông tin tenant và cấu hình riêng.
- `Users` & `Roles`: Quản lý danh tính và phân quyền.
- `BlacklistedCompanies`: Danh sách doanh nghiệp rủi ro cục bộ và toàn cầu.
- `InvoiceCheckResults`: Lưu trữ kết quả kiểm tra tự động cho mỗi hóa đơn.

---

## 5. YÊU CẦU GIAO DIỆN BÊN NGOÀI (EXTERNAL INTERFACE REQUIREMENTS)

### 5.1. Giao diện người dùng (User Interface)
- Thiết kế theo phong cách Modern & Clean, tối ưu hóa cho trải nghiệm kế toán chuyên nghiệp.
- Dashboard trực quan với các biểu đồ Bar/Pie chart cho chi phí và rủi ro.
- Modal xem chi tiết hóa đơn hỗ trợ so sánh song song (Ảnh gốc vs Kết quả OCR).

### 5.2. Giao diện API (Software Interfaces)
- **RESTful API**: Cung cấp các endpoints cho Frontend và các hệ thống ERP tích hợp.
- **VnPay API**: Giao tiếp qua HMAC-SHA512 để đảm bảo an toàn giao dịch.
- **VietQR API**: Tra cứu thông tin ngân hàng và doanh nghiệp.

---

## 6. YÊU CẦU PHI CHỨC NĂNG (NON-FUNCTIONAL REQUIREMENTS)

### 6.1. Bảo mật (Security)
- Toàn bộ dữ liệu nhạy cảm lưu trong Private Subnet trên AWS.
- Mã hóa dữ liệu tĩnh (AES-256) trên S3 và RDS.
- Xác thực qua Amazon Cognito với JWT định danh.

### 6.2. Hiệu năng (Performance)
- Thời gian trích xuất OCR (không tính Gemini): < 5 giây/hóa đơn.
- Thời gian phản hồi API: < 500ms cho các request thông thường.
- Khả năng xử lý đồng thời: Tối thiểu 50 user truy cập dashboard cùng lúc.

### 6.3. Tuân thủ pháp lý (Compliance)
- Đáp ứng đầy đủ các tiêu chuẩn về lưu trữ và bảo quản hóa đơn điện tử theo **Circular 78/2021/TT-BTC**.

---

*Tài liệu này được soạn thảo bởi Antigravity AI Assistant.*
