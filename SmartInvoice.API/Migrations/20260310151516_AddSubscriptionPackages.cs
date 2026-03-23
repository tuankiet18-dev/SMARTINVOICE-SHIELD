using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SmartInvoice.API.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionPackages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BillingCycle",
                table: "Companies",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "SubscriptionPackageId",
                table: "Companies",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SubscriptionPackages",
                columns: table => new
                {
                    PackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    PackageCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PackageName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PricePerMonth = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PricePerSixMonths = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PricePerYear = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MaxUsers = table.Column<int>(type: "integer", nullable: false),
                    MaxInvoicesPerMonth = table.Column<int>(type: "integer", nullable: false),
                    StorageQuotaGB = table.Column<int>(type: "integer", nullable: false),
                    HasAiProcessing = table.Column<bool>(type: "boolean", nullable: false),
                    HasAdvancedWorkflow = table.Column<bool>(type: "boolean", nullable: false),
                    HasRiskWarning = table.Column<bool>(type: "boolean", nullable: false),
                    HasAuditLog = table.Column<bool>(type: "boolean", nullable: false),
                    HasErpIntegration = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPackages", x => x.PackageId);
                });

            migrationBuilder.InsertData(
                table: "SubscriptionPackages",
                columns: new[] { "PackageId", "CreatedAt", "Description", "HasAdvancedWorkflow", "HasAiProcessing", "HasAuditLog", "HasErpIntegration", "HasRiskWarning", "IsActive", "MaxInvoicesPerMonth", "MaxUsers", "PackageCode", "PackageName", "PricePerMonth", "PricePerSixMonths", "PricePerYear", "StorageQuotaGB", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Trải nghiệm sức mạnh xử lý hóa đơn bằng AI dành cho cá nhân hoặc doanh nghiệp mới thành lập.", false, true, false, false, false, true, 30, 1, "FREE", "Gói Dùng Thử (Free)", 0m, 0m, 0m, 1, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("22222222-2222-2222-2222-222222222222"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Giải pháp tối ưu cho doanh nghiệp siêu nhỏ, đáp ứng nhu cầu xử lý hóa đơn tự động cơ bản.", false, true, false, false, false, true, 200, 5, "STARTER", "Gói Khởi Nghiệp (Starter)", 199000m, 995000m, 1990000m, 5, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("33333333-3333-3333-3333-333333333333"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Quản trị rủi ro toàn diện và tự động hóa quy trình phê duyệt cho doanh nghiệp vừa và nhỏ (SME).", true, true, true, false, true, true, 1000, 15, "PRO", "Gói Chuyên Nghiệp (Professional)", 599000m, 2995000m, 5990000m, 20, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("44444444-4444-4444-4444-444444444444"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Giải pháp tùy biến chuyên sâu, tích hợp API trực tiếp vào hệ thống ERP của tập đoàn.", true, true, true, true, true, true, 99999, 999, "ENTERPRISE", "Gói Doanh Nghiệp (Enterprise)", 1999000m, 9995000m, 19990000m, 100, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Companies_SubscriptionPackageId",
                table: "Companies",
                column: "SubscriptionPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPackages_PackageCode",
                table: "SubscriptionPackages",
                column: "PackageCode",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Companies_SubscriptionPackages_SubscriptionPackageId",
                table: "Companies",
                column: "SubscriptionPackageId",
                principalTable: "SubscriptionPackages",
                principalColumn: "PackageId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Companies_SubscriptionPackages_SubscriptionPackageId",
                table: "Companies");

            migrationBuilder.DropTable(
                name: "SubscriptionPackages");

            migrationBuilder.DropIndex(
                name: "IX_Companies_SubscriptionPackageId",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "BillingCycle",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "SubscriptionPackageId",
                table: "Companies");
        }
    }
}
