using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Transport.Infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCashBox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CashBoxId",
                table: "ReservePayment",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReserveRelatedId",
                table: "Passenger",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CashBox",
                columns: table => new
                {
                    CashBoxId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Description = table.Column<string>(type: "NVARCHAR(200)", nullable: true),
                    OpenedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "VARCHAR(20)", nullable: false),
                    OpenedByUserId = table.Column<int>(type: "int", nullable: false),
                    ClosedByUserId = table.Column<int>(type: "int", nullable: true),
                    ReserveId = table.Column<int>(type: "int", nullable: true),
                    CreatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: false, defaultValue: "System"),
                    UpdatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashBox", x => x.CashBoxId);
                    table.ForeignKey(
                        name: "FK_CashBox_Reserve_ReserveId",
                        column: x => x.ReserveId,
                        principalTable: "Reserve",
                        principalColumn: "ReserveId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CashBox_User_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashBox_User_OpenedByUserId",
                        column: x => x.OpenedByUserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 1, 11, 15, 56, 56, 458, DateTimeKind.Utc).AddTicks(5415));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2026, 1, 11, 15, 56, 56, 458, DateTimeKind.Utc).AddTicks(5417));

            migrationBuilder.CreateIndex(
                name: "IX_ReservePayment_CashBoxId",
                table: "ReservePayment",
                column: "CashBoxId");

            migrationBuilder.CreateIndex(
                name: "IX_Passenger_ReserveRelatedId",
                table: "Passenger",
                column: "ReserveRelatedId");

            migrationBuilder.CreateIndex(
                name: "IX_CashBox_ClosedByUserId",
                table: "CashBox",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CashBox_OpenedAt",
                table: "CashBox",
                column: "OpenedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CashBox_OpenedByUserId",
                table: "CashBox",
                column: "OpenedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CashBox_ReserveId",
                table: "CashBox",
                column: "ReserveId");

            migrationBuilder.CreateIndex(
                name: "IX_CashBox_Status",
                table: "CashBox",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CashBox_Status_OpenedAt",
                table: "CashBox",
                columns: new[] { "Status", "OpenedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Passenger_Reserve_ReserveRelatedId",
                table: "Passenger",
                column: "ReserveRelatedId",
                principalTable: "Reserve",
                principalColumn: "ReserveId");

            migrationBuilder.AddForeignKey(
                name: "FK_ReservePayment_CashBox_CashBoxId",
                table: "ReservePayment",
                column: "CashBoxId",
                principalTable: "CashBox",
                principalColumn: "CashBoxId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Passenger_Reserve_ReserveRelatedId",
                table: "Passenger");

            migrationBuilder.DropForeignKey(
                name: "FK_ReservePayment_CashBox_CashBoxId",
                table: "ReservePayment");

            migrationBuilder.DropTable(
                name: "CashBox");

            migrationBuilder.DropIndex(
                name: "IX_ReservePayment_CashBoxId",
                table: "ReservePayment");

            migrationBuilder.DropIndex(
                name: "IX_Passenger_ReserveRelatedId",
                table: "Passenger");

            migrationBuilder.DropColumn(
                name: "CashBoxId",
                table: "ReservePayment");

            migrationBuilder.DropColumn(
                name: "ReserveRelatedId",
                table: "Passenger");

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
    }
}
