using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Transport.Infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMpPayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Payment",
                columns: table => new
                {
                    PaymentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PaymentMpId = table.Column<long>(type: "bigint", nullable: true),
                    ExternalReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StatusDetail = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PaymentTypeId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PaymentMethodId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Installments = table.Column<int>(type: "int", nullable: true),
                    CardLastFourDigits = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    CardHolderName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AuthorizationCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FeeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    NetReceivedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    RefundedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Captured = table.Column<bool>(type: "bit", nullable: true),
                    DateCreatedMp = table.Column<DateTime>(type: "datetime", nullable: true),
                    DateApproved = table.Column<DateTime>(type: "datetime", nullable: true),
                    DateLastUpdated = table.Column<DateTime>(type: "datetime", nullable: true),
                    RawJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TransactionDetails = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: false, defaultValue: "System"),
                    UpdatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payment", x => x.PaymentId);
                });

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2025, 5, 22, 2, 32, 28, 238, DateTimeKind.Utc).AddTicks(7920));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2025, 5, 22, 2, 32, 28, 238, DateTimeKind.Utc).AddTicks(7922));

            migrationBuilder.CreateIndex(
                name: "IX_Payment_Email",
                table: "Payment",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Payment_ExternalReference",
                table: "Payment",
                column: "ExternalReference");

            migrationBuilder.CreateIndex(
                name: "IX_Payment_PaymentMpId",
                table: "Payment",
                column: "PaymentMpId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Payment");

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2025, 5, 18, 22, 41, 3, 244, DateTimeKind.Utc).AddTicks(1076));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2025, 5, 18, 22, 41, 3, 244, DateTimeKind.Utc).AddTicks(1079));
        }
    }
}
