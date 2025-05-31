using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Transport.Infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DepartureHour",
                table: "Service");

            migrationBuilder.DropColumn(
                name: "EndDay",
                table: "Service");

            migrationBuilder.DropColumn(
                name: "IsHoliday",
                table: "Service");

            migrationBuilder.DropColumn(
                name: "StartDay",
                table: "Service");

            migrationBuilder.AddColumn<int>(
                name: "ServiceScheduleId",
                table: "Reserve",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ServiceSchedule",
                columns: table => new
                {
                    ServiceScheduleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceId = table.Column<int>(type: "int", nullable: false),
                    StartDay = table.Column<int>(type: "int", nullable: false),
                    EndDay = table.Column<int>(type: "int", nullable: false),
                    DepartureHour = table.Column<TimeSpan>(type: "time", nullable: false),
                    IsHoliday = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: false, defaultValue: "System"),
                    UpdatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
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
                });

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2025, 5, 30, 1, 26, 3, 410, DateTimeKind.Utc).AddTicks(1367));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2025, 5, 30, 1, 26, 3, 410, DateTimeKind.Utc).AddTicks(1370));

            migrationBuilder.CreateIndex(
                name: "IX_Reserve_ServiceScheduleId",
                table: "Reserve",
                column: "ServiceScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceSchedule_ServiceId",
                table: "ServiceSchedule",
                column: "ServiceId");

            migrationBuilder.AddForeignKey(
                name: "FK_Reserve_ServiceSchedule_ServiceScheduleId",
                table: "Reserve",
                column: "ServiceScheduleId",
                principalTable: "ServiceSchedule",
                principalColumn: "ServiceScheduleId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
                name: "ServiceScheduleId",
                table: "Reserve");

            migrationBuilder.AddColumn<TimeSpan>(
                name: "DepartureHour",
                table: "Service",
                type: "time",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));

            migrationBuilder.AddColumn<int>(
                name: "EndDay",
                table: "Service",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsHoliday",
                table: "Service",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "StartDay",
                table: "Service",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2025, 5, 26, 11, 33, 42, 541, DateTimeKind.Utc).AddTicks(5717));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2025, 5, 26, 11, 33, 42, 541, DateTimeKind.Utc).AddTicks(5719));
        }
    }
}
