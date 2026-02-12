using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInvoice.API.Migrations
{
    /// <inheritdoc />
    public partial class FixRelationshipMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceAuditLogs_Invoices_InvoiceId1",
                table: "InvoiceAuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_ValidationLayers_Invoices_InvoiceId1",
                table: "ValidationLayers");

            migrationBuilder.DropIndex(
                name: "IX_ValidationLayers_InvoiceId1",
                table: "ValidationLayers");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceAuditLogs_InvoiceId1",
                table: "InvoiceAuditLogs");

            migrationBuilder.DropColumn(
                name: "InvoiceId1",
                table: "ValidationLayers");

            migrationBuilder.DropColumn(
                name: "InvoiceId1",
                table: "InvoiceAuditLogs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "InvoiceId1",
                table: "ValidationLayers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InvoiceId1",
                table: "InvoiceAuditLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ValidationLayers_InvoiceId1",
                table: "ValidationLayers",
                column: "InvoiceId1");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceAuditLogs_InvoiceId1",
                table: "InvoiceAuditLogs",
                column: "InvoiceId1");

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceAuditLogs_Invoices_InvoiceId1",
                table: "InvoiceAuditLogs",
                column: "InvoiceId1",
                principalTable: "Invoices",
                principalColumn: "InvoiceId");

            migrationBuilder.AddForeignKey(
                name: "FK_ValidationLayers_Invoices_InvoiceId1",
                table: "ValidationLayers",
                column: "InvoiceId1",
                principalTable: "Invoices",
                principalColumn: "InvoiceId");
        }
    }
}
