using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace transport.infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReserveGenerationDaysToTenantConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReserveGenerationDays",
                table: "TenantConfig",
                type: "int",
                nullable: false,
                defaultValue: 15);

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 5, 25, 22, 56, 33, 184, DateTimeKind.Utc).AddTicks(7701));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2026, 5, 25, 22, 56, 33, 184, DateTimeKind.Utc).AddTicks(7702));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 3,
                column: "CreatedDate",
                value: new DateTime(2026, 5, 25, 22, 56, 33, 184, DateTimeKind.Utc).AddTicks(7704));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReserveGenerationDays",
                table: "TenantConfig");

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 5, 17, 1, 18, 57, 669, DateTimeKind.Utc).AddTicks(3639));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2026, 5, 17, 1, 18, 57, 669, DateTimeKind.Utc).AddTicks(3641));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 3,
                column: "CreatedDate",
                value: new DateTime(2026, 5, 17, 1, 18, 57, 669, DateTimeKind.Utc).AddTicks(3642));
        }
    }
}
