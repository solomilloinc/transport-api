using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace transport.infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class Multitenancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Trip_OriginCityId_DestinationCityId",
                table: "Trip");

            migrationBuilder.DropIndex(
                name: "IX_Holiday_HolidayDate",
                table: "Holiday");

            migrationBuilder.DropIndex(
                name: "IX_Driver_DocumentNumber",
                table: "Driver");

            migrationBuilder.DropIndex(
                name: "IX_Customer_DocumentNumber",
                table: "Customer");

            migrationBuilder.DropIndex(
                name: "IX_Customer_Email",
                table: "Customer");

            migrationBuilder.DropIndex(
                name: "IX_City_Code",
                table: "City");

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "VehicleType",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Vehicle",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "User",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "TripPrice",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "TripPickupStop",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Trip",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "ServiceSchedule",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "ServiceDirection",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "ServiceCustomer",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Service",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "ReserveSlotLock",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "ReservePayment",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "ReserveDirection",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Reserve",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Passenger",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Holiday",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Driver",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Direction",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "CustomerAccountTransactions",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Customer",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "City",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "CashBox",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "Tenant",
                columns: table => new
                {
                    TenantId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Domain = table.Column<string>(type: "nvarchar(253)", maxLength: 253, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: false, defaultValue: "System"),
                    UpdatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenant", x => x.TenantId);
                });

            // Seed default tenant BEFORE creating FKs (existing rows have TenantId = 1)
            migrationBuilder.Sql(@"
                SET IDENTITY_INSERT [Tenant] ON;
                INSERT INTO [Tenant] ([TenantId], [Code], [Name], [Status], [CreatedBy], [CreatedDate])
                VALUES (1, 'default', 'Default Tenant', 0, 'System', GETDATE());
                SET IDENTITY_INSERT [Tenant] OFF;
            ");

            // Set TenantId = 1 for existing users (column was added with default 0)
            migrationBuilder.Sql("UPDATE [User] SET [TenantId] = 1 WHERE [TenantId] = 0;");

            migrationBuilder.CreateTable(
                name: "TenantConfig",
                columns: table => new
                {
                    TenantConfigId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CompanyNameShort = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CompanyNameLegal = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    LogoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FaviconUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Tagline = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ContactAddress = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ContactPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ContactEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BookingsEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TermsText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CancellationPolicy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StyleConfigJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: false, defaultValue: "System"),
                    UpdatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantConfig", x => x.TenantConfigId);
                    table.ForeignKey(
                        name: "FK_TenantConfig_Tenant_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenant",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantPaymentConfig",
                columns: table => new
                {
                    TenantPaymentConfigId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    AccessToken = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PublicKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    WebhookSecret = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: false, defaultValue: "System"),
                    UpdatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantPaymentConfig", x => x.TenantPaymentConfigId);
                    table.ForeignKey(
                        name: "FK_TenantPaymentConfig_Tenant_TenantId",
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
                value: new DateTime(2026, 3, 15, 18, 39, 8, 252, DateTimeKind.Utc).AddTicks(8811));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2026, 3, 15, 18, 39, 8, 252, DateTimeKind.Utc).AddTicks(8813));

            migrationBuilder.InsertData(
                table: "Role",
                columns: new[] { "RoleId", "CreatedBy", "CreatedDate", "Name", "UpdatedBy", "UpdatedDate" },
                values: new object[] { 3, "System", new DateTime(2026, 3, 15, 18, 39, 8, 252, DateTimeKind.Utc).AddTicks(8815), "SuperAdmin", null, null });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleType_TenantId",
                table: "VehicleType",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicle_TenantId",
                table: "Vehicle",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_User_TenantId",
                table: "User",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TripPrice_TenantId",
                table: "TripPrice",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TripPickupStop_TenantId",
                table: "TripPickupStop",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Trip_OriginCityId",
                table: "Trip",
                column: "OriginCityId");

            migrationBuilder.CreateIndex(
                name: "IX_Trip_TenantId",
                table: "Trip",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Trip_TenantId_OriginCityId_DestinationCityId",
                table: "Trip",
                columns: new[] { "TenantId", "OriginCityId", "DestinationCityId" },
                unique: true,
                filter: "[Status] = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceSchedule_TenantId",
                table: "ServiceSchedule",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceDirection_TenantId",
                table: "ServiceDirection",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceCustomer_TenantId",
                table: "ServiceCustomer",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Service_TenantId",
                table: "Service",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ReserveSlotLock_TenantId",
                table: "ReserveSlotLock",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservePayment_TenantId",
                table: "ReservePayment",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ReserveDirection_TenantId",
                table: "ReserveDirection",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Reserve_TenantId",
                table: "Reserve",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Passenger_TenantId",
                table: "Passenger",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Holiday_TenantId",
                table: "Holiday",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Holiday_TenantId_HolidayDate",
                table: "Holiday",
                columns: new[] { "TenantId", "HolidayDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Driver_TenantId",
                table: "Driver",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Driver_TenantId_DocumentNumber",
                table: "Driver",
                columns: new[] { "TenantId", "DocumentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Direction_TenantId",
                table: "Direction",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAccountTransactions_TenantId",
                table: "CustomerAccountTransactions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Customer_TenantId",
                table: "Customer",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Customer_TenantId_DocumentNumber",
                table: "Customer",
                columns: new[] { "TenantId", "DocumentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customer_TenantId_Email",
                table: "Customer",
                columns: new[] { "TenantId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_City_TenantId",
                table: "City",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_City_TenantId_Code",
                table: "City",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashBox_TenantId",
                table: "CashBox",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenant_Code",
                table: "Tenant",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenant_Domain",
                table: "Tenant",
                column: "Domain",
                unique: true,
                filter: "[Domain] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TenantConfig_TenantId",
                table: "TenantConfig",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantPaymentConfig_TenantId",
                table: "TenantPaymentConfig",
                column: "TenantId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CashBox_Tenant_TenantId",
                table: "CashBox",
                column: "TenantId",
                principalTable: "Tenant",
                principalColumn: "TenantId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_City_Tenant_TenantId",
                table: "City",
                column: "TenantId",
                principalTable: "Tenant",
                principalColumn: "TenantId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Customer_Tenant_TenantId",
                table: "Customer",
                column: "TenantId",
                principalTable: "Tenant",
                principalColumn: "TenantId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerAccountTransactions_Tenant_TenantId",
                table: "CustomerAccountTransactions",
                column: "TenantId",
                principalTable: "Tenant",
                principalColumn: "TenantId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Direction_Tenant_TenantId",
                table: "Direction",
                column: "TenantId",
                principalTable: "Tenant",
                principalColumn: "TenantId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Driver_Tenant_TenantId",
                table: "Driver",
                column: "TenantId",
                principalTable: "Tenant",
                principalColumn: "TenantId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Holiday_Tenant_TenantId",
                table: "Holiday",
                column: "TenantId",
                principalTable: "Tenant",
                principalColumn: "TenantId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Passenger_Tenant_TenantId",
                table: "Passenger",
                column: "TenantId",
                principalTable: "Tenant",
                principalColumn: "TenantId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Reserve_Tenant_TenantId",
                table: "Reserve",
                column: "TenantId",
                principalTable: "Tenant",
                principalColumn: "TenantId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReserveDirection_Tenant_TenantId",
                table: "ReserveDirection",
                column: "TenantId",
                principalTable: "Tenant",
                principalColumn: "TenantId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReservePayment_Tenant_TenantId",
                table: "ReservePayment",
                column: "TenantId",
                principalTable: "Tenant",
                principalColumn: "TenantId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReserveSlotLock_Tenant_TenantId",
                table: "ReserveSlotLock",
                column: "TenantId",
                principalTable: "Tenant",
                principalColumn: "TenantId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Service_Tenant_TenantId",
                table: "Service",
                column: "TenantId",
                principalTable: "Tenant",
                principalColumn: "TenantId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceCustomer_Tenant_TenantId",
                table: "ServiceCustomer",
                column: "TenantId",
                principalTable: "Tenant",
                principalColumn: "TenantId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceDirection_Tenant_TenantId",
                table: "ServiceDirection",
                column: "TenantId",
                principalTable: "Tenant",
                principalColumn: "TenantId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceSchedule_Tenant_TenantId",
                table: "ServiceSchedule",
                column: "TenantId",
                principalTable: "Tenant",
                principalColumn: "TenantId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Trip_Tenant_TenantId",
                table: "Trip",
                column: "TenantId",
                principalTable: "Tenant",
                principalColumn: "TenantId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TripPickupStop_Tenant_TenantId",
                table: "TripPickupStop",
                column: "TenantId",
                principalTable: "Tenant",
                principalColumn: "TenantId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TripPrice_Tenant_TenantId",
                table: "TripPrice",
                column: "TenantId",
                principalTable: "Tenant",
                principalColumn: "TenantId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_User_Tenant_TenantId",
                table: "User",
                column: "TenantId",
                principalTable: "Tenant",
                principalColumn: "TenantId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Vehicle_Tenant_TenantId",
                table: "Vehicle",
                column: "TenantId",
                principalTable: "Tenant",
                principalColumn: "TenantId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_VehicleType_Tenant_TenantId",
                table: "VehicleType",
                column: "TenantId",
                principalTable: "Tenant",
                principalColumn: "TenantId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CashBox_Tenant_TenantId",
                table: "CashBox");

            migrationBuilder.DropForeignKey(
                name: "FK_City_Tenant_TenantId",
                table: "City");

            migrationBuilder.DropForeignKey(
                name: "FK_Customer_Tenant_TenantId",
                table: "Customer");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomerAccountTransactions_Tenant_TenantId",
                table: "CustomerAccountTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Direction_Tenant_TenantId",
                table: "Direction");

            migrationBuilder.DropForeignKey(
                name: "FK_Driver_Tenant_TenantId",
                table: "Driver");

            migrationBuilder.DropForeignKey(
                name: "FK_Holiday_Tenant_TenantId",
                table: "Holiday");

            migrationBuilder.DropForeignKey(
                name: "FK_Passenger_Tenant_TenantId",
                table: "Passenger");

            migrationBuilder.DropForeignKey(
                name: "FK_Reserve_Tenant_TenantId",
                table: "Reserve");

            migrationBuilder.DropForeignKey(
                name: "FK_ReserveDirection_Tenant_TenantId",
                table: "ReserveDirection");

            migrationBuilder.DropForeignKey(
                name: "FK_ReservePayment_Tenant_TenantId",
                table: "ReservePayment");

            migrationBuilder.DropForeignKey(
                name: "FK_ReserveSlotLock_Tenant_TenantId",
                table: "ReserveSlotLock");

            migrationBuilder.DropForeignKey(
                name: "FK_Service_Tenant_TenantId",
                table: "Service");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiceCustomer_Tenant_TenantId",
                table: "ServiceCustomer");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiceDirection_Tenant_TenantId",
                table: "ServiceDirection");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiceSchedule_Tenant_TenantId",
                table: "ServiceSchedule");

            migrationBuilder.DropForeignKey(
                name: "FK_Trip_Tenant_TenantId",
                table: "Trip");

            migrationBuilder.DropForeignKey(
                name: "FK_TripPickupStop_Tenant_TenantId",
                table: "TripPickupStop");

            migrationBuilder.DropForeignKey(
                name: "FK_TripPrice_Tenant_TenantId",
                table: "TripPrice");

            migrationBuilder.DropForeignKey(
                name: "FK_User_Tenant_TenantId",
                table: "User");

            migrationBuilder.DropForeignKey(
                name: "FK_Vehicle_Tenant_TenantId",
                table: "Vehicle");

            migrationBuilder.DropForeignKey(
                name: "FK_VehicleType_Tenant_TenantId",
                table: "VehicleType");

            migrationBuilder.DropTable(
                name: "TenantConfig");

            migrationBuilder.DropTable(
                name: "TenantPaymentConfig");

            migrationBuilder.DropTable(
                name: "Tenant");

            migrationBuilder.DropIndex(
                name: "IX_VehicleType_TenantId",
                table: "VehicleType");

            migrationBuilder.DropIndex(
                name: "IX_Vehicle_TenantId",
                table: "Vehicle");

            migrationBuilder.DropIndex(
                name: "IX_User_TenantId",
                table: "User");

            migrationBuilder.DropIndex(
                name: "IX_TripPrice_TenantId",
                table: "TripPrice");

            migrationBuilder.DropIndex(
                name: "IX_TripPickupStop_TenantId",
                table: "TripPickupStop");

            migrationBuilder.DropIndex(
                name: "IX_Trip_OriginCityId",
                table: "Trip");

            migrationBuilder.DropIndex(
                name: "IX_Trip_TenantId",
                table: "Trip");

            migrationBuilder.DropIndex(
                name: "IX_Trip_TenantId_OriginCityId_DestinationCityId",
                table: "Trip");

            migrationBuilder.DropIndex(
                name: "IX_ServiceSchedule_TenantId",
                table: "ServiceSchedule");

            migrationBuilder.DropIndex(
                name: "IX_ServiceDirection_TenantId",
                table: "ServiceDirection");

            migrationBuilder.DropIndex(
                name: "IX_ServiceCustomer_TenantId",
                table: "ServiceCustomer");

            migrationBuilder.DropIndex(
                name: "IX_Service_TenantId",
                table: "Service");

            migrationBuilder.DropIndex(
                name: "IX_ReserveSlotLock_TenantId",
                table: "ReserveSlotLock");

            migrationBuilder.DropIndex(
                name: "IX_ReservePayment_TenantId",
                table: "ReservePayment");

            migrationBuilder.DropIndex(
                name: "IX_ReserveDirection_TenantId",
                table: "ReserveDirection");

            migrationBuilder.DropIndex(
                name: "IX_Reserve_TenantId",
                table: "Reserve");

            migrationBuilder.DropIndex(
                name: "IX_Passenger_TenantId",
                table: "Passenger");

            migrationBuilder.DropIndex(
                name: "IX_Holiday_TenantId",
                table: "Holiday");

            migrationBuilder.DropIndex(
                name: "IX_Holiday_TenantId_HolidayDate",
                table: "Holiday");

            migrationBuilder.DropIndex(
                name: "IX_Driver_TenantId",
                table: "Driver");

            migrationBuilder.DropIndex(
                name: "IX_Driver_TenantId_DocumentNumber",
                table: "Driver");

            migrationBuilder.DropIndex(
                name: "IX_Direction_TenantId",
                table: "Direction");

            migrationBuilder.DropIndex(
                name: "IX_CustomerAccountTransactions_TenantId",
                table: "CustomerAccountTransactions");

            migrationBuilder.DropIndex(
                name: "IX_Customer_TenantId",
                table: "Customer");

            migrationBuilder.DropIndex(
                name: "IX_Customer_TenantId_DocumentNumber",
                table: "Customer");

            migrationBuilder.DropIndex(
                name: "IX_Customer_TenantId_Email",
                table: "Customer");

            migrationBuilder.DropIndex(
                name: "IX_City_TenantId",
                table: "City");

            migrationBuilder.DropIndex(
                name: "IX_City_TenantId_Code",
                table: "City");

            migrationBuilder.DropIndex(
                name: "IX_CashBox_TenantId",
                table: "CashBox");

            migrationBuilder.DeleteData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 3);

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "VehicleType");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Vehicle");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "User");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "TripPrice");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "TripPickupStop");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Trip");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ServiceSchedule");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ServiceDirection");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ServiceCustomer");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Service");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ReserveSlotLock");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ReservePayment");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ReserveDirection");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Reserve");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Passenger");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Holiday");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Driver");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Direction");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CustomerAccountTransactions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Customer");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "City");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CashBox");

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 3, 12, 3, 48, 34, 367, DateTimeKind.Utc).AddTicks(4617));

            migrationBuilder.UpdateData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2026, 3, 12, 3, 48, 34, 367, DateTimeKind.Utc).AddTicks(4619));

            migrationBuilder.CreateIndex(
                name: "IX_Trip_OriginCityId_DestinationCityId",
                table: "Trip",
                columns: new[] { "OriginCityId", "DestinationCityId" },
                unique: true,
                filter: "[Status] = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_Holiday_HolidayDate",
                table: "Holiday",
                column: "HolidayDate",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Driver_DocumentNumber",
                table: "Driver",
                column: "DocumentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customer_DocumentNumber",
                table: "Customer",
                column: "DocumentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customer_Email",
                table: "Customer",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_City_Code",
                table: "City",
                column: "Code",
                unique: true);
        }
    }
}
