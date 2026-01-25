using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace transport.infraestructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "City",
                columns: table => new
                {
                    CityId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: false, defaultValue: "System"),
                    UpdatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_City", x => x.CityId);
                });

            migrationBuilder.CreateTable(
                name: "Customer",
                columns: table => new
                {
                    CustomerId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Phone1 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Phone2 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CurrentBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    CreatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: false, defaultValue: "System"),
                    UpdatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customer", x => x.CustomerId);
                });

            migrationBuilder.CreateTable(
                name: "Driver",
                columns: table => new
                {
                    DriverId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: false, defaultValue: "System"),
                    UpdatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Driver", x => x.DriverId);
                });

            migrationBuilder.CreateTable(
                name: "Holiday",
                columns: table => new
                {
                    HolidayId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HolidayDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CreatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: false, defaultValue: "System"),
                    UpdatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Holiday", x => x.HolidayId);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessage",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Topic = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Processed = table.Column<bool>(type: "bit", nullable: false),
                    ProcessedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessage", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Role",
                columns: table => new
                {
                    RoleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    CreatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: false, defaultValue: "System"),
                    UpdatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Role", x => x.RoleId);
                });

            migrationBuilder.CreateTable(
                name: "VehicleType",
                columns: table => new
                {
                    VehicleTypeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    ImageBase64 = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: false, defaultValue: "System"),
                    UpdatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleType", x => x.VehicleTypeId);
                });

            migrationBuilder.CreateTable(
                name: "Direction",
                columns: table => new
                {
                    DirectionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "VARCHAR(250)", nullable: false),
                    Lat = table.Column<double>(type: "float", nullable: true),
                    Lng = table.Column<double>(type: "float", nullable: true),
                    CityId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: false, defaultValue: "System"),
                    UpdatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Direction", x => x.DirectionId);
                    table.ForeignKey(
                        name: "FK_Direction_City_CityId",
                        column: x => x.CityId,
                        principalTable: "City",
                        principalColumn: "CityId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Trip",
                columns: table => new
                {
                    TripId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OriginCityId = table.Column<int>(type: "int", nullable: false),
                    DestinationCityId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "VARCHAR(256)", maxLength: 100, nullable: false, defaultValue: "System"),
                    UpdatedBy = table.Column<string>(type: "VARCHAR(256)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trip", x => x.TripId);
                    table.ForeignKey(
                        name: "FK_Trip_City_DestinationCityId",
                        column: x => x.DestinationCityId,
                        principalTable: "City",
                        principalColumn: "CityId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Trip_City_OriginCityId",
                        column: x => x.OriginCityId,
                        principalTable: "City",
                        principalColumn: "CityId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "User",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: true),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Password = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_User", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_User_Customer_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customer",
                        principalColumn: "CustomerId");
                    table.ForeignKey(
                        name: "FK_User_Role_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Role",
                        principalColumn: "RoleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Vehicle",
                columns: table => new
                {
                    VehicleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VehicleTypeId = table.Column<int>(type: "int", nullable: false),
                    InternalNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AvailableQuantity = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: false, defaultValue: "System"),
                    UpdatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehicle", x => x.VehicleId);
                    table.ForeignKey(
                        name: "FK_Vehicle_VehicleType_VehicleTypeId",
                        column: x => x.VehicleTypeId,
                        principalTable: "VehicleType",
                        principalColumn: "VehicleTypeId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TripPrice",
                columns: table => new
                {
                    TripPriceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TripId = table.Column<int>(type: "int", nullable: false),
                    CityId = table.Column<int>(type: "int", nullable: false),
                    DirectionId = table.Column<int>(type: "int", nullable: true),
                    ReserveTypeId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "VARCHAR(256)", maxLength: 100, nullable: false, defaultValue: "System"),
                    UpdatedBy = table.Column<string>(type: "VARCHAR(256)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripPrice", x => x.TripPriceId);
                    table.ForeignKey(
                        name: "FK_TripPrice_City_CityId",
                        column: x => x.CityId,
                        principalTable: "City",
                        principalColumn: "CityId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TripPrice_Direction_DirectionId",
                        column: x => x.DirectionId,
                        principalTable: "Direction",
                        principalColumn: "DirectionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TripPrice_Trip_TripId",
                        column: x => x.TripId,
                        principalTable: "Trip",
                        principalColumn: "TripId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RefreshToken",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Token = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedByIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    ReplacedByToken = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: false, defaultValue: "System"),
                    UpdatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshToken", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshToken_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Service",
                columns: table => new
                {
                    ServiceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    TripId = table.Column<int>(type: "int", nullable: false),
                    OriginId = table.Column<int>(type: "int", nullable: false),
                    DestinationId = table.Column<int>(type: "int", nullable: false),
                    EstimatedDuration = table.Column<TimeSpan>(type: "time", nullable: false),
                    VehicleId = table.Column<int>(type: "int", nullable: false),
                    StartDay = table.Column<int>(type: "int", nullable: false),
                    EndDay = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: false, defaultValue: "System"),
                    UpdatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Service", x => x.ServiceId);
                    table.ForeignKey(
                        name: "FK_Service_City_DestinationId",
                        column: x => x.DestinationId,
                        principalTable: "City",
                        principalColumn: "CityId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Service_City_OriginId",
                        column: x => x.OriginId,
                        principalTable: "City",
                        principalColumn: "CityId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Service_Trip_TripId",
                        column: x => x.TripId,
                        principalTable: "Trip",
                        principalColumn: "TripId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Service_Vehicle_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicle",
                        principalColumn: "VehicleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceCustomer",
                columns: table => new
                {
                    ServiceCustomerId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceId = table.Column<int>(type: "int", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: false, defaultValue: "System"),
                    UpdatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
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
                });

            migrationBuilder.CreateTable(
                name: "ServiceSchedule",
                columns: table => new
                {
                    ServiceScheduleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceId = table.Column<int>(type: "int", nullable: false),
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

            migrationBuilder.CreateTable(
                name: "Reserve",
                columns: table => new
                {
                    ReserveId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReserveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VehicleId = table.Column<int>(type: "int", nullable: false),
                    DriverId = table.Column<int>(type: "int", nullable: true),
                    ServiceId = table.Column<int>(type: "int", nullable: true),
                    ServiceScheduleId = table.Column<int>(type: "int", nullable: true),
                    TripId = table.Column<int>(type: "int", nullable: false),
                    OriginId = table.Column<int>(type: "int", nullable: false),
                    DestinationId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "VARCHAR(20)", nullable: false),
                    ServiceName = table.Column<string>(type: "VARCHAR(250)", nullable: false),
                    OriginName = table.Column<string>(type: "VARCHAR(100)", nullable: false),
                    DestinationName = table.Column<string>(type: "VARCHAR(100)", nullable: false),
                    DepartureHour = table.Column<TimeSpan>(type: "time", nullable: false),
                    EstimatedDuration = table.Column<TimeSpan>(type: "time", nullable: false),
                    IsHoliday = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: false, defaultValue: "System"),
                    UpdatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reserve", x => x.ReserveId);
                    table.ForeignKey(
                        name: "FK_Reserve_City_DestinationId",
                        column: x => x.DestinationId,
                        principalTable: "City",
                        principalColumn: "CityId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reserve_City_OriginId",
                        column: x => x.OriginId,
                        principalTable: "City",
                        principalColumn: "CityId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reserve_Driver_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Driver",
                        principalColumn: "DriverId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Reserve_ServiceSchedule_ServiceScheduleId",
                        column: x => x.ServiceScheduleId,
                        principalTable: "ServiceSchedule",
                        principalColumn: "ServiceScheduleId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reserve_Service_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Service",
                        principalColumn: "ServiceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reserve_Trip_TripId",
                        column: x => x.TripId,
                        principalTable: "Trip",
                        principalColumn: "TripId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reserve_Vehicle_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicle",
                        principalColumn: "VehicleId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CashBox",
                columns: table => new
                {
                    CashBoxId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Description = table.Column<string>(type: "NVARCHAR(200)", nullable: true),
                    OpenedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "VARCHAR(20)", nullable: false),
                    OpenedByUserId = table.Column<int>(type: "int", nullable: false),
                    ClosedByUserId = table.Column<int>(type: "int", nullable: true),
                    ReserveId = table.Column<int>(type: "int", nullable: true),
                    CreatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: false, defaultValue: "System"),
                    UpdatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashBox", x => x.CashBoxId);
                    table.ForeignKey(
                        name: "FK_CashBox_Reserve_ReserveId",
                        column: x => x.ReserveId,
                        principalTable: "Reserve",
                        principalColumn: "ReserveId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CashBox_User_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashBox_User_OpenedByUserId",
                        column: x => x.OpenedByUserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Passenger",
                columns: table => new
                {
                    PassengerId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReserveId = table.Column<int>(type: "int", nullable: false),
                    ReserveRelatedId = table.Column<int>(type: "int", nullable: true),
                    FirstName = table.Column<string>(type: "VARCHAR(100)", nullable: false),
                    LastName = table.Column<string>(type: "VARCHAR(100)", nullable: false),
                    DocumentNumber = table.Column<string>(type: "VARCHAR(50)", nullable: false),
                    Email = table.Column<string>(type: "VARCHAR(150)", nullable: true),
                    Phone = table.Column<string>(type: "VARCHAR(30)", nullable: true),
                    PickupLocationId = table.Column<int>(type: "int", nullable: true),
                    DropoffLocationId = table.Column<int>(type: "int", nullable: true),
                    PickupAddress = table.Column<string>(type: "VARCHAR(250)", nullable: true),
                    DropoffAddress = table.Column<string>(type: "VARCHAR(250)", nullable: true),
                    HasTraveled = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "VARCHAR(20)", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: true),
                    CreatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: false, defaultValue: "System"),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DirectionId = table.Column<int>(type: "int", nullable: true),
                    DirectionId1 = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Passenger", x => x.PassengerId);
                    table.ForeignKey(
                        name: "FK_Passenger_Customer_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customer",
                        principalColumn: "CustomerId");
                    table.ForeignKey(
                        name: "FK_Passenger_Direction_DirectionId",
                        column: x => x.DirectionId,
                        principalTable: "Direction",
                        principalColumn: "DirectionId");
                    table.ForeignKey(
                        name: "FK_Passenger_Direction_DirectionId1",
                        column: x => x.DirectionId1,
                        principalTable: "Direction",
                        principalColumn: "DirectionId");
                    table.ForeignKey(
                        name: "FK_Passenger_Direction_DropoffLocationId",
                        column: x => x.DropoffLocationId,
                        principalTable: "Direction",
                        principalColumn: "DirectionId");
                    table.ForeignKey(
                        name: "FK_Passenger_Direction_PickupLocationId",
                        column: x => x.PickupLocationId,
                        principalTable: "Direction",
                        principalColumn: "DirectionId");
                    table.ForeignKey(
                        name: "FK_Passenger_Reserve_ReserveId",
                        column: x => x.ReserveId,
                        principalTable: "Reserve",
                        principalColumn: "ReserveId");
                    table.ForeignKey(
                        name: "FK_Passenger_Reserve_ReserveRelatedId",
                        column: x => x.ReserveRelatedId,
                        principalTable: "Reserve",
                        principalColumn: "ReserveId");
                });

            migrationBuilder.CreateTable(
                name: "ReserveSlotLock",
                columns: table => new
                {
                    ReserveSlotLockId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LockToken = table.Column<string>(type: "VARCHAR(50)", nullable: false),
                    OutboundReserveId = table.Column<int>(type: "int", nullable: false),
                    ReturnReserveId = table.Column<int>(type: "int", nullable: true),
                    SlotsLocked = table.Column<int>(type: "int", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "VARCHAR(20)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UserEmail = table.Column<string>(type: "VARCHAR(100)", nullable: true),
                    UserDocumentNumber = table.Column<string>(type: "VARCHAR(20)", nullable: true),
                    CustomerId = table.Column<int>(type: "int", nullable: true),
                    CreatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: false, defaultValue: "System"),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReserveSlotLock", x => x.ReserveSlotLockId);
                    table.ForeignKey(
                        name: "FK_ReserveSlotLock_Customer_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customer",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ReserveSlotLock_Reserve_OutboundReserveId",
                        column: x => x.OutboundReserveId,
                        principalTable: "Reserve",
                        principalColumn: "ReserveId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReserveSlotLock_Reserve_ReturnReserveId",
                        column: x => x.ReturnReserveId,
                        principalTable: "Reserve",
                        principalColumn: "ReserveId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReservePayment",
                columns: table => new
                {
                    ReservePaymentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReserveId = table.Column<int>(type: "int", nullable: false),
                    Method = table.Column<string>(type: "VARCHAR(20)", nullable: false),
                    Status = table.Column<string>(type: "VARCHAR(20)", nullable: false),
                    StatusDetail = table.Column<string>(type: "VARCHAR(MAX)", nullable: true),
                    ResultApiExternalRawJson = table.Column<string>(type: "NVARCHAR(MAX)", nullable: true),
                    CustomerId = table.Column<int>(type: "int", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentExternalId = table.Column<long>(type: "BIGINT", nullable: true),
                    PayerName = table.Column<string>(type: "VARCHAR(150)", nullable: true),
                    PayerDocumentNumber = table.Column<string>(type: "VARCHAR(50)", nullable: true),
                    PayerEmail = table.Column<string>(type: "VARCHAR(150)", nullable: true),
                    ParentReservePaymentId = table.Column<int>(type: "int", nullable: true),
                    CashBoxId = table.Column<int>(type: "int", nullable: true),
                    CreatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: false, defaultValue: "System"),
                    UpdatedBy = table.Column<string>(type: "VARCHAR(256)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservePayment", x => x.ReservePaymentId);
                    table.ForeignKey(
                        name: "FK_ReservePayment_CashBox_CashBoxId",
                        column: x => x.CashBoxId,
                        principalTable: "CashBox",
                        principalColumn: "CashBoxId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservePayment_Customer_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customer",
                        principalColumn: "CustomerId");
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
                        onDelete: ReferentialAction.Restrict);
                });

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

            migrationBuilder.InsertData(
                table: "Role",
                columns: new[] { "RoleId", "CreatedBy", "CreatedDate", "Name", "UpdatedBy", "UpdatedDate" },
                values: new object[,]
                {
                    { 1, "System", new DateTime(2026, 1, 19, 22, 28, 2, 182, DateTimeKind.Utc).AddTicks(9444), "Administrador", null, null },
                    { 2, "System", new DateTime(2026, 1, 19, 22, 28, 2, 182, DateTimeKind.Utc).AddTicks(9448), "Cliente", null, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashBox_ClosedByUserId",
                table: "CashBox",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CashBox_OpenedAt",
                table: "CashBox",
                column: "OpenedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CashBox_OpenedByUserId",
                table: "CashBox",
                column: "OpenedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CashBox_ReserveId",
                table: "CashBox",
                column: "ReserveId");

            migrationBuilder.CreateIndex(
                name: "IX_CashBox_Status",
                table: "CashBox",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CashBox_Status_OpenedAt",
                table: "CashBox",
                columns: new[] { "Status", "OpenedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_City_Code",
                table: "City",
                column: "Code",
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

            migrationBuilder.CreateIndex(
                name: "IX_Direction_CityId",
                table: "Direction",
                column: "CityId");

            migrationBuilder.CreateIndex(
                name: "IX_Driver_DocumentNumber",
                table: "Driver",
                column: "DocumentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Holiday_HolidayDate",
                table: "Holiday",
                column: "HolidayDate",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Passenger_CustomerId",
                table: "Passenger",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Passenger_DirectionId",
                table: "Passenger",
                column: "DirectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Passenger_DirectionId1",
                table: "Passenger",
                column: "DirectionId1");

            migrationBuilder.CreateIndex(
                name: "IX_Passenger_DropoffLocationId",
                table: "Passenger",
                column: "DropoffLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Passenger_PickupLocationId",
                table: "Passenger",
                column: "PickupLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Passenger_ReserveId",
                table: "Passenger",
                column: "ReserveId");

            migrationBuilder.CreateIndex(
                name: "IX_Passenger_ReserveId_DocumentNumber",
                table: "Passenger",
                columns: new[] { "ReserveId", "DocumentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Passenger_ReserveRelatedId",
                table: "Passenger",
                column: "ReserveRelatedId");

            migrationBuilder.CreateIndex(
                name: "IX_Passenger_Status",
                table: "Passenger",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshToken_UserId",
                table: "RefreshToken",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Reserve_DestinationId",
                table: "Reserve",
                column: "DestinationId");

            migrationBuilder.CreateIndex(
                name: "IX_Reserve_DriverId",
                table: "Reserve",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_Reserve_OriginId_DestinationId_ReserveDate",
                table: "Reserve",
                columns: new[] { "OriginId", "DestinationId", "ReserveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Reserve_ServiceId_ReserveDate",
                table: "Reserve",
                columns: new[] { "ServiceId", "ReserveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Reserve_ServiceScheduleId",
                table: "Reserve",
                column: "ServiceScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_Reserve_Status_ReserveDate",
                table: "Reserve",
                columns: new[] { "Status", "ReserveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Reserve_TripId_ReserveDate",
                table: "Reserve",
                columns: new[] { "TripId", "ReserveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Reserve_VehicleId",
                table: "Reserve",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservePayment_CashBoxId",
                table: "ReservePayment",
                column: "CashBoxId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservePayment_CustomerId",
                table: "ReservePayment",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservePayment_ParentReservePaymentId",
                table: "ReservePayment",
                column: "ParentReservePaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservePayment_PaymentExternalId",
                table: "ReservePayment",
                column: "PaymentExternalId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservePayment_ReserveId",
                table: "ReservePayment",
                column: "ReserveId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservePayment_Status",
                table: "ReservePayment",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ReserveSlotLock_CreatedDate",
                table: "ReserveSlotLock",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_ReserveSlotLock_CustomerId",
                table: "ReserveSlotLock",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ReserveSlotLock_LockToken",
                table: "ReserveSlotLock",
                column: "LockToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReserveSlotLock_OutboundReserveId",
                table: "ReserveSlotLock",
                column: "OutboundReserveId");

            migrationBuilder.CreateIndex(
                name: "IX_ReserveSlotLock_ReturnReserveId",
                table: "ReserveSlotLock",
                column: "ReturnReserveId");

            migrationBuilder.CreateIndex(
                name: "IX_ReserveSlotLock_Status_ExpiresAt",
                table: "ReserveSlotLock",
                columns: new[] { "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Service_DestinationId",
                table: "Service",
                column: "DestinationId");

            migrationBuilder.CreateIndex(
                name: "IX_Service_OriginId",
                table: "Service",
                column: "OriginId");

            migrationBuilder.CreateIndex(
                name: "IX_Service_TripId",
                table: "Service",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_Service_VehicleId",
                table: "Service",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceCustomer_CustomerId",
                table: "ServiceCustomer",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceCustomer_ServiceId",
                table: "ServiceCustomer",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceSchedule_ServiceId",
                table: "ServiceSchedule",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Trip_DestinationCityId",
                table: "Trip",
                column: "DestinationCityId");

            migrationBuilder.CreateIndex(
                name: "IX_Trip_OriginCityId_DestinationCityId",
                table: "Trip",
                columns: new[] { "OriginCityId", "DestinationCityId" },
                unique: true,
                filter: "[Status] = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_TripPrice_CityId",
                table: "TripPrice",
                column: "CityId");

            migrationBuilder.CreateIndex(
                name: "IX_TripPrice_DirectionId",
                table: "TripPrice",
                column: "DirectionId");

            migrationBuilder.CreateIndex(
                name: "IX_TripPrice_TripId_CityId_DirectionId_ReserveTypeId",
                table: "TripPrice",
                columns: new[] { "TripId", "CityId", "DirectionId", "ReserveTypeId" },
                unique: true,
                filter: "[Status] = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_User_CustomerId",
                table: "User",
                column: "CustomerId",
                unique: true,
                filter: "[CustomerId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_User_RoleId",
                table: "User",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicle_VehicleTypeId",
                table: "Vehicle",
                column: "VehicleTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleType_Name",
                table: "VehicleType",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerAccountTransactions");

            migrationBuilder.DropTable(
                name: "Holiday");

            migrationBuilder.DropTable(
                name: "OutboxMessage");

            migrationBuilder.DropTable(
                name: "Passenger");

            migrationBuilder.DropTable(
                name: "RefreshToken");

            migrationBuilder.DropTable(
                name: "ReserveSlotLock");

            migrationBuilder.DropTable(
                name: "ServiceCustomer");

            migrationBuilder.DropTable(
                name: "TripPrice");

            migrationBuilder.DropTable(
                name: "ReservePayment");

            migrationBuilder.DropTable(
                name: "Direction");

            migrationBuilder.DropTable(
                name: "CashBox");

            migrationBuilder.DropTable(
                name: "Reserve");

            migrationBuilder.DropTable(
                name: "User");

            migrationBuilder.DropTable(
                name: "Driver");

            migrationBuilder.DropTable(
                name: "ServiceSchedule");

            migrationBuilder.DropTable(
                name: "Customer");

            migrationBuilder.DropTable(
                name: "Role");

            migrationBuilder.DropTable(
                name: "Service");

            migrationBuilder.DropTable(
                name: "Trip");

            migrationBuilder.DropTable(
                name: "Vehicle");

            migrationBuilder.DropTable(
                name: "City");

            migrationBuilder.DropTable(
                name: "VehicleType");
        }
    }
}
