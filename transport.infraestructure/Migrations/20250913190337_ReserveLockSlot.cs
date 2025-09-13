using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Transport.Infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class ReserveLockSlot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReserveSlotLock",
                columns: table => new
                {
                    ReserveSlotLockId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LockToken = table.Column<string>(type: "VARCHAR(50)", nullable: false),
                    OutboundReserveId = table.Column<int>(type: "int", nullable: false),
                    ReturnReserveId = table.Column<int>(type: "int", nullable: true),
                    SlotsLocked = table.Column<int>(type: "int", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "VARCHAR(20)", nullable: false),
                    UserEmail = table.Column<string>(type: "VARCHAR(100)", nullable: true),
                    UserDocumentNumber = table.Column<string>(type: "VARCHAR(20)", nullable: true),
                    CustomerId = table.Column<int>(type: "int", nullable: true),
                    CreatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: false, defaultValue: "System"),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReserveSlotLock", x => x.ReserveSlotLockId);
                    table.ForeignKey(
                        name: "FK_ReserveSlotLock_Customer_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customer",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ReserveSlotLock_Reserve_OutboundReserveId",
                        column: x => x.OutboundReserveId,
                        principalTable: "Reserve",
                        principalColumn: "ReserveId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReserveSlotLock_Reserve_ReturnReserveId",
                        column: x => x.ReturnReserveId,
                        principalTable: "Reserve",
                        principalColumn: "ReserveId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2025, 9, 13, 19, 3, 37, 343, DateTimeKind.Utc).AddTicks(1672));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2025, 9, 13, 19, 3, 37, 343, DateTimeKind.Utc).AddTicks(1673));

            migrationBuilder.CreateIndex(
                name: "IX_ReserveSlotLock_CreatedDate",
                table: "ReserveSlotLock",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_ReserveSlotLock_CustomerId",
                table: "ReserveSlotLock",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ReserveSlotLock_LockToken",
                table: "ReserveSlotLock",
                column: "LockToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReserveSlotLock_OutboundReserveId",
                table: "ReserveSlotLock",
                column: "OutboundReserveId");

            migrationBuilder.CreateIndex(
                name: "IX_ReserveSlotLock_ReturnReserveId",
                table: "ReserveSlotLock",
                column: "ReturnReserveId");

            migrationBuilder.CreateIndex(
                name: "IX_ReserveSlotLock_Status_ExpiresAt",
                table: "ReserveSlotLock",
                columns: new[] { "Status", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReserveSlotLock");

            migrationBuilder.UpdateData(
           table: "Role",
           keyColumn: "RoleId",
           keyValue: 1,
           column: "CreatedDate",
           value: new DateTime(2025, 8, 29, 21, 26, 36, 204, DateTimeKind.Utc).AddTicks(2800));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2025, 8, 29, 21, 26, 36, 204, DateTimeKind.Utc).AddTicks(2803));

            migrationBuilder.CreateIndex(
                name: "IX_CustomerBookingHistory_BookingDate",
                table: "CustomerBookingHistory",
                column: "BookingDate");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerBookingHistory_CustomerId_ReserveId_Role",
                table: "CustomerBookingHistory",
                columns: new[] { "CustomerId", "ReserveId", "Role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerBookingHistory_ReserveId",
                table: "CustomerBookingHistory",
                column: "ReserveId");
        }
    }
}
