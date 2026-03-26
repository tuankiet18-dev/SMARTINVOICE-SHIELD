using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInvoice.API.Migrations
{
    /// <inheritdoc />
    public partial class AddQuotaFieldsToCompany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentActiveUsers",
                table: "Companies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "UsedStorageBytes",
                table: "Companies",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentActiveUsers",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "UsedStorageBytes",
                table: "Companies");
        }
    }
}
