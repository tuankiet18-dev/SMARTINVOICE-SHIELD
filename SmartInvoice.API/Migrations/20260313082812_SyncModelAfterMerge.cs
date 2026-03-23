using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInvoice.API.Migrations
{
    /// <inheritdoc />
    public partial class SyncModelAfterMerge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
            DROP TABLE IF EXISTS "RiskCheckResults";
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
