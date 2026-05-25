using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace transport.infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFrequentSubscriptionRemoveServiceCustomer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceCustomer");

            migrationBuilder.AddColumn<int>(
                name: "FrequentSubscriptionId",
                table: "Passenger",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FrequentSubscription",
                columns: table => new
                {
                    FrequentSubscriptionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    ReserveTypeId = table.Column<string>(type: "VARCHAR(20)", nullable: false),
                    OutboundServiceId = table.Column<int>(type: "int", nullable: false),
                    InboundServiceId = table.Column<int>(type: "int", nullable: true),
                    OutboundPickupLocationId = table.Column<int>(type: "int", nullable: false),
                    OutboundDropoffLocationId = table.Column<int>(type: "int", nullable: false),
                    InboundPickupLocationId = table.Column<int>(type: "int", nullable: true),
                    InboundDropoffLocationId = table.Column<int>(type: "int", nullable: true),
                    StartDate = table.Column<DateTime>(type: "date", nullable: false),
                    EndDate = table.Column<DateTime>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "VARCHAR(20)", nullable: false),
                    CreatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: false, defaultValue: "System"),
                    UpdatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FrequentSubscription", x => x.FrequentSubscriptionId);
                    table.ForeignKey(
                        name: "FK_FrequentSubscription_Customer_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customer",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FrequentSubscription_Direction_InboundDropoffLocationId",
                        column: x => x.InboundDropoffLocationId,
                        principalTable: "Direction",
                        principalColumn: "DirectionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FrequentSubscription_Direction_InboundPickupLocationId",
                        column: x => x.InboundPickupLocationId,
                        principalTable: "Direction",
                        principalColumn: "DirectionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FrequentSubscription_Direction_OutboundDropoffLocationId",
                        column: x => x.OutboundDropoffLocationId,
                        principalTable: "Direction",
                        principalColumn: "DirectionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FrequentSubscription_Direction_OutboundPickupLocationId",
                        column: x => x.OutboundPickupLocationId,
                        principalTable: "Direction",
                        principalColumn: "DirectionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FrequentSubscription_Service_InboundServiceId",
                        column: x => x.InboundServiceId,
                        principalTable: "Service",
                        principalColumn: "ServiceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FrequentSubscription_Service_OutboundServiceId",
                        column: x => x.OutboundServiceId,
                        principalTable: "Service",
                        principalColumn: "ServiceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FrequentSubscription_Tenant_TenantId",
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
                value: new DateTime(2026, 5, 17, 1, 18, 57, 669, DateTimeKind.Utc).AddTicks(3639));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2026, 5, 17, 1, 18, 57, 669, DateTimeKind.Utc).AddTicks(3641));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 3,
                column: "CreatedDate",
                value: new DateTime(2026, 5, 17, 1, 18, 57, 669, DateTimeKind.Utc).AddTicks(3642));

            migrationBuilder.CreateIndex(
                name: "IX_Passenger_FrequentSubscriptionId",
                table: "Passenger",
                column: "FrequentSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_FrequentSubscription_CustomerId",
                table: "FrequentSubscription",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_FrequentSubscription_InboundDropoffLocationId",
                table: "FrequentSubscription",
                column: "InboundDropoffLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_FrequentSubscription_InboundPickupLocationId",
                table: "FrequentSubscription",
                column: "InboundPickupLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_FrequentSubscription_InboundServiceId",
                table: "FrequentSubscription",
                column: "InboundServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_FrequentSubscription_OutboundDropoffLocationId",
                table: "FrequentSubscription",
                column: "OutboundDropoffLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_FrequentSubscription_OutboundPickupLocationId",
                table: "FrequentSubscription",
                column: "OutboundPickupLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_FrequentSubscription_OutboundServiceId",
                table: "FrequentSubscription",
                column: "OutboundServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_FrequentSubscription_TenantId",
                table: "FrequentSubscription",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FrequentSubscription_TenantId_CustomerId_OutboundServiceId",
                table: "FrequentSubscription",
                columns: new[] { "TenantId", "CustomerId", "OutboundServiceId" },
                unique: true,
                filter: "[Status] = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_FrequentSubscription_TenantId_InboundServiceId_Status",
                table: "FrequentSubscription",
                columns: new[] { "TenantId", "InboundServiceId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_FrequentSubscription_TenantId_OutboundServiceId_Status",
                table: "FrequentSubscription",
                columns: new[] { "TenantId", "OutboundServiceId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_Passenger_FrequentSubscription_FrequentSubscriptionId",
                table: "Passenger",
                column: "FrequentSubscriptionId",
                principalTable: "FrequentSubscription",
                principalColumn: "FrequentSubscriptionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Passenger_FrequentSubscription_FrequentSubscriptionId",
                table: "Passenger");

            migrationBuilder.DropTable(
                name: "FrequentSubscription");

            migrationBuilder.DropIndex(
                name: "IX_Passenger_FrequentSubscriptionId",
                table: "Passenger");

            migrationBuilder.DropColumn(
                name: "FrequentSubscriptionId",
                table: "Passenger");

            migrationBuilder.CreateTable(
                name: "ServiceCustomer",
                columns: table => new
                {
                    ServiceCustomerId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    ServiceId = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: false, defaultValue: "System"),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    TenantId = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    UpdatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceCustomer", x => x.ServiceCustomerId);
                    table.ForeignKey(
                        name: "FK_ServiceCustomer_Customer_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customer",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServiceCustomer_Service_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Service",
                        principalColumn: "ServiceId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServiceCustomer_Tenant_TenantId",
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
                name: "IX_ServiceCustomer_CustomerId",
                table: "ServiceCustomer",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceCustomer_ServiceId",
                table: "ServiceCustomer",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceCustomer_TenantId",
                table: "ServiceCustomer",
                column: "TenantId");
        }
    }
}
