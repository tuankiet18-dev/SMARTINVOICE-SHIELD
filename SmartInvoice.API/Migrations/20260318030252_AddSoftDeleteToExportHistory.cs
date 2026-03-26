using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInvoice.API.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftDeleteToExportHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "ExportHistories",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ExportHistories",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "ExportHistories");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ExportHistories");
        }
    }
}
