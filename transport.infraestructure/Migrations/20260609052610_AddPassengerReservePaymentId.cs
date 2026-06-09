using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace transport.infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPassengerReservePaymentId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReservePaymentId",
                table: "Passenger",
                type: "int",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 6, 9, 5, 26, 9, 890, DateTimeKind.Utc).AddTicks(978));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2026, 6, 9, 5, 26, 9, 890, DateTimeKind.Utc).AddTicks(982));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 3,
                column: "CreatedDate",
                value: new DateTime(2026, 6, 9, 5, 26, 9, 890, DateTimeKind.Utc).AddTicks(983));

            migrationBuilder.CreateIndex(
                name: "IX_Passenger_ReservePaymentId",
                table: "Passenger",
                column: "ReservePaymentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Passenger_ReservePayment_ReservePaymentId",
                table: "Passenger",
                column: "ReservePaymentId",
                principalTable: "ReservePayment",
                principalColumn: "ReservePaymentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Passenger_ReservePayment_ReservePaymentId",
                table: "Passenger");

            migrationBuilder.DropIndex(
                name: "IX_Passenger_ReservePaymentId",
                table: "Passenger");

            migrationBuilder.DropColumn(
                name: "ReservePaymentId",
                table: "Passenger");

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 6, 6, 0, 59, 28, 247, DateTimeKind.Utc).AddTicks(4155));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2026, 6, 6, 0, 59, 28, 247, DateTimeKind.Utc).AddTicks(4157));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 3,
                column: "CreatedDate",
                value: new DateTime(2026, 6, 6, 0, 59, 28, 247, DateTimeKind.Utc).AddTicks(4158));
        }
    }
}
