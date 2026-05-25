using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace transport.infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class FlattenServiceDropServiceSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reserve_ServiceSchedule_ServiceScheduleId",
                table: "Reserve");

            migrationBuilder.DropTable(
                name: "ServiceSchedule");

            migrationBuilder.DropIndex(
                name: "IX_Reserve_ServiceScheduleId",
                table: "Reserve");

            migrationBuilder.DropColumn(
                name: "EndDay",
                table: "Service");

            migrationBuilder.DropColumn(
                name: "ServiceScheduleId",
                table: "Reserve");

            migrationBuilder.RenameColumn(
                name: "StartDay",
                table: "Service",
                newName: "DayOfWeek");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Service",
                type: "VARCHAR(20)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<TimeSpan>(
                name: "DepartureHour",
                table: "Service",
                type: "time",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));

            migrationBuilder.AddColumn<bool>(
                name: "IsHoliday",
                table: "Service",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 5, 16, 20, 13, 0, 152, DateTimeKind.Utc).AddTicks(2225));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2026, 5, 16, 20, 13, 0, 152, DateTimeKind.Utc).AddTicks(2226));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 3,
                column: "CreatedDate",
                value: new DateTime(2026, 5, 16, 20, 13, 0, 152, DateTimeKind.Utc).AddTicks(2227));

            migrationBuilder.CreateIndex(
                name: "IX_Service_TenantId_TripId_DayOfWeek_DepartureHour",
                table: "Service",
                columns: new[] { "TenantId", "TripId", "DayOfWeek", "DepartureHour" },
                unique: true,
                filter: "[Status] = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_Reserve_TenantId_TripId_ReserveDate_DepartureHour",
                table: "Reserve",
                columns: new[] { "TenantId", "TripId", "ReserveDate", "DepartureHour" },
                unique: true,
                filter: "[Status] <> 'Cancelled' AND [Status] <> 'Expired'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Service_TenantId_TripId_DayOfWeek_DepartureHour",
                table: "Service");

            migrationBuilder.DropIndex(
                name: "IX_Reserve_TenantId_TripId_ReserveDate_DepartureHour",
                table: "Reserve");

            migrationBuilder.DropColumn(
                name: "DepartureHour",
                table: "Service");

            migrationBuilder.DropColumn(
                name: "IsHoliday",
                table: "Service");

            migrationBuilder.RenameColumn(
                name: "DayOfWeek",
                table: "Service",
                newName: "StartDay");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Service",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "VARCHAR(20)");

            migrationBuilder.AddColumn<int>(
                name: "EndDay",
                table: "Service",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ServiceScheduleId",
                table: "Reserve",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ServiceSchedule",
                columns: table => new
                {
                    ServiceScheduleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceId = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: false, defaultValue: "System"),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    DepartureHour = table.Column<TimeSpan>(type: "time", nullable: false),
                    IsHoliday = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    UpdatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceSchedule", x => x.ServiceScheduleId);
                    table.ForeignKey(
                        name: "FK_ServiceSchedule_Service_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Service",
                        principalColumn: "ServiceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServiceSchedule_Tenant_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenant",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 5, 14, 0, 50, 51, 247, DateTimeKind.Utc).AddTicks(6865));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2026, 5, 14, 0, 50, 51, 247, DateTimeKind.Utc).AddTicks(6867));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 3,
                column: "CreatedDate",
                value: new DateTime(2026, 5, 14, 0, 50, 51, 247, DateTimeKind.Utc).AddTicks(6868));

            migrationBuilder.CreateIndex(
                name: "IX_Reserve_ServiceScheduleId",
                table: "Reserve",
                column: "ServiceScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceSchedule_ServiceId",
                table: "ServiceSchedule",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceSchedule_TenantId",
                table: "ServiceSchedule",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_Reserve_ServiceSchedule_ServiceScheduleId",
                table: "Reserve",
                column: "ServiceScheduleId",
                principalTable: "ServiceSchedule",
                principalColumn: "ServiceScheduleId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
