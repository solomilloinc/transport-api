using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace transport.infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantReserveConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantReserveConfig",
                columns: table => new
                {
                    TenantReserveConfigId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    RoundTripSameDayOnly = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: false, defaultValue: "System"),
                    UpdatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantReserveConfig", x => x.TenantReserveConfigId);
                    table.ForeignKey(
                        name: "FK_TenantReserveConfig_Tenant_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenant",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 4, 20, 23, 12, 26, 510, DateTimeKind.Utc).AddTicks(4981));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2026, 4, 20, 23, 12, 26, 510, DateTimeKind.Utc).AddTicks(4982));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 3,
                column: "CreatedDate",
                value: new DateTime(2026, 4, 20, 23, 12, 26, 510, DateTimeKind.Utc).AddTicks(4984));

            migrationBuilder.CreateIndex(
                name: "IX_TenantReserveConfig_TenantId",
                table: "TenantReserveConfig",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantReserveConfig");

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 3, 15, 18, 39, 8, 252, DateTimeKind.Utc).AddTicks(8811));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2026, 3, 15, 18, 39, 8, 252, DateTimeKind.Utc).AddTicks(8813));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 3,
                column: "CreatedDate",
                value: new DateTime(2026, 3, 15, 18, 39, 8, 252, DateTimeKind.Utc).AddTicks(8815));
        }
    }
}
