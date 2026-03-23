using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInvoice.API.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceDossierColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "OriginalFileId",
                table: "Invoices",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "VisualFileId",
                table: "Invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_VisualFileId",
                table: "Invoices",
                column: "VisualFileId");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_FileStorages_VisualFileId",
                table: "Invoices",
                column: "VisualFileId",
                principalTable: "FileStorages",
                principalColumn: "FileId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_FileStorages_VisualFileId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_VisualFileId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "VisualFileId",
                table: "Invoices");

            migrationBuilder.AlterColumn<Guid>(
                name: "OriginalFileId",
                table: "Invoices",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
