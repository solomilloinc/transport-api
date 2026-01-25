using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace transport.infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDirectionsInReserveAndService : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReserveDirection",
                columns: table => new
                {
                    ReserveDirectionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReserveId = table.Column<int>(type: "int", nullable: false),
                    DirectionId = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: false, defaultValue: "System"),
                    UpdatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReserveDirection", x => x.ReserveDirectionId);
                    table.ForeignKey(
                        name: "FK_ReserveDirection_Direction_DirectionId",
                        column: x => x.DirectionId,
                        principalTable: "Direction",
                        principalColumn: "DirectionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReserveDirection_Reserve_ReserveId",
                        column: x => x.ReserveId,
                        principalTable: "Reserve",
                        principalColumn: "ReserveId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceDirection",
                columns: table => new
                {
                    ServiceDirectionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceId = table.Column<int>(type: "int", nullable: false),
                    DirectionId = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: false, defaultValue: "System"),
                    UpdatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceDirection", x => x.ServiceDirectionId);
                    table.ForeignKey(
                        name: "FK_ServiceDirection_Direction_DirectionId",
                        column: x => x.DirectionId,
                        principalTable: "Direction",
                        principalColumn: "DirectionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServiceDirection_Service_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Service",
                        principalColumn: "ServiceId",
                        onDelete: ReferentialAction.Cascade);
                });

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
                name: "IX_ReserveDirection_DirectionId",
                table: "ReserveDirection",
                column: "DirectionId");

            migrationBuilder.CreateIndex(
                name: "IX_ReserveDirection_ReserveId_DirectionId",
                table: "ReserveDirection",
                columns: new[] { "ReserveId", "DirectionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceDirection_DirectionId",
                table: "ServiceDirection",
                column: "DirectionId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceDirection_ServiceId_DirectionId",
                table: "ServiceDirection",
                columns: new[] { "ServiceId", "DirectionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReserveDirection");

            migrationBuilder.DropTable(
                name: "ServiceDirection");

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 1, 19, 22, 28, 2, 182, DateTimeKind.Utc).AddTicks(9444));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2026, 1, 19, 22, 28, 2, 182, DateTimeKind.Utc).AddTicks(9448));
        }
    }
}
