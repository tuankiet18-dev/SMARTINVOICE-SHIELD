# Hướng dẫn đưa PostgreSQL từ Local lên Amazon RDS

Để chuyển đổi Database từ môi trường máy cá nhân (Local) lên môi trường Cloud Product (Amazon RDS), Leader và team cần thực hiện 3 bước lớn dưới đây. Vì hệ thống đang dùng Entity Framework Core (EF Core), quá trình này cực kỳ nhẹ nhàng và an toàn.

## Bước 1: Khởi tạo Amazon RDS (Console)

1. Đăng nhập vào **AWS Management Console**.
2. Tìm kiếm dịch vụ **RDS** (Relational Database Service).
3. Bấm nút **Create database**.
4. Cấu hình các thông số sau:
   - **Database creation method:** `Standard create`
   - **Engine options:** `PostgreSQL` (Chọn Version 14 để tương thích code hiện tại).
   - **Templates:** Nên chọn `Free tier` cho giai đoạn Dev/Test, hoặc `Production` nếu muốn chạy thật (sẽ tự động bật Multi-AZ).
   - **Settings:**
     - `DB instance identifier`: Tên gợi nhớ (ví dụ: `smartinvoice-prod-db`)
     - `Master username`: Tên tài khoản admin (ví dụ: `postgresadmin`)
     - `Master password`: Đặt mật khẩu đủ mạnh và **LƯU LẠI**.
   - **Instance configuration:** `db.t3.micro` hoặc `db.t3.small` là đủ dùng lúc đầu.
   - **Storage:** Cấp khoảng `20GB` SSD (gp2/gp3) và **bật Auto scaling** lên khoảng 100GB để phòng hờ dữ liệu nhiều.
   - **Connectivity:**
     - **VPC:** Chọn Default VPC hoặc tạo mới.
     - **Public access:** Chọn **No** (Rất quan trọng về bảo mật). RDS không bao giờ được phép cho public truy cập thẳng từ Internet. Chỉ có máy chủ BE (Elastic Beanstalk) nằm chung mạng VPC mới kết nối được.
       *Lưu ý:* Nếu bạn (Leader) muốn dùng DBeaver hoặc pgAdmin từ máy local chọc thẳng vào RDS để debug cho nhanh, hãy tạm thời chọn **Yes** (nhưng phải thiết lập Rule Security Group chỉ cho phép IP wifi nhà bạn thôi).
5. Bấm **Create database** và đi pha ly cà phê chờ tầm 5-10 phút.

## Bước 2: Lấy Connection String (Chuỗi kết nối)

Khi RDS chuyển trạng thái sang `Available`, bấm vào database vừa tạo.
1. Mục **Connectivity & security**, tìm bảng **Endpoint & port**.
2. Copy cái **Endpoint** (ví dụ: `smartinvoice-prod.xxxxxx.ap-southeast-1.rds.amazonaws.com`).
3. Port mặc định của PostgreSQL là `5432`.

Chuỗi kết nối của bạn sẽ có dạng:
```text
Host=smartinvoice-prod.xxxxxx.ap-southeast-1.rds.amazonaws.com;Port=5432;Database=SmartInvoiceDb;Username=postgresadmin;Password=YourComplexPassword123;
```

## Bước 3: Đẩy Code và Database Schema dọn đường

1. **Cập nhật `appsettings.Production.json` chạy trên Cloud:**
   Mở source code API, tạo hoặc file sửa file `appsettings.Production.json` (chứ không phải `appsettings.Development.json` đâu nhé):
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=smartinvoice-prod.xxxxxx.ap-southeast-1.rds.amazonaws.com;Port=5432;Database=SmartInvoiceDb;Username=postgresadmin;Password=YourComplexPassword123;"
     }
   }
   ```

2. **Chạy EF Core Migrations lên DB Cloud:**
   Vì bạn đã viết xong hết code Entity trong local, hãy tận dụng Migrations để nó tự động tạo toàn bộ cái bảng (Users, Invoices, v.v...) trống tinh tươm lên RDS mà không cần phải export/import dump data SQL.

   Mở Terminal ở folder **SmartInvoice.API** và gõ lệnh siêu pháp thuật này:
   ```bash
   dotnet ef database update --connection "Host=smartinvoice-prod.xxxxxx.ap-southeast-1.rds.amazonaws.com;Port=5432;Database=SmartInvoiceDb;Username=postgresadmin;Password=YourComplexPassword123;"
   ```
   *Note: EF Core sẽ cắm thẳng vào RDS và tự apply toàn bộ những file Migration trong thư mục `Migrations/` của bạn.*

Sau khi chạy xong lệnh trên, bạn có thể tự hào vỗ ngực nói với team: "Hệ thống đã chính thức bám rễ lên Cloud!". BE của bạn bây giờ có thể kết nối bình thường với Dữ liệu Cloud rồi.
