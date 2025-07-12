using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Transport.Infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class ReserveWithMpIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "PaymentExternalId",
                table: "ReservePayment",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResultApiExternalRawJson",
                table: "ReservePayment",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StatusDetail",
                table: "ReservePayment",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "CustomerReserve",
                type: "VARCHAR(20)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2025, 7, 12, 17, 11, 44, 177, DateTimeKind.Utc).AddTicks(7675));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2025, 7, 12, 17, 11, 44, 177, DateTimeKind.Utc).AddTicks(7677));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentExternalId",
                table: "ReservePayment");

            migrationBuilder.DropColumn(
                name: "ResultApiExternalRawJson",
                table: "ReservePayment");

            migrationBuilder.DropColumn(
                name: "StatusDetail",
                table: "ReservePayment");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "CustomerReserve",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "VARCHAR(20)");

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2025, 6, 27, 22, 28, 21, 11, DateTimeKind.Utc).AddTicks(427));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2025, 6, 27, 22, 28, 21, 11, DateTimeKind.Utc).AddTicks(429));
        }
    }
}
