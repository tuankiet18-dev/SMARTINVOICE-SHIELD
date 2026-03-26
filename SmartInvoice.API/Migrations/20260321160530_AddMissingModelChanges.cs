using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInvoice.API.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AutoApproveThreshold",
                table: "Companies",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsAutoApproveEnabled",
                table: "Companies",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoApproveThreshold",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "IsAutoApproveEnabled",
                table: "Companies");
        }
    }
}
