using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInvoice.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPackageLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PackageLevel",
                table: "SubscriptionPackages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "SubscriptionPackages",
                keyColumn: "PackageId",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "PackageLevel",
                value: 1);

            migrationBuilder.UpdateData(
                table: "SubscriptionPackages",
                keyColumn: "PackageId",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "PackageLevel",
                value: 2);

            migrationBuilder.UpdateData(
                table: "SubscriptionPackages",
                keyColumn: "PackageId",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "PackageLevel",
                value: 3);

            migrationBuilder.UpdateData(
                table: "SubscriptionPackages",
                keyColumn: "PackageId",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                column: "PackageLevel",
                value: 4);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PackageLevel",
                table: "SubscriptionPackages");
        }
    }
}
