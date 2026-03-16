using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInvoice.API.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSystemConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "SystemConfigurations",
                keyColumn: "ConfigId",
                keyValue: 5);

            migrationBuilder.UpdateData(
                table: "SystemConfigurations",
                keyColumn: "ConfigId",
                keyValue: 1,
                columns: new[] { "Category", "ConfigKey", "ConfigType", "ConfigValue", "CreatedAt", "DefaultValue", "Description", "UpdatedAt" },
                values: new object[] { "Business Logic", "CURRENCY_TOLERANCE", "Integer", "10", new DateTime(2026, 3, 16, 16, 24, 55, 373, DateTimeKind.Utc).AddTicks(6592), "10", "Dung sai làm tròn tiền (VNĐ)", new DateTime(2026, 3, 16, 16, 24, 55, 373, DateTimeKind.Utc).AddTicks(6703) });

            migrationBuilder.UpdateData(
                table: "SystemConfigurations",
                keyColumn: "ConfigId",
                keyValue: 2,
                columns: new[] { "Category", "ConfigKey", "ConfigType", "ConfigValue", "CreatedAt", "DefaultValue", "Description", "UpdatedAt" },
                values: new object[] { "Business Logic", "ENABLE_VIETQR_VALIDATION", "Boolean", "true", new DateTime(2026, 3, 16, 16, 24, 55, 373, DateTimeKind.Utc).AddTicks(6801), "true", "Xác thực MST qua VietQR", new DateTime(2026, 3, 16, 16, 24, 55, 373, DateTimeKind.Utc).AddTicks(6802) });

            migrationBuilder.UpdateData(
                table: "SystemConfigurations",
                keyColumn: "ConfigId",
                keyValue: 3,
                columns: new[] { "Category", "ConfigKey", "CreatedAt", "Description", "UpdatedAt" },
                values: new object[] { "System & Storage", "MAX_UPLOAD_SIZE_MB", new DateTime(2026, 3, 16, 16, 24, 55, 373, DateTimeKind.Utc).AddTicks(6805), "Giới hạn dung lượng tải file (MB)", new DateTime(2026, 3, 16, 16, 24, 55, 373, DateTimeKind.Utc).AddTicks(6806) });

            migrationBuilder.UpdateData(
                table: "SystemConfigurations",
                keyColumn: "ConfigId",
                keyValue: 4,
                columns: new[] { "Category", "ConfigKey", "ConfigValue", "CreatedAt", "DefaultValue", "Description", "UpdatedAt" },
                values: new object[] { "System & Storage", "MAINTENANCE_MODE", "false", new DateTime(2026, 3, 16, 16, 24, 55, 373, DateTimeKind.Utc).AddTicks(6809), "false", "Chế độ bảo trì (Chặn thao tác)", new DateTime(2026, 3, 16, 16, 24, 55, 373, DateTimeKind.Utc).AddTicks(6809) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "SystemConfigurations",
                keyColumn: "ConfigId",
                keyValue: 1,
                columns: new[] { "Category", "ConfigKey", "ConfigType", "ConfigValue", "CreatedAt", "DefaultValue", "Description", "UpdatedAt" },
                values: new object[] { "AI & OCR", "OcrApiEndpoint", "String", "http://localhost:5000/process_invoice", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "http://localhost:5000/process_invoice", "Endpoint kết nối với dịch vụ OCR Python.", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.UpdateData(
                table: "SystemConfigurations",
                keyColumn: "ConfigId",
                keyValue: 2,
                columns: new[] { "Category", "ConfigKey", "ConfigType", "ConfigValue", "CreatedAt", "DefaultValue", "Description", "UpdatedAt" },
                values: new object[] { "AI & OCR", "ConfidenceThreshold", "Integer", "0.85", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "0.85", "Ngưỡng độ tin cậy để tự động chấp nhận kết quả OCR.", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.UpdateData(
                table: "SystemConfigurations",
                keyColumn: "ConfigId",
                keyValue: 3,
                columns: new[] { "Category", "ConfigKey", "CreatedAt", "Description", "UpdatedAt" },
                values: new object[] { "Hệ thống", "MaxUploadSizeMB", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Dung lượng tối đa cho mỗi file upload (MB).", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.UpdateData(
                table: "SystemConfigurations",
                keyColumn: "ConfigId",
                keyValue: 4,
                columns: new[] { "Category", "ConfigKey", "ConfigValue", "CreatedAt", "DefaultValue", "Description", "UpdatedAt" },
                values: new object[] { "AI & OCR", "AllowMachineLearning", "true", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "true", "Cho phép AI học từ dữ liệu chỉnh sửa của người dùng.", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.InsertData(
                table: "SystemConfigurations",
                columns: new[] { "ConfigId", "Category", "ConfigKey", "ConfigType", "ConfigValue", "CreatedAt", "DefaultValue", "Description", "IsEncrypted", "IsReadOnly", "RequiresRestart", "UpdatedAt", "UpdatedBy" },
                values: new object[] { 5, "Hệ thống", "SyncIntervalMinutes", "Integer", "15", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "15", "Thời gian đồng bộ dữ liệu với AWS S3 (phút).", false, false, false, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null });
        }
    }
}
