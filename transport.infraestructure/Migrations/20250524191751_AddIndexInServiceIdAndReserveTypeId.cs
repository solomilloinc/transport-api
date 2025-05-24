using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Transport.Infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIndexInServiceIdAndReserveTypeId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReservePrice_ServiceId",
                table: "ReservePrice");

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2025, 5, 24, 19, 17, 50, 567, DateTimeKind.Utc).AddTicks(852));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2025, 5, 24, 19, 17, 50, 567, DateTimeKind.Utc).AddTicks(855));

            migrationBuilder.CreateIndex(
                name: "IX_ReservePrice_ServiceId_ReserveTypeId",
                table: "ReservePrice",
                columns: new[] { "ServiceId", "ReserveTypeId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReservePrice_ServiceId_ReserveTypeId",
                table: "ReservePrice");

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
                name: "IX_ReservePrice_ServiceId",
                table: "ReservePrice",
                column: "ServiceId",
                unique: true);
        }
    }
}
