using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Transport.Infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class AddColumnsInReserve : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "DepartureHour",
                table: "Reserve",
                type: "time",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));

            migrationBuilder.AddColumn<string>(
                name: "DestinationName",
                table: "Reserve",
                type: "VARCHAR(100)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsHoliday",
                table: "Reserve",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OriginName",
                table: "Reserve",
                type: "VARCHAR(100)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ServiceName",
                table: "Reserve",
                type: "VARCHAR(250)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2025, 6, 7, 1, 18, 11, 374, DateTimeKind.Utc).AddTicks(7054));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2025, 6, 7, 1, 18, 11, 374, DateTimeKind.Utc).AddTicks(7058));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DepartureHour",
                table: "Reserve");

            migrationBuilder.DropColumn(
                name: "DestinationName",
                table: "Reserve");

            migrationBuilder.DropColumn(
                name: "IsHoliday",
                table: "Reserve");

            migrationBuilder.DropColumn(
                name: "OriginName",
                table: "Reserve");

            migrationBuilder.DropColumn(
                name: "ServiceName",
                table: "Reserve");

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
    }
}
