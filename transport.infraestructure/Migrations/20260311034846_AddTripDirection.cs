using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace transport.infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTripDirection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TripDirection",
                columns: table => new
                {
                    TripDirectionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TripId = table.Column<int>(type: "int", nullable: false),
                    DirectionId = table.Column<int>(type: "int", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    PickupTimeOffset = table.Column<TimeSpan>(type: "time", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "VARCHAR(256)", maxLength: 100, nullable: false, defaultValue: "System"),
                    UpdatedBy = table.Column<string>(type: "VARCHAR(256)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripDirection", x => x.TripDirectionId);
                    table.ForeignKey(
                        name: "FK_TripDirection_Direction_DirectionId",
                        column: x => x.DirectionId,
                        principalTable: "Direction",
                        principalColumn: "DirectionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TripDirection_Trip_TripId",
                        column: x => x.TripId,
                        principalTable: "Trip",
                        principalColumn: "TripId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 3, 11, 3, 48, 45, 958, DateTimeKind.Utc).AddTicks(2998));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2026, 3, 11, 3, 48, 45, 958, DateTimeKind.Utc).AddTicks(3000));

            migrationBuilder.CreateIndex(
                name: "IX_TripDirection_DirectionId",
                table: "TripDirection",
                column: "DirectionId");

            migrationBuilder.CreateIndex(
                name: "IX_TripDirection_TripId_DirectionId",
                table: "TripDirection",
                columns: new[] { "TripId", "DirectionId" },
                unique: true,
                filter: "[Status] = 'Active'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TripDirection");

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
    }
}
