using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace transport.infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPassengerRelatedPassengerId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RelatedPassengerId",
                table: "Passenger",
                type: "int",
                nullable: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_Passenger_RelatedPassengerId",
                table: "Passenger",
                column: "RelatedPassengerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Passenger_Passenger_RelatedPassengerId",
                table: "Passenger",
                column: "RelatedPassengerId",
                principalTable: "Passenger",
                principalColumn: "PassengerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Passenger_Passenger_RelatedPassengerId",
                table: "Passenger");

            migrationBuilder.DropIndex(
                name: "IX_Passenger_RelatedPassengerId",
                table: "Passenger");

            migrationBuilder.DropColumn(
                name: "RelatedPassengerId",
                table: "Passenger");

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
    }
}
