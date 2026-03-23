using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SmartInvoice.API.Migrations
{
    /// <inheritdoc />
    public partial class AddConfigurationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Companies_SubscriptionPackages_SubscriptionPackageId",
                table: "Companies");

            migrationBuilder.InsertData(
                table: "SystemConfigurations",
                columns: new[] { "ConfigId", "Category", "ConfigKey", "ConfigType", "ConfigValue", "CreatedAt", "DefaultValue", "Description", "IsEncrypted", "IsReadOnly", "RequiresRestart", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, "AI & OCR", "OcrApiEndpoint", "String", "http://localhost:5000/process_invoice", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "http://localhost:5000/process_invoice", "Endpoint kết nối với dịch vụ OCR Python.", false, false, false, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { 2, "AI & OCR", "ConfidenceThreshold", "Integer", "0.85", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "0.85", "Ngưỡng độ tin cậy để tự động chấp nhận kết quả OCR.", false, false, false, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { 3, "Hệ thống", "MaxUploadSizeMB", "Integer", "10", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "10", "Dung lượng tối đa cho mỗi file upload (MB).", false, false, false, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { 4, "AI & OCR", "AllowMachineLearning", "Boolean", "true", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "true", "Cho phép AI học từ dữ liệu chỉnh sửa của người dùng.", false, false, false, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { 5, "Hệ thống", "SyncIntervalMinutes", "Integer", "15", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "15", "Thời gian đồng bộ dữ liệu với AWS S3 (phút).", false, false, false, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null }
                });

            migrationBuilder.AddForeignKey(
                name: "FK_Companies_SubscriptionPackages_SubscriptionPackageId",
                table: "Companies",
                column: "SubscriptionPackageId",
                principalTable: "SubscriptionPackages",
                principalColumn: "PackageId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Companies_SubscriptionPackages_SubscriptionPackageId",
                table: "Companies");

            migrationBuilder.DeleteData(
                table: "SystemConfigurations",
                keyColumn: "ConfigId",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "SystemConfigurations",
                keyColumn: "ConfigId",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "SystemConfigurations",
                keyColumn: "ConfigId",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "SystemConfigurations",
                keyColumn: "ConfigId",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "SystemConfigurations",
                keyColumn: "ConfigId",
                keyValue: 5);

            migrationBuilder.AddForeignKey(
                name: "FK_Companies_SubscriptionPackages_SubscriptionPackageId",
                table: "Companies",
                column: "SubscriptionPackageId",
                principalTable: "SubscriptionPackages",
                principalColumn: "PackageId",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
