using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Transport.Infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReserveReportsAndPassengers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reserve_ServiceId",
                table: "Reserve");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Reserve",
                type: "VARCHAR(20)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "CustomerReserve",
                type: "VARCHAR(20)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "CustomerReserve",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2025, 5, 24, 18, 44, 56, 558, DateTimeKind.Utc).AddTicks(5699));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2025, 5, 24, 18, 44, 56, 558, DateTimeKind.Utc).AddTicks(5702));

            migrationBuilder.CreateIndex(
                name: "IX_Reserve_ServiceId_ReserveDate",
                table: "Reserve",
                columns: new[] { "ServiceId", "ReserveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Reserve_Status_ReserveDate",
                table: "Reserve",
                columns: new[] { "Status", "ReserveDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reserve_ServiceId_ReserveDate",
                table: "Reserve");

            migrationBuilder.DropIndex(
                name: "IX_Reserve_Status_ReserveDate",
                table: "Reserve");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "CustomerReserve");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "CustomerReserve");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Reserve",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "VARCHAR(20)");

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2025, 5, 18, 22, 41, 3, 244, DateTimeKind.Utc).AddTicks(1076));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2025, 5, 18, 22, 41, 3, 244, DateTimeKind.Utc).AddTicks(1079));

            migrationBuilder.CreateIndex(
                name: "IX_Reserve_ServiceId",
                table: "Reserve",
                column: "ServiceId");
        }
    }
}
