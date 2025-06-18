using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Transport.Infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReservePayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "CustomerReserve");

            migrationBuilder.DropColumn(
                name: "StatusPayment",
                table: "CustomerReserve");

            migrationBuilder.AddColumn<int>(
                name: "ReferencePaymentId",
                table: "CustomerReserve",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReservePayment",
                columns: table => new
                {
                    ReservePaymentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReserveId = table.Column<int>(type: "int", nullable: false),
                    Method = table.Column<string>(type: "VARCHAR(20)", nullable: false),
                    Status = table.Column<string>(type: "VARCHAR(20)", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ParentReservePaymentId = table.Column<int>(type: "int", nullable: true),
                    CreatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: false, defaultValue: "System"),
                    UpdatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservePayment", x => x.ReservePaymentId);
                    table.ForeignKey(
                        name: "FK_ReservePayment_Customer_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customer",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReservePayment_ReservePayment_ParentReservePaymentId",
                        column: x => x.ParentReservePaymentId,
                        principalTable: "ReservePayment",
                        principalColumn: "ReservePaymentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservePayment_Reserve_ReserveId",
                        column: x => x.ReserveId,
                        principalTable: "Reserve",
                        principalColumn: "ReserveId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2025, 6, 18, 4, 36, 0, 103, DateTimeKind.Utc).AddTicks(2752));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2025, 6, 18, 4, 36, 0, 103, DateTimeKind.Utc).AddTicks(2754));

            migrationBuilder.CreateIndex(
                name: "IX_ReservePayment_CustomerId",
                table: "ReservePayment",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservePayment_ParentReservePaymentId",
                table: "ReservePayment",
                column: "ParentReservePaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservePayment_ReserveId",
                table: "ReservePayment",
                column: "ReserveId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReservePayment");

            migrationBuilder.DropColumn(
                name: "ReferencePaymentId",
                table: "CustomerReserve");

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "CustomerReserve",
                type: "VARCHAR(20)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StatusPayment",
                table: "CustomerReserve",
                type: "int",
                maxLength: 50,
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2025, 6, 7, 1, 18, 11, 374, DateTimeKind.Utc).AddTicks(7054));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2025, 6, 7, 1, 18, 11, 374, DateTimeKind.Utc).AddTicks(7058));
        }
    }
}
