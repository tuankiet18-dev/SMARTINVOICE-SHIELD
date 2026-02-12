# SMARTINVOICE SHIELD
## HỆ THỐNG QUẢN TRỊ & RÀ SOÁT RỦI RO HÓA ĐƠN ĐIỆN TỬ
### Tài liệu Dự án Chính thức - Production System

---

**Phiên bản**: 1.0 Production Edition  
**Ngày ban hành**: 06/02/2026  
**Tình trạng**: Official - Confidential  
**Cơ quan**: AWS First Cloud AI Journey Internship Program  
**Thời gian thực hiện**: 3 tháng (12 tuần)  
**Quy mô team**: 5 thành viên

---

**TUÂN THỦ QUY ĐỊNH PHÁP LÝ**:
- Nghị định 123/2020/NĐ-CP về hóa đơn, chứng từ
- Quyết định số 1550/QĐ-TCT về định dạng hóa đơn điện tử
- Thông tư 78/2021/TT-BTC hướng dẫn thực hiện

---

## 📑 MỤC LỤC

### PHẦN I: TỔNG QUAN DỰ ÁN
1. [Giới thiệu chung](#1-giới-thiệu-chung)
2. [Mục tiêu & Phạm vi](#2-mục-tiêu--phạm-vi)
3. [Định hướng phát triển](#3-định-hướng-phát-triển)
4. [Yêu cầu tuân thủ pháp lý](#4-yêu-cầu-tuân-thủ-pháp-lý)

### PHẦN II: THIẾT KẾ HỆ THỐNG
5. [Kiến trúc tổng thể](#5-kiến-trúc-tổng-thể)
6. [Thiết kế Database Production](#6-thiết-kế-database-production)
7. [Tech Stack & Services](#7-tech-stack--services)
8. [Security Architecture](#8-security-architecture)

### PHẦN III: TRIỂN KHAI
9. [Phân công Team 5 người](#9-phân-công-team-5-người)
10. [Timeline 12 tuần](#10-timeline-12-tuần)
11. [Quality Assurance](#11-quality-assurance)
12. [Deployment Strategy](#12-deployment-strategy)

### PHẦN IV: QUẢN LÝ DỰ ÁN
13. [Risk Management](#13-risk-management)
14. [Change Management](#14-change-management)
15. [Documentation Standards](#15-documentation-standards)

---

# PHẦN I: TỔNG QUAN DỰ ÁN

## 1. GIỚI THIỆU CHUNG

### 1.1 Bối cảnh dự án

**Vấn đề thực tế**:
```
Hiện trạng xử lý hóa đơn tại doanh nghiệp VN:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
⚠️ Nhập thủ công 100% dữ liệu từ hóa đơn giấy/PDF
⚠️ Thời gian xử lý: 5-10 phút/hóa đơn
⚠️ Tỷ lệ sai sót: 15-20% do nhập liệu thủ công
⚠️ Không phát hiện được hóa đơn giả, MST không hợp lệ
⚠️ Rủi ro pháp lý cao khi khai thuế với hóa đơn có vấn đề
⚠️ Không có hệ thống quản lý tập trung và truy vết
⚠️ Khó khăn trong việc tìm kiếm, tra cứu hóa đơn cũ
```

**Tác động kinh doanh**:
- Chi phí nhân công cao (kế toán viên làm việc thủ công)
- Rủi ro tài chính (phạt thuế khi dùng hóa đơn không hợp lệ)
- Thiếu minh bạch trong quản lý chi phí
- Không có cơ chế cảnh báo sớm về rủi ro

---

### 1.2 Giải pháp SmartInvoice Shield

**Định nghĩa sản phẩm**:

SmartInvoice Shield là hệ thống SaaS (Software as a Service) quản trị và rà soát rủi ro hóa đơn điện tử, cung cấp giải pháp toàn diện cho doanh nghiệp Việt Nam trong việc:

1. **Thu thập & Số hóa**: Upload và tự động trích xuất dữ liệu từ hóa đơn XML/PDF/Ảnh
2. **Rà soát & Kiểm tra**: Validate 3 lớp tuân thủ pháp luật (Cấu trúc/Chữ ký số/Nghiệp vụ)
3. **Quản lý & Lưu trữ**: Lưu trữ tập trung, phân quyền, audit trail đầy đủ
4. **Tìm kiếm & Báo cáo**: Tìm kiếm nhanh, dashboard analytics, export Excel
5. **Cảnh báo rủi ro**: Tự động phát hiện hóa đơn giả, MST không hợp lệ, sai lệch toán học

---

### 1.3 Giá trị cốt lõi (Core Value Proposition)

```
┌─────────────────────────────────────────────────────────────┐
│  "Cắt giảm 90% thời gian xử lý hóa đơn                      │
│   & Phát hiện 100% rủi ro pháp lý trước khi khai thuế"      │
└─────────────────────────────────────────────────────────────┘

✅ Tự động hóa: AI đọc hóa đơn thay con người
✅ Tuân thủ 100%: Validate theo Nghị định 123/2020/NĐ-CP
✅ An toàn: Anti-Spoofing, kiểm tra chữ ký số
✅ Minh bạch: Audit trail đầy đủ, không thể xóa lịch sử
✅ Thông minh: Cảnh báo rủi ro real-time
```

---

## 2. MỤC TIÊU & PHẠM VI

### 2.1 Mục tiêu dự án (SMART Goals)

**Mục tiêu kỹ thuật**:
```
S - Specific (Cụ thể):
  ✓ Xây dựng hệ thống SaaS production-ready
  ✓ Xử lý được 3 loại hóa đơn: XML/PDF/Ảnh
  ✓ Validate 3 lớp: Structure/Signature/Business Logic
  ✓ Multi-tenant: Hỗ trợ nhiều công ty (demo 2 companies)

M - Measurable (Đo lường được):
  ✓ AI accuracy ≥ 85% trên hóa đơn PDF/Ảnh
  ✓ API response time < 2s (p95)
  ✓ Database query < 500ms
  ✓ Uptime ≥ 99%
  ✓ Test coverage ≥ 80%

A - Achievable (Khả thi):
  ✓ Team 5 người x 3 tháng = 60 man-months
  ✓ Có code C# XML validation sẵn (30% công việc)
  ✓ AWS Free Tier support
  ✓ Tài liệu pháp lý đầy đủ

R - Relevant (Phù hợp):
  ✓ Giải quyết pain point thực tế của doanh nghiệp
  ✓ Tuân thủ quy định pháp luật VN
  ✓ Showcase AWS AI services (Textract)
  ✓ Portfolio project chất lượng cao

T - Time-bound (Thời hạn):
  ✓ Week 1-4: Foundation & Core Features
  ✓ Week 5-8: AI Integration & Testing
  ✓ Week 9-12: Polish & Deployment
  ✓ Deadline: End of Week 12
```

---

### 2.2 Phạm vi dự án (Scope)

#### ✅ TRONG PHẠM VI (In-Scope)

**A. Xử lý hóa đơn điện tử**
```
1. Hóa đơn GTGT (01GTKT) - Mẫu số 01
   ├─ Định dạng: XML theo Quyết định 1550/QĐ-TCT
   ├─ Bắt buộc: Chữ ký số, MST, Thuế GTGT
   └─ Validation: 3 lớp đầy đủ

2. Hóa đơn Bán hàng (02GTTT) - Mẫu số 02
   ├─ Định dạng: XML
   ├─ Không có: Thuế GTGT
   └─ Validation: 3 lớp

3. Hóa đơn Máy tính tiền (Cash Register)
   ├─ Đặc điểm: Có MCCQT (Mã Cơ quan thuế)
   ├─ Relaxed: Cho phép thiếu chữ ký người bán
   └─ Validation: Adjusted rules

4. Hóa đơn Ảnh/PDF (OCR)
   ├─ AWS Textract AnalyzeExpense
   ├─ Mapping về cấu trúc chuẩn
   └─ Risk: Yellow (thiếu XML pháp lý)
```

**B. Chức năng hệ thống**
```
1. Quản lý hóa đơn
   ├─ Upload (XML/PDF/JPG/PNG)
   ├─ Auto-extract data (AI + XML parser)
   ├─ Edit & Correct (Manual override)
   ├─ Submit for approval
   ├─ Approve/Reject workflow
   └─ Version control (lưu lịch sử sửa đổi)

2. Rà soát rủi ro (3-Layer Validation)
   ├─ Layer 1: XSD Structure validation
   ├─ Layer 2: Digital signature verification + Anti-Spoofing
   ├─ Layer 3: Business logic (MST API, Math, Mandatory fields)
   └─ Risk scoring: Green/Yellow/Orange/Red

3. Lưu trữ & Tìm kiếm
   ├─ Full-text search (Elasticsearch hoặc PostgreSQL FTS)
   ├─ Filter nâng cao (Date range, Status, Risk level, Type)
   ├─ Sort & Pagination
   └─ Quick search by Invoice#, MST, Company name

4. Báo cáo & Xuất file
   ├─ Dashboard analytics (Charts, KPIs)
   ├─ Export Excel (MISA/FAST format)
   ├─ Export PDF report
   └─ Scheduled reports (optional)

5. Phân quyền & Audit
   ├─ Multi-tenant isolation
   ├─ Role-based access (Member/Admin/SuperAdmin)
   ├─ Audit trail đầy đủ (WHO/WHAT/WHEN/WHY)
   └─ Immutable logs (không thể xóa)
```

**C. Tích hợp AWS Services**
```
✓ Amazon S3: File storage
✓ Amazon Textract: OCR AI
✓ Amazon RDS PostgreSQL: Database
✓ AWS Elastic Beanstalk: Backend hosting
✓ AWS Amplify: Frontend hosting
✓ AWS Secrets Manager: Credentials
✓ Amazon CloudWatch: Monitoring & Logs
✓ AWS IAM: Access control
```

---

#### ❌ NGOÀI PHẠM VI (Out-of-Scope)

```
× Tích hợp trực tiếp với phần mềm kế toán (MISA/FAST API)
  → Chỉ export Excel tương thích

× Mobile app (iOS/Android)
  → Chỉ web responsive

× Blockchain verification
  → Chỉ digital signature verification

× Real-time collaboration (Google Docs style)
  → Chỉ audit trail

× Payment gateway integration
  → Không xử lý thanh toán

× Advanced ML training (custom model)
  → Chỉ dùng pre-trained Textract

× Multi-language support
  → Chỉ tiếng Việt + English UI (optional)
```

---

## 3. ĐỊNH HƯỚNG PHÁT TRIỂN

### 3.1 Design Philosophy (Triết lý thiết kế)

Dự án được xây dựng dựa trên 3 trụ cột:

#### **Trụ cột 1: Tuân thủ & Tự động (Compliance & Automation)**
```
Mục tiêu: Đảm bảo 100% tuân thủ pháp luật VN về hóa đơn điện tử

Nguyên tắc:
✓ Mọi validation rule phải có cơ sở pháp lý rõ ràng
✓ Không bỏ qua bất kỳ trường bắt buộc nào theo luật
✓ Chữ ký số phải được verify 100% (nếu có)
✓ Anti-Spoofing: MST người ký phải khớp MST người bán
✓ Cảnh báo rõ ràng khi vi phạm quy định

Implementation:
- InvoiceProcessor.cs đã có sẵn logic tuân thủ
- ValidationLayers table lưu kết quả từng lớp
- RiskReasons (JSONB) giải thích rõ từng warning/error
```

#### **Trụ cột 2: Cloud Native & Scalable (Mở rộng dễ dàng)**
```
Mục tiêu: Kiến trúc sẵn sàng cho production, dễ scale

Nguyên tắc:
✓ Stateless API (horizontal scaling)
✓ Database connection pooling
✓ S3 cho file storage (unlimited)
✓ Managed services (RDS, Beanstalk) giảm ops overhead
✓ Monitoring & Alerting từ đầu

Implementation:
- .NET Core API (stateless, async/await)
- PostgreSQL với indexes tối ưu
- S3 lifecycle policies (auto archive)
- CloudWatch metrics & alarms
```

#### **Trụ cột 3: User Experience & Transparency (Minh bạch)**
```
Mục tiêu: Giảm tải nhận thức, tăng độ tin cậy

Nguyên tắc:
✓ Hệ thống tự động phân loại → User chỉ cần confirm
✓ Cảnh báo rõ ràng, dễ hiểu (không dùng thuật ngữ kỹ thuật)
✓ Audit trail đầy đủ → Truy vết 100% thay đổi
✓ Immutable logs → Không ai xóa được lịch sử
✓ Dashboard trực quan → KPI một cái nhìn

Implementation:
- Risk badges color-coded (Green/Yellow/Orange/Red)
- Audit log timeline UI (WHO did WHAT, WHEN)
- Dashboard với Recharts visualization
- User-friendly error messages
```

---

### 3.2 Technical Principles (Nguyên tắc kỹ thuật)

```
1. SEPARATION OF CONCERNS
   ├─ Controllers: Nhận request, validate input
   ├─ Services: Business logic
   ├─ Repositories: Database access
   └─ Models: Data structures

2. DEPENDENCY INJECTION
   ├─ Loose coupling
   ├─ Testable code
   └─ Easy to mock

3. ASYNC/AWAIT EVERYWHERE
   ├─ Non-blocking I/O
   ├─ Better performance
   └─ Scalable

4. FAIL FAST
   ├─ Validate early
   ├─ Return clear errors
   └─ Don't hide exceptions

5. AUDIT EVERYTHING
   ├─ Every state change → Audit log
   ├─ WHO did WHAT
   └─ Immutable records

6. SECURITY BY DEFAULT
   ├─ JWT authentication
   ├─ HTTPS only
   ├─ Parameterized queries (prevent SQL injection)
   ├─ Input validation
   └─ CORS properly configured
```

---

## 4. YÊU CẦU TUÂN THỦ PHÁP LÝ

### 4.1 Nghị định 123/2020/NĐ-CP

**Điều 12: Nội dung hóa đơn điện tử**

Hệ thống phải lưu trữ đầy đủ các trường theo quy định:

```
A. THÔNG TIN NGƯỜI BÁN (NBan)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Bắt buộc:
✓ Tên người bán (Ten)
✓ Mã số thuế (MST)
✓ Địa chỉ (DChi)

Không bắt buộc nhưng nên có:
○ Số điện thoại (SDT)
○ Số tài khoản ngân hàng (STKNHang)
○ Địa chỉ thư điện tử (DCTDTu)

B. THÔNG TIN NGƯỜI MUA (NMua)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Bắt buộc (nếu có MST):
✓ Tên người mua (Ten)
✓ Mã số thuế (MST)
✓ Địa chỉ (DChi)

Không bắt buộc:
○ Họ tên người mua hàng (HVTNMHang)
○ Số điện thoại (SDT)

C. THÔNG TIN CHUNG (TTChung)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Bắt buộc:
✓ Phiên bản (PBan): "2.0.0"
✓ Loại hóa đơn (THDon): "1" (GTGT) / "2" (Bán hàng)
✓ Ký hiệu mẫu số (KHMSHDon): VD "01GTKT"
✓ Ký hiệu hóa đơn (KHHDon): VD "C24T"
✓ Số hóa đơn (SHDon): "0001234"
✓ Ngày lập (NLap): "2025-01-15"
✓ Đơn vị tiền tệ (DVTTe): "VND"

Không bắt buộc:
○ Mã của cơ quan thuế (MCCQT) - Bắt buộc với máy tính tiền
○ Hình thức thanh toán (HTTToan)
○ Ghi chú (GChu)

D. DANH SÁCH HÀNG HÓA (DSHHDVu)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Mỗi dòng hàng (HHDVu) bắt buộc:
✓ Tính chất (TChat): "1" (hàng hóa) / "2" (dịch vụ) / "3" (khuyến mại)
✓ Số thứ tự (STT)
✓ Tên hàng hóa (Ten)
✓ Đơn vị tính (DVTinh)
✓ Số lượng (SLuong)
✓ Đơn giá (DGia)
✓ Thành tiền (ThTien)

Nếu là GTGT:
✓ Thuế suất (TSuat): "0", "5", "8", "10"
✓ Tiền thuế (TThue)

E. TỔNG TIỀN (TToan)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Bắt buộc:
✓ Tổng tiền chưa thuế (TgTCThue)
✓ Tổng tiền thuế GTGT (TgTThue) - Nếu là GTGT
✓ Tổng thanh toán bằng số (TgTTTBSo)
✓ Tổng thanh toán bằng chữ (TgTTTBChu)
```

**→ Hệ thống PHẢI lưu trữ tất cả các trường trên trong database**

---

### 4.2 Quyết định 1550/QĐ-TCT

**Về định dạng file XML**:

```xml
<!-- Cấu trúc chuẩn theo 1550/QĐ-TCT -->
<HDon>
  <DLHDon>
    <TTChung>
      <!-- Thông tin chung -->
    </TTChung>
    <NDHDon>
      <NBan>
        <!-- Người bán -->
      </NBan>
      <NMua>
        <!-- Người mua -->
      </NMua>
      <DSHHDVu>
        <HHDVu>
          <!-- Danh sách hàng hóa -->
        </HHDVu>
      </DSHHDVu>
      <TToan>
        <!-- Tổng tiền -->
      </TToan>
    </NDHDon>
  </DLHDon>
  <DSCKS>
    <NBan>
      <Signature>
        <!-- Chữ ký số người bán -->
      </Signature>
    </NBan>
  </DSCKS>
</HDon>
```

**Yêu cầu chữ ký số** (Điều 17 NĐ 123/2020):
- Bắt buộc với GTGT và Bán hàng
- Chứng thư số phải còn hiệu lực
- MST trong Subject phải khớp MST người bán
- Sử dụng thuật toán: RSA-SHA256 (phổ biến nhất)

---

### 4.3 Validation Rules theo Pháp luật

Hệ thống implement các rule sau:

```
LAYER 1: STRUCTURE VALIDATION (XSD)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
✓ XML well-formed
✓ Tuân thủ InvoiceSchema.xsd
✓ Các trường bắt buộc không được thiếu
✓ Kiểu dữ liệu đúng (date, decimal, string)
→ Nếu fail: RISK = RED

LAYER 2: SIGNATURE VALIDATION
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
✓ Chữ ký số hợp lệ (integrity check)
✓ Chứng thư số chưa hết hạn (optional - cần cert chain)
✓ Anti-Spoofing: MST trong cert Subject = MST người bán
→ Nếu missing signature (GTGT/Bán hàng): RISK = YELLOW
→ Nếu signature invalid: RISK = ORANGE
→ Nếu Anti-Spoofing detected: RISK = RED

LAYER 3: BUSINESS LOGIC VALIDATION
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
A. Kiểm tra MST (Mã số thuế)
   ✓ Định dạng 10 số: Checksum Mod-11
   ✓ Định dạng 13 số: 10 số đầu + "-NNN"
   ✓ Định dạng 12 số (CCCD): Chỉ check numeric
   ✓ Tra cứu VietQR API: Doanh nghiệp có hoạt động?
   → Nếu invalid format: RISK = ORANGE
   → Nếu inactive business: RISK = RED

B. Kiểm tra toán học
   ✓ SLuong × DGia = ThTien (tolerance ±10 VND)
   ✓ Nếu GTGT: TgTCThue + TgTThue = TgTTTBSo
   ✓ Nếu Bán hàng: TgTCThue = TgTTTBSo
   → Nếu sai lệch: RISK = ORANGE

C. Kiểm tra ngày tháng
   ✓ NLap không được ở tương lai
   ✓ NLap phải có định dạng YYYY-MM-DD
   → Nếu future date: RISK = ORANGE

D. Kiểm tra đặc thù theo loại
   ✓ Máy tính tiền: Phải có MCCQT
   ✓ GTGT: Phải có TSuat và TThue
   → Nếu thiếu: RISK = YELLOW/ORANGE
```

---

## 5. KIẾN TRÚC TỔNG THỂ

### 5.1 System Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                    CLIENT LAYER                                  │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  React SPA (TypeScript + Material-UI)                    │   │
│  │  - Login/Auth pages                                      │   │
│  │  - Invoice Management (List/Detail/Upload/Edit)          │   │
│  │  - Dashboard & Analytics                                 │   │
│  │  - Admin panel (Approval queue, User management)         │   │
│  │                                                           │   │
│  │  Hosted on: AWS Amplify                                  │   │
│  └──────────────────────────────────────────────────────────┘   │
└───────────────────────────┬─────────────────────────────────────┘
                            │ HTTPS (REST API + JWT)
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│                    API LAYER                                     │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  .NET 6 Web API (C#)                                     │   │
│  │  ┌────────────────────────────────────────────────────┐  │   │
│  │  │  Controllers                                       │  │   │
│  │  │  ├─ AuthController (Login, Register, RefreshToken)│  │   │
│  │  │  ├─ InvoiceController (CRUD, Validate, Submit)    │  │   │
│  │  │  ├─ DashboardController (Stats, Charts)           │  │   │
│  │  │  ├─ ExportController (Excel, PDF)                 │  │   │
│  │  │  └─ AdminController (User management, Config)     │  │   │
│  │  └────────────────────────────────────────────────────┘  │   │
│  │                                                           │   │
│  │  ┌────────────────────────────────────────────────────┐  │   │
│  │  │  Services (Business Logic)                         │  │   │
│  │  │  ├─ InvoiceProcessorService (3-layer validation)  │  │   │
│  │  │  ├─ TextractService (AWS Textract integration)    │  │   │
│  │  │  ├─ S3Service (File upload/download)              │  │   │
│  │  │  ├─ ValidationService (Risk calculation)          │  │   │
│  │  │  ├─ SearchService (Full-text search)              │  │   │
│  │  │  ├─ ExportService (Excel generation)              │  │   │
│  │  │  └─ NotificationService (Email/In-app alerts)     │  │   │
│  │  └────────────────────────────────────────────────────┘  │   │
│  │                                                           │   │
│  │  ┌────────────────────────────────────────────────────┐  │   │
│  │  │  Repositories (Data Access)                        │  │   │
│  │  │  ├─ IInvoiceRepository                             │  │   │
│  │  │  ├─ IUserRepository                                │  │   │
│  │  │  ├─ IAuditLogRepository                            │  │   │
│  │  │  └─ Generic Repository Pattern                     │  │   │
│  │  └────────────────────────────────────────────────────┘  │   │
│  │                                                           │   │
│  │  Hosted on: AWS Elastic Beanstalk                        │   │
│  └──────────────────────────────────────────────────────────┘   │
└───────────┬─────────────┬──────────────┬────────────────────────┘
            │             │              │
            ▼             ▼              ▼
┌──────────────┐  ┌──────────────┐  ┌──────────────────┐
│   AWS RDS    │  │    AWS S3    │  │  AWS Textract    │
│  PostgreSQL  │  │  File Bucket │  │  AnalyzeExpense  │
│              │  │              │  │                  │
│ - Invoices   │  │ - XML files  │  │ - OCR PDF/Image  │
│ - Users      │  │ - PDF files  │  │ - Extract fields │
│ - AuditLogs  │  │ - Images     │  │                  │
│ - 12 tables  │  │ - Exports    │  │                  │
└──────────────┘  └──────────────┘  └──────────────────┘
            ▲
            │
            ▼
┌────────────────────────────────────────┐
│     EXTERNAL APIS                      │
│  ┌──────────────────────────────────┐  │
│  │  VietQR API (MST Verification)   │  │
│  │  https://api.vietqr.io/v2/...    │  │
│  └──────────────────────────────────┘  │
└────────────────────────────────────────┘
```

---

### 5.2 Data Flow Architecture

**Flow 1: XML Invoice Processing**
```
┌─────────┐
│  User   │
│ Upload  │
│  XML    │
└────┬────┘
     │
     ▼
┌─────────────────────────┐
│  1. Upload to S3        │
│     - Validate file ext │
│     - Generate S3 key   │
│     - Upload async      │
└────────┬────────────────┘
         │
         ▼
┌────────────────────────────────────┐
│  2. Create Invoice record (Draft) │
│     - Save metadata to DB          │
│     - Status = "Draft"             │
│     - ProcessingMethod = "XML"     │
└────────┬───────────────────────────┘
         │
         ▼
┌──────────────────────────────────────┐
│  3. InvoiceProcessorService          │
│     ├─ Download XML from S3          │
│     ├─ Layer 1: XSD Validation       │
│     ├─ Layer 2: Signature Check      │
│     └─ Layer 3: Business Logic       │
└────────┬─────────────────────────────┘
         │
         ▼
┌──────────────────────────────────────┐
│  4. Calculate Risk Level             │
│     - Green: All pass                │
│     - Yellow: Minor issues           │
│     - Orange: Medium risk            │
│     - Red: Critical issues           │
└────────┬─────────────────────────────┘
         │
         ▼
┌──────────────────────────────────────┐
│  5. Save ValidationResult to DB      │
│     - Update Invoice                 │
│     - Create ValidationLayers        │
│     - Create RiskCheckResults        │
│     - Create AuditLog (UPLOAD)       │
└────────┬─────────────────────────────┘
         │
         ▼
┌─────────────────┐
│  6. Return to   │
│     Frontend    │
│     - Show risk │
│     - Show data │
└─────────────────┘
```

**Flow 2: PDF/Image Invoice Processing (OCR)**
```
User Upload PDF/JPG
     │
     ▼
1. Upload to S3
     │
     ▼
2. Create Invoice (Draft, ProcessingMethod="OCR")
     │
     ▼
3. TextractService.AnalyzeExpenseAsync()
     ├─ Call AWS Textract API
     ├─ Wait for response (2-5s)
     └─ Parse response JSON
     │
     ▼
4. Map Textract fields to Invoice model
     ├─ INVOICE_RECEIPT_ID → InvoiceNumber
     ├─ INVOICE_RECEIPT_DATE → InvoiceDate
     ├─ TOTAL → TotalAmount
     ├─ VENDOR_NAME → Seller info
     └─ Confidence scores
     │
     ▼
5. Basic Risk Check
     ├─ Missing XML → Yellow
     ├─ Low confidence (<70%) → Orange
     └─ Invalid format → Orange
     │
     ▼
6. Save to DB
     ├─ RawData = Textract response (JSONB)
     ├─ ExtractedData = Mapped data
     └─ RiskLevel
     │
     ▼
7. Return to Frontend
     - Show extracted data
     - Show confidence scores
     - Allow manual correction
```

**Flow 3: Approval Workflow**
```
Member:
  Upload → Edit (optional) → Submit
                              │
                              ▼
                        Status = "Pending"
                              │
                              ▼
                        Notification → Admin
                              │
                              ▼
Admin:                  Review Invoice
  ├─ If OK  → Approve → Status = "Approved"
  └─ If NOT → Reject  → Status = "Rejected"
                              │
                              ▼
                        AuditLog created
                              │
                              ▼
                        Notification → Member
```

---

## 6. THIẾT KẾ DATABASE PRODUCTION

### 6.1 Database Schema Overview

**Tổng quan**: 12 tables (bỏ Tags như yêu cầu)

```
CORE ENTITIES (6 tables):
├─ Companies (Multi-tenant)
├─ Users (Authentication & Authorization)
├─ Invoices (Central entity)
├─ FileStorages (S3 file metadata)
├─ DocumentTypes (Invoice classifications)
└─ ValidationLayers (3-layer validation results)

SUPPORT ENTITIES (6 tables):
├─ InvoiceAuditLogs (Immutable audit trail)
├─ RiskCheckResults (Detailed risk analysis)
├─ Notifications (In-app alerts)
├─ ExportHistories (Export tracking)
├─ AIProcessingLogs (Textract metrics)
└─ SystemConfigurations (App settings)
```

---

### 6.2 Detailed Schema Design

Tôi sẽ tạo file riêng cho database schema chi tiết do nội dung quá dài. File này sẽ bao gồm:
- 12 tables với mọi trường theo Nghị định 123/2020
- Indexes production-ready
- Constraints & Foreign keys
- Sample data
- Migration scripts

---

**[Tiếp theo: Phần II Database Detail sẽ được tạo trong file riêng]**

Tài liệu này đang được xây dựng, tôi sẽ tạo các file bổ sung:
1. DATABASE_SCHEMA_PRODUCTION.md - Chi tiết 12 tables
2. API_SPECIFICATION.md - REST API documentation
3. TEAM_HANDBOOK.md - Phân công 5 người + Timeline
4. DEPLOYMENT_GUIDE_PRODUCTION.md - AWS deployment chi tiết

Bạn có muốn tôi tiếp tục tạo các file này không?
