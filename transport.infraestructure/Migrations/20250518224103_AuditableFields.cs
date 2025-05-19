using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Transport.Infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class AuditableFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReservePrice_ServiceId",
                table: "ReservePrice");

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "VehicleType",
                type: "VARCHAR(256)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDate",
                table: "VehicleType",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETDATE()");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "VehicleType",
                type: "VARCHAR(256)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedDate",
                table: "VehicleType",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Vehicle",
                type: "VARCHAR(256)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDate",
                table: "Vehicle",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETDATE()");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Vehicle",
                type: "VARCHAR(256)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedDate",
                table: "Vehicle",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ServiceCustomer",
                type: "VARCHAR(256)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDate",
                table: "ServiceCustomer",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETDATE()");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "ServiceCustomer",
                type: "VARCHAR(256)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedDate",
                table: "ServiceCustomer",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Service",
                type: "VARCHAR(256)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDate",
                table: "Service",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETDATE()");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Service",
                type: "VARCHAR(256)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedDate",
                table: "Service",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Role",
                type: "VARCHAR(256)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDate",
                table: "Role",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETDATE()");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Role",
                type: "VARCHAR(256)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedDate",
                table: "Role",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ReservePrice",
                type: "VARCHAR(256)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDate",
                table: "ReservePrice",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETDATE()");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "ReservePrice",
                type: "VARCHAR(256)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedDate",
                table: "ReservePrice",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Reserve",
                type: "VARCHAR(256)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDate",
                table: "Reserve",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETDATE()");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Reserve",
                type: "VARCHAR(256)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedDate",
                table: "Reserve",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "RefreshToken",
                type: "VARCHAR(256)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDate",
                table: "RefreshToken",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETDATE()");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "RefreshToken",
                type: "VARCHAR(256)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedDate",
                table: "RefreshToken",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Holiday",
                type: "VARCHAR(256)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDate",
                table: "Holiday",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETDATE()");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Holiday",
                type: "VARCHAR(256)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedDate",
                table: "Holiday",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Driver",
                type: "VARCHAR(256)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDate",
                table: "Driver",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETDATE()");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Driver",
                type: "VARCHAR(256)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedDate",
                table: "Driver",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Direction",
                type: "VARCHAR(256)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDate",
                table: "Direction",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETDATE()");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Direction",
                type: "VARCHAR(256)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedDate",
                table: "Direction",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "PickupLocationId",
                table: "CustomerReserve",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "DropoffLocationId",
                table: "CustomerReserve",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "CustomerReserve",
                type: "VARCHAR(256)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDate",
                table: "CustomerReserve",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETDATE()");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "CustomerReserve",
                type: "VARCHAR(256)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedDate",
                table: "CustomerReserve",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "CustomerReserve",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Customer",
                type: "VARCHAR(256)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDate",
                table: "Customer",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETDATE()");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Customer",
                type: "VARCHAR(256)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedDate",
                table: "Customer",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "City",
                type: "VARCHAR(256)",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDate",
                table: "City",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETDATE()");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "City",
                type: "VARCHAR(256)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedDate",
                table: "City",
                type: "datetime2",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 1,
                columns: new[] { "CreatedBy", "CreatedDate", "UpdatedBy", "UpdatedDate" },
                values: new object[] { "System", new DateTime(2025, 5, 18, 22, 41, 3, 244, DateTimeKind.Utc).AddTicks(1076), null, null });

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 2,
                columns: new[] { "CreatedBy", "CreatedDate", "UpdatedBy", "UpdatedDate" },
                values: new object[] { "System", new DateTime(2025, 5, 18, 22, 41, 3, 244, DateTimeKind.Utc).AddTicks(1079), null, null });

            migrationBuilder.CreateIndex(
                name: "IX_ReservePrice_ServiceId",
                table: "ReservePrice",
                column: "ServiceId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReservePrice_ServiceId",
                table: "ReservePrice");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "VehicleType");

            migrationBuilder.DropColumn(
                name: "CreatedDate",
                table: "VehicleType");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "VehicleType");

            migrationBuilder.DropColumn(
                name: "UpdatedDate",
                table: "VehicleType");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Vehicle");

            migrationBuilder.DropColumn(
                name: "CreatedDate",
                table: "Vehicle");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Vehicle");

            migrationBuilder.DropColumn(
                name: "UpdatedDate",
                table: "Vehicle");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ServiceCustomer");

            migrationBuilder.DropColumn(
                name: "CreatedDate",
                table: "ServiceCustomer");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "ServiceCustomer");

            migrationBuilder.DropColumn(
                name: "UpdatedDate",
                table: "ServiceCustomer");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Service");

            migrationBuilder.DropColumn(
                name: "CreatedDate",
                table: "Service");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Service");

            migrationBuilder.DropColumn(
                name: "UpdatedDate",
                table: "Service");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Role");

            migrationBuilder.DropColumn(
                name: "CreatedDate",
                table: "Role");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Role");

            migrationBuilder.DropColumn(
                name: "UpdatedDate",
                table: "Role");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ReservePrice");

            migrationBuilder.DropColumn(
                name: "CreatedDate",
                table: "ReservePrice");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "ReservePrice");

            migrationBuilder.DropColumn(
                name: "UpdatedDate",
                table: "ReservePrice");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Reserve");

            migrationBuilder.DropColumn(
                name: "CreatedDate",
                table: "Reserve");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Reserve");

            migrationBuilder.DropColumn(
                name: "UpdatedDate",
                table: "Reserve");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "RefreshToken");

            migrationBuilder.DropColumn(
                name: "CreatedDate",
                table: "RefreshToken");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "RefreshToken");

            migrationBuilder.DropColumn(
                name: "UpdatedDate",
                table: "RefreshToken");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Holiday");

            migrationBuilder.DropColumn(
                name: "CreatedDate",
                table: "Holiday");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Holiday");

            migrationBuilder.DropColumn(
                name: "UpdatedDate",
                table: "Holiday");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Driver");

            migrationBuilder.DropColumn(
                name: "CreatedDate",
                table: "Driver");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Driver");

            migrationBuilder.DropColumn(
                name: "UpdatedDate",
                table: "Driver");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Direction");

            migrationBuilder.DropColumn(
                name: "CreatedDate",
                table: "Direction");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Direction");

            migrationBuilder.DropColumn(
                name: "UpdatedDate",
                table: "Direction");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "CustomerReserve");

            migrationBuilder.DropColumn(
                name: "CreatedDate",
                table: "CustomerReserve");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "CustomerReserve");

            migrationBuilder.DropColumn(
                name: "UpdatedDate",
                table: "CustomerReserve");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "CustomerReserve");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Customer");

            migrationBuilder.DropColumn(
                name: "CreatedDate",
                table: "Customer");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Customer");

            migrationBuilder.DropColumn(
                name: "UpdatedDate",
                table: "Customer");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "City");

            migrationBuilder.DropColumn(
                name: "CreatedDate",
                table: "City");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "City");

            migrationBuilder.DropColumn(
                name: "UpdatedDate",
                table: "City");

            migrationBuilder.AlterColumn<int>(
                name: "PickupLocationId",
                table: "CustomerReserve",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "DropoffLocationId",
                table: "CustomerReserve",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReservePrice_ServiceId",
                table: "ReservePrice",
                column: "ServiceId");
        }
    }
}
