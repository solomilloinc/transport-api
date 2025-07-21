using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Transport.Infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReserveDateInCustomerReserve : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReserveDate",
                table: "CustomerReserve",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2025, 7, 20, 20, 50, 27, 357, DateTimeKind.Utc).AddTicks(5035));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2025, 7, 20, 20, 50, 27, 357, DateTimeKind.Utc).AddTicks(5037));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReserveDate",
                table: "CustomerReserve");

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2025, 7, 12, 17, 17, 7, 856, DateTimeKind.Utc).AddTicks(2203));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2025, 7, 12, 17, 17, 7, 856, DateTimeKind.Utc).AddTicks(2205));
        }
    }
}
