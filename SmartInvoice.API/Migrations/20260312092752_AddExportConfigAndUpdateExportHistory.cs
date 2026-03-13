using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInvoice.API.Migrations
{
    /// <inheritdoc />
    public partial class AddExportConfigAndUpdateExportHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""RiskCheckResults"";");

            migrationBuilder.AddColumn<string>(
                name: "FileName",
                table: "ExportHistories",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ExportHistories",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ExportConfigs",
                columns: table => new
                {
                    ConfigId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefaultDebitAccount = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DefaultCreditAccount = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DefaultTaxAccount = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DefaultWarehouse = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportConfigs", x => x.ConfigId);
                    table.ForeignKey(
                        name: "FK_ExportConfigs_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExportConfigs_CompanyId",
                table: "ExportConfigs",
                column: "CompanyId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExportConfigs");

            migrationBuilder.DropColumn(
                name: "FileName",
                table: "ExportHistories");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ExportHistories");

            migrationBuilder.CreateTable(
                name: "RiskCheckResults",
                columns: table => new
                {
                    CheckId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckDetails = table.Column<string>(type: "jsonb", nullable: true),
                    CheckDurationMs = table.Column<int>(type: "integer", nullable: true),
                    CheckStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CheckSubType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CheckType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CheckedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CheckedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    RiskLevel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Suggestion = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskCheckResults", x => x.CheckId);
                    table.ForeignKey(
                        name: "FK_RiskCheckResults_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "InvoiceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RiskCheckResults_InvoiceId",
                table: "RiskCheckResults",
                column: "InvoiceId");
        }
    }
}
