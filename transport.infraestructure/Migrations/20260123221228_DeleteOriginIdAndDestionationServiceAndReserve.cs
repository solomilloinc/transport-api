using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace transport.infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class DeleteOriginIdAndDestionationServiceAndReserve : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reserve_City_DestinationId",
                table: "Reserve");

            migrationBuilder.DropForeignKey(
                name: "FK_Reserve_City_OriginId",
                table: "Reserve");

            migrationBuilder.DropForeignKey(
                name: "FK_Service_City_DestinationId",
                table: "Service");

            migrationBuilder.DropForeignKey(
                name: "FK_Service_City_OriginId",
                table: "Service");

            migrationBuilder.DropIndex(
                name: "IX_Service_DestinationId",
                table: "Service");

            migrationBuilder.DropIndex(
                name: "IX_Service_OriginId",
                table: "Service");

            migrationBuilder.DropIndex(
                name: "IX_Reserve_DestinationId",
                table: "Reserve");

            migrationBuilder.DropIndex(
                name: "IX_Reserve_OriginId_DestinationId_ReserveDate",
                table: "Reserve");

            migrationBuilder.DropColumn(
                name: "DestinationId",
                table: "Service");

            migrationBuilder.DropColumn(
                name: "OriginId",
                table: "Service");

            migrationBuilder.DropColumn(
                name: "DestinationId",
                table: "Reserve");

            migrationBuilder.DropColumn(
                name: "OriginId",
                table: "Reserve");

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 1, 23, 22, 12, 27, 879, DateTimeKind.Utc).AddTicks(375));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2026, 1, 23, 22, 12, 27, 879, DateTimeKind.Utc).AddTicks(378));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DestinationId",
                table: "Service",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OriginId",
                table: "Service",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DestinationId",
                table: "Reserve",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OriginId",
                table: "Reserve",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 1, 23, 20, 22, 29, 348, DateTimeKind.Utc).AddTicks(4391));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2026, 1, 23, 20, 22, 29, 348, DateTimeKind.Utc).AddTicks(4393));

            migrationBuilder.CreateIndex(
                name: "IX_Service_DestinationId",
                table: "Service",
                column: "DestinationId");

            migrationBuilder.CreateIndex(
                name: "IX_Service_OriginId",
                table: "Service",
                column: "OriginId");

            migrationBuilder.CreateIndex(
                name: "IX_Reserve_DestinationId",
                table: "Reserve",
                column: "DestinationId");

            migrationBuilder.CreateIndex(
                name: "IX_Reserve_OriginId_DestinationId_ReserveDate",
                table: "Reserve",
                columns: new[] { "OriginId", "DestinationId", "ReserveDate" });

            migrationBuilder.AddForeignKey(
                name: "FK_Reserve_City_DestinationId",
                table: "Reserve",
                column: "DestinationId",
                principalTable: "City",
                principalColumn: "CityId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Reserve_City_OriginId",
                table: "Reserve",
                column: "OriginId",
                principalTable: "City",
                principalColumn: "CityId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Service_City_DestinationId",
                table: "Service",
                column: "DestinationId",
                principalTable: "City",
                principalColumn: "CityId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Service_City_OriginId",
                table: "Service",
                column: "OriginId",
                principalTable: "City",
                principalColumn: "CityId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
