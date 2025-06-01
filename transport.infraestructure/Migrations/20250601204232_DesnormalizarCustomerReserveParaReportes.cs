using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Transport.Infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class DesnormalizarCustomerReserveParaReportes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerEmail",
                table: "CustomerReserve",
                type: "VARCHAR(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CustomerFullName",
                table: "CustomerReserve",
                type: "VARCHAR(250)",
                maxLength: 250,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DestinationCityName",
                table: "CustomerReserve",
                type: "VARCHAR(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DocumentNumber",
                table: "CustomerReserve",
                type: "VARCHAR(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DriverName",
                table: "CustomerReserve",
                type: "VARCHAR(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DropoffAddress",
                table: "CustomerReserve",
                type: "VARCHAR(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginCityName",
                table: "CustomerReserve",
                type: "VARCHAR(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Phone1",
                table: "CustomerReserve",
                type: "VARCHAR(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone2",
                table: "CustomerReserve",
                type: "VARCHAR(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PickupAddress",
                table: "CustomerReserve",
                type: "VARCHAR(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ServiceName",
                table: "CustomerReserve",
                type: "VARCHAR(250)",
                maxLength: 250,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "VehicleInternalNumber",
                table: "CustomerReserve",
                type: "VARCHAR(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2025, 6, 1, 20, 42, 32, 318, DateTimeKind.Utc).AddTicks(5202));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2025, 6, 1, 20, 42, 32, 318, DateTimeKind.Utc).AddTicks(5206));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerEmail",
                table: "CustomerReserve");

            migrationBuilder.DropColumn(
                name: "CustomerFullName",
                table: "CustomerReserve");

            migrationBuilder.DropColumn(
                name: "DestinationCityName",
                table: "CustomerReserve");

            migrationBuilder.DropColumn(
                name: "DocumentNumber",
                table: "CustomerReserve");

            migrationBuilder.DropColumn(
                name: "DriverName",
                table: "CustomerReserve");

            migrationBuilder.DropColumn(
                name: "DropoffAddress",
                table: "CustomerReserve");

            migrationBuilder.DropColumn(
                name: "OriginCityName",
                table: "CustomerReserve");

            migrationBuilder.DropColumn(
                name: "Phone1",
                table: "CustomerReserve");

            migrationBuilder.DropColumn(
                name: "Phone2",
                table: "CustomerReserve");

            migrationBuilder.DropColumn(
                name: "PickupAddress",
                table: "CustomerReserve");

            migrationBuilder.DropColumn(
                name: "ServiceName",
                table: "CustomerReserve");

            migrationBuilder.DropColumn(
                name: "VehicleInternalNumber",
                table: "CustomerReserve");

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2025, 5, 30, 1, 26, 3, 410, DateTimeKind.Utc).AddTicks(1367));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2025, 5, 30, 1, 26, 3, 410, DateTimeKind.Utc).AddTicks(1370));
        }
    }
}
