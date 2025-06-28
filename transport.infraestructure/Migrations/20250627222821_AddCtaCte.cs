using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Transport.Infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCtaCte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CurrentBalance",
                table: "Customer",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "CustomerAccountTransactions",
                columns: table => new
                {
                    CustomerAccountTransactionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    RelatedReserveId = table.Column<int>(type: "int", nullable: true),
                    ReservePaymentId = table.Column<int>(type: "int", nullable: true),
                    CreatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: false, defaultValue: "System"),
                    UpdatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerAccountTransactions", x => x.CustomerAccountTransactionId);
                    table.ForeignKey(
                        name: "FK_CustomerAccountTransactions_Customer_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customer",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerAccountTransactions_ReservePayment_ReservePaymentId",
                        column: x => x.ReservePaymentId,
                        principalTable: "ReservePayment",
                        principalColumn: "ReservePaymentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerAccountTransactions_Reserve_RelatedReserveId",
                        column: x => x.RelatedReserveId,
                        principalTable: "Reserve",
                        principalColumn: "ReserveId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2025, 6, 27, 22, 28, 21, 11, DateTimeKind.Utc).AddTicks(427));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2025, 6, 27, 22, 28, 21, 11, DateTimeKind.Utc).AddTicks(429));

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAccountTransactions_CustomerId",
                table: "CustomerAccountTransactions",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAccountTransactions_Date",
                table: "CustomerAccountTransactions",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAccountTransactions_RelatedReserveId",
                table: "CustomerAccountTransactions",
                column: "RelatedReserveId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAccountTransactions_ReservePaymentId",
                table: "CustomerAccountTransactions",
                column: "ReservePaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAccountTransactions_Type",
                table: "CustomerAccountTransactions",
                column: "Type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerAccountTransactions");

            migrationBuilder.DropColumn(
                name: "CurrentBalance",
                table: "Customer");

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
        }
    }
}
