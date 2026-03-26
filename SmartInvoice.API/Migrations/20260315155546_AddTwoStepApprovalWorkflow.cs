using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInvoice.API.Migrations
{
    /// <inheritdoc />
    public partial class AddTwoStepApprovalWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Workflow_CurrentApprovalStep",
                table: "Invoices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "Workflow_Level1ApprovedAt",
                table: "Invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "Workflow_Level1ApprovedBy",
                table: "Invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Workflow_Level2ApprovedAt",
                table: "Invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "Workflow_Level2ApprovedBy",
                table: "Invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequireTwoStepApproval",
                table: "Companies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "TwoStepApprovalThreshold",
                table: "Companies",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Workflow_Level1ApprovedBy",
                table: "Invoices",
                column: "Workflow_Level1ApprovedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Workflow_Level2ApprovedBy",
                table: "Invoices",
                column: "Workflow_Level2ApprovedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Users_Workflow_Level1ApprovedBy",
                table: "Invoices",
                column: "Workflow_Level1ApprovedBy",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Users_Workflow_Level2ApprovedBy",
                table: "Invoices",
                column: "Workflow_Level2ApprovedBy",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Users_Workflow_Level1ApprovedBy",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Users_Workflow_Level2ApprovedBy",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_Workflow_Level1ApprovedBy",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_Workflow_Level2ApprovedBy",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "Workflow_CurrentApprovalStep",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "Workflow_Level1ApprovedAt",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "Workflow_Level1ApprovedBy",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "Workflow_Level2ApprovedAt",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "Workflow_Level2ApprovedBy",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "RequireTwoStepApproval",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "TwoStepApprovalThreshold",
                table: "Companies");
        }
    }
}
