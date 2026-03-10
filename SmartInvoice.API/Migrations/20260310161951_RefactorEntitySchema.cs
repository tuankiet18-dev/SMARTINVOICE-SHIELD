using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using SmartInvoice.API.Entities.JsonModels;

#nullable disable

namespace SmartInvoice.API.Migrations
{
    /// <inheritdoc />
    public partial class RefactorEntitySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvoiceLineItems");

            migrationBuilder.DropTable(
                name: "RiskCheckResults");

            migrationBuilder.DropTable(
                name: "ValidationLayers");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_CompanyId_InvoiceDate",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_CompanyId_RiskLevel",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_CompanyId_Status",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_InvoiceNumber",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_SellerTaxCode",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "RiskReasons",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ValidationResult",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "S3Url",
                table: "FileStorages");

            migrationBuilder.DropColumn(
                name: "S3UrlExpiresAt",
                table: "FileStorages");

            migrationBuilder.DropColumn(
                name: "S3Url",
                table: "ExportHistories");

            migrationBuilder.DropColumn(
                name: "S3UrlExpiresAt",
                table: "ExportHistories");

            migrationBuilder.DropColumn(
                name: "ProcessedData",
                table: "AIProcessingLogs");

            migrationBuilder.CreateTable(
                name: "InvoiceCheckResults",
                columns: table => new
                {
                    CheckId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CheckName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CheckOrder = table.Column<int>(type: "integer", nullable: false),
                    IsValid = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    Suggestion = table.Column<string>(type: "text", nullable: true),
                    ErrorDetails = table.Column<string>(type: "jsonb", nullable: true),
                    AdditionalData = table.Column<string>(type: "jsonb", nullable: true),
                    DurationMs = table.Column<int>(type: "integer", nullable: true),
                    CheckedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CheckedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceCheckResults", x => x.CheckId);
                    table.ForeignKey(
                        name: "FK_InvoiceCheckResults_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "InvoiceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CompanyId_Status_RiskLevel_InvoiceDate",
                table: "Invoices",
                columns: new[] { "CompanyId", "Status", "RiskLevel", "InvoiceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceCheckResults_InvoiceId",
                table: "InvoiceCheckResults",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CompanyId_SellerTaxCode",
                table: "Invoices",
                columns: new[] { "CompanyId", "SellerTaxCode" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CompanyId_SellerTaxCode_FormNumber_SerialNumber_InvoiceNumber",
                table: "Invoices",
                columns: new[] { "CompanyId", "SellerTaxCode", "FormNumber", "SerialNumber", "InvoiceNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvoiceCheckResults");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_CompanyId_SellerTaxCode",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_CompanyId_SellerTaxCode_FormNumber_SerialNumber_InvoiceNumber",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_CompanyId_Status_RiskLevel_InvoiceDate",
                table: "Invoices");

            migrationBuilder.AddColumn<List<RiskReason>>(
                name: "RiskReasons",
                table: "Invoices",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<ValidationResultModel>(
                name: "ValidationResult",
                table: "Invoices",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "S3Url",
                table: "FileStorages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "S3UrlExpiresAt",
                table: "FileStorages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "S3Url",
                table: "ExportHistories",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "S3UrlExpiresAt",
                table: "ExportHistories",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessedData",
                table: "AIProcessingLogs",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InvoiceLineItems",
                columns: table => new
                {
                    LineItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfidenceScore = table.Column<float>(type: "real", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ItemName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VatAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    VatRate = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceLineItems", x => x.LineItemId);
                    table.ForeignKey(
                        name: "FK_InvoiceLineItems_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "InvoiceId",
                        onDelete: ReferentialAction.Cascade);
                });

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

            migrationBuilder.CreateTable(
                name: "ValidationLayers",
                columns: table => new
                {
                    LayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CheckedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ErrorDetails = table.Column<string>(type: "jsonb", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    IsValid = table.Column<bool>(type: "boolean", nullable: false),
                    LayerData = table.Column<string>(type: "jsonb", nullable: true),
                    LayerName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LayerOrder = table.Column<int>(type: "integer", nullable: false),
                    ValidationDurationMs = table.Column<int>(type: "integer", nullable: true),
                    ValidationStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValidationLayers", x => x.LayerId);
                    table.ForeignKey(
                        name: "FK_ValidationLayers_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "InvoiceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CompanyId_InvoiceDate",
                table: "Invoices",
                columns: new[] { "CompanyId", "InvoiceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CompanyId_RiskLevel",
                table: "Invoices",
                columns: new[] { "CompanyId", "RiskLevel" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CompanyId_Status",
                table: "Invoices",
                columns: new[] { "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_InvoiceNumber",
                table: "Invoices",
                column: "InvoiceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_SellerTaxCode",
                table: "Invoices",
                column: "SellerTaxCode");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLineItems_InvoiceId",
                table: "InvoiceLineItems",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskCheckResults_InvoiceId",
                table: "RiskCheckResults",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ValidationLayers_InvoiceId",
                table: "ValidationLayers",
                column: "InvoiceId");
        }
    }
}
