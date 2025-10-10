using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Transport.Infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class StartDayEndDayToServiceSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndDay",
                table: "ServiceSchedule");

            migrationBuilder.DropColumn(
                name: "StartDay",
                table: "ServiceSchedule");

            migrationBuilder.AddColumn<int>(
                name: "EndDay",
                table: "Service",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StartDay",
                table: "Service",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2025, 10, 10, 16, 7, 25, 608, DateTimeKind.Utc).AddTicks(7124));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2025, 10, 10, 16, 7, 25, 608, DateTimeKind.Utc).AddTicks(7128));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndDay",
                table: "Service");

            migrationBuilder.DropColumn(
                name: "StartDay",
                table: "Service");

            migrationBuilder.AddColumn<int>(
                name: "EndDay",
                table: "ServiceSchedule",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StartDay",
                table: "ServiceSchedule",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2025, 9, 17, 21, 36, 42, 866, DateTimeKind.Utc).AddTicks(3697));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2025, 9, 17, 21, 36, 42, 866, DateTimeKind.Utc).AddTicks(3699));
        }
    }
}
