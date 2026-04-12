namespace SmartInvoice.API.Enums;

public enum UserRole
{
    SuperAdmin,         // Quản trị viên hệ thống SmartInvoice
    CompanyAdmin,       // Giám đốc / Chủ doanh nghiệp (Toàn quyền)
    ChiefAccountant,    // Kế toán trưởng (Người duyệt cấp 2 / Duyệt cuối)
    Accountant,         // Kế toán viên (Người tạo, gửi duyệt, hoặc duyệt cấp 1)
}
