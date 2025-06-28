IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [City] (
    [CityId] int NOT NULL IDENTITY,
    [Code] nvarchar(50) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Status] int NOT NULL,
    CONSTRAINT [PK_City] PRIMARY KEY ([CityId])
);
GO

CREATE TABLE [Customer] (
    [CustomerId] int NOT NULL IDENTITY,
    [FirstName] nvarchar(100) NOT NULL,
    [LastName] nvarchar(100) NOT NULL,
    [Email] nvarchar(150) NOT NULL,
    [DocumentNumber] nvarchar(50) NOT NULL,
    [Phone1] nvarchar(20) NOT NULL,
    [Phone2] nvarchar(20) NULL,
    [Status] int NOT NULL,
    CONSTRAINT [PK_Customer] PRIMARY KEY ([CustomerId])
);
GO

CREATE TABLE [Driver] (
    [DriverId] int NOT NULL IDENTITY,
    [FirstName] nvarchar(100) NOT NULL,
    [LastName] nvarchar(100) NOT NULL,
    [DocumentNumber] nvarchar(50) NOT NULL,
    [Status] int NOT NULL,
    CONSTRAINT [PK_Driver] PRIMARY KEY ([DriverId])
);
GO

CREATE TABLE [Holiday] (
    [HolidayId] int NOT NULL IDENTITY,
    [HolidayDate] datetime2 NOT NULL,
    [Description] nvarchar(255) NOT NULL,
    CONSTRAINT [PK_Holiday] PRIMARY KEY ([HolidayId])
);
GO

CREATE TABLE [OutboxMessage] (
    [Id] uniqueidentifier NOT NULL,
    [OccurredOn] datetime2 NOT NULL,
    [Type] nvarchar(100) NOT NULL,
    [Content] nvarchar(max) NOT NULL,
    [Topic] nvarchar(50) NULL,
    [Processed] bit NOT NULL,
    [ProcessedOn] datetime2 NULL,
    CONSTRAINT [PK_OutboxMessage] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [Role] (
    [RoleId] int NOT NULL IDENTITY,
    [Name] nvarchar(250) NOT NULL,
    CONSTRAINT [PK_Role] PRIMARY KEY ([RoleId])
);
GO

CREATE TABLE [VehicleType] (
    [VehicleTypeId] int NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    [Quantity] int NOT NULL,
    [ImageBase64] text NULL,
    [Status] int NOT NULL,
    CONSTRAINT [PK_VehicleType] PRIMARY KEY ([VehicleTypeId])
);
GO

CREATE TABLE [Direction] (
    [DirectionId] int NOT NULL IDENTITY,
    [Name] nvarchar(250) NOT NULL,
    [CityId] int NOT NULL,
    CONSTRAINT [PK_Direction] PRIMARY KEY ([DirectionId]),
    CONSTRAINT [FK_Direction_City_CityId] FOREIGN KEY ([CityId]) REFERENCES [City] ([CityId]) ON DELETE CASCADE
);
GO

CREATE TABLE [User] (
    [UserId] int NOT NULL IDENTITY,
    [CustomerId] int NULL,
    [RoleId] int NOT NULL,
    [Email] nvarchar(max) NOT NULL,
    [Password] nvarchar(max) NOT NULL,
    [Status] int NOT NULL,
    CONSTRAINT [PK_User] PRIMARY KEY ([UserId]),
    CONSTRAINT [FK_User_Customer_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customer] ([CustomerId]),
    CONSTRAINT [FK_User_Role_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Role] ([RoleId]) ON DELETE CASCADE
);
GO

CREATE TABLE [Vehicle] (
    [VehicleId] int NOT NULL IDENTITY,
    [VehicleTypeId] int NOT NULL,
    [InternalNumber] nvarchar(50) NOT NULL,
    [Status] int NOT NULL,
    [AvailableQuantity] int NOT NULL,
    CONSTRAINT [PK_Vehicle] PRIMARY KEY ([VehicleId]),
    CONSTRAINT [FK_Vehicle_VehicleType_VehicleTypeId] FOREIGN KEY ([VehicleTypeId]) REFERENCES [VehicleType] ([VehicleTypeId]) ON DELETE CASCADE
);
GO

CREATE TABLE [Service] (
    [ServiceId] int NOT NULL IDENTITY,
    [Name] nvarchar(250) NOT NULL,
    [StartDay] int NOT NULL,
    [EndDay] int NOT NULL,
    [OriginId] int NOT NULL,
    [DestinationId] int NOT NULL,
    [EstimatedDuration] time NOT NULL,
    [DepartureHour] time NOT NULL,
    [IsHoliday] bit NOT NULL,
    [VehicleId] int NOT NULL,
    [Status] int NOT NULL,
    CONSTRAINT [PK_Service] PRIMARY KEY ([ServiceId]),
    CONSTRAINT [FK_Service_City_DestinationId] FOREIGN KEY ([DestinationId]) REFERENCES [City] ([CityId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Service_City_OriginId] FOREIGN KEY ([OriginId]) REFERENCES [City] ([CityId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Service_Vehicle_VehicleId] FOREIGN KEY ([VehicleId]) REFERENCES [Vehicle] ([VehicleId]) ON DELETE CASCADE
);
GO

CREATE TABLE [Reserve] (
    [ReserveId] int NOT NULL IDENTITY,
    [ReserveDate] datetime2 NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [VehicleId] int NOT NULL,
    [DriverId] int NULL,
    [ServiceId] int NOT NULL,
    CONSTRAINT [PK_Reserve] PRIMARY KEY ([ReserveId]),
    CONSTRAINT [FK_Reserve_Driver_DriverId] FOREIGN KEY ([DriverId]) REFERENCES [Driver] ([DriverId]),
    CONSTRAINT [FK_Reserve_Service_ServiceId] FOREIGN KEY ([ServiceId]) REFERENCES [Service] ([ServiceId]) ON DELETE CASCADE,
    CONSTRAINT [FK_Reserve_Vehicle_VehicleId] FOREIGN KEY ([VehicleId]) REFERENCES [Vehicle] ([VehicleId]) ON DELETE NO ACTION
);
GO

CREATE TABLE [ReservePrice] (
    [ReservePriceId] int NOT NULL IDENTITY,
    [ServiceId] int NOT NULL,
    [Price] decimal(10,2) NOT NULL,
    [ReserveTypeId] int NOT NULL,
    CONSTRAINT [PK_ReservePrice] PRIMARY KEY ([ReservePriceId]),
    CONSTRAINT [FK_ReservePrice_Service_ServiceId] FOREIGN KEY ([ServiceId]) REFERENCES [Service] ([ServiceId]) ON DELETE CASCADE
);
GO

CREATE TABLE [ServiceCustomer] (
    [ServiceCustomerId] int NOT NULL IDENTITY,
    [ServiceId] int NOT NULL,
    [CustomerId] int NOT NULL,
    CONSTRAINT [PK_ServiceCustomer] PRIMARY KEY ([ServiceCustomerId]),
    CONSTRAINT [FK_ServiceCustomer_Customer_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customer] ([CustomerId]) ON DELETE CASCADE,
    CONSTRAINT [FK_ServiceCustomer_Service_ServiceId] FOREIGN KEY ([ServiceId]) REFERENCES [Service] ([ServiceId]) ON DELETE CASCADE
);
GO

CREATE TABLE [CustomerReserve] (
    [CustomerReserveId] int NOT NULL IDENTITY,
    [CustomerId] int NOT NULL,
    [ReserveId] int NOT NULL,
    [IsPayment] bit NOT NULL,
    [StatusPayment] int NOT NULL,
    [Price] decimal(10,2) NOT NULL,
    [PickupLocationId] int NOT NULL,
    [DropoffLocationId] int NOT NULL,
    [HasTraveled] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_CustomerReserve] PRIMARY KEY ([CustomerReserveId]),
    CONSTRAINT [FK_CustomerReserve_Customer_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customer] ([CustomerId]) ON DELETE CASCADE,
    CONSTRAINT [FK_CustomerReserve_Direction_DropoffLocationId] FOREIGN KEY ([DropoffLocationId]) REFERENCES [Direction] ([DirectionId]),
    CONSTRAINT [FK_CustomerReserve_Direction_PickupLocationId] FOREIGN KEY ([PickupLocationId]) REFERENCES [Direction] ([DirectionId]),
    CONSTRAINT [FK_CustomerReserve_Reserve_ReserveId] FOREIGN KEY ([ReserveId]) REFERENCES [Reserve] ([ReserveId]) ON DELETE CASCADE
);
GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'RoleId', N'Name') AND [object_id] = OBJECT_ID(N'[Role]'))
    SET IDENTITY_INSERT [Role] ON;
INSERT INTO [Role] ([RoleId], [Name])
VALUES (1, N'Administrador'),
(2, N'Cliente');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'RoleId', N'Name') AND [object_id] = OBJECT_ID(N'[Role]'))
    SET IDENTITY_INSERT [Role] OFF;
GO

CREATE UNIQUE INDEX [IX_City_Code] ON [City] ([Code]);
GO

CREATE UNIQUE INDEX [IX_Customer_DocumentNumber] ON [Customer] ([DocumentNumber]);
GO

CREATE UNIQUE INDEX [IX_Customer_Email] ON [Customer] ([Email]);
GO

CREATE INDEX [IX_CustomerReserve_CustomerId] ON [CustomerReserve] ([CustomerId]);
GO

CREATE INDEX [IX_CustomerReserve_DropoffLocationId] ON [CustomerReserve] ([DropoffLocationId]);
GO

CREATE INDEX [IX_CustomerReserve_PickupLocationId] ON [CustomerReserve] ([PickupLocationId]);
GO

CREATE INDEX [IX_CustomerReserve_ReserveId] ON [CustomerReserve] ([ReserveId]);
GO

CREATE INDEX [IX_Direction_CityId] ON [Direction] ([CityId]);
GO

CREATE UNIQUE INDEX [IX_Driver_DocumentNumber] ON [Driver] ([DocumentNumber]);
GO

CREATE UNIQUE INDEX [IX_Holiday_HolidayDate] ON [Holiday] ([HolidayDate]);
GO

CREATE INDEX [IX_Reserve_DriverId] ON [Reserve] ([DriverId]);
GO

CREATE INDEX [IX_Reserve_ServiceId] ON [Reserve] ([ServiceId]);
GO

CREATE INDEX [IX_Reserve_VehicleId] ON [Reserve] ([VehicleId]);
GO

CREATE INDEX [IX_ReservePrice_ServiceId] ON [ReservePrice] ([ServiceId]);
GO

CREATE INDEX [IX_Service_DestinationId] ON [Service] ([DestinationId]);
GO

CREATE INDEX [IX_Service_OriginId] ON [Service] ([OriginId]);
GO

CREATE INDEX [IX_Service_VehicleId] ON [Service] ([VehicleId]);
GO

CREATE INDEX [IX_ServiceCustomer_CustomerId] ON [ServiceCustomer] ([CustomerId]);
GO

CREATE INDEX [IX_ServiceCustomer_ServiceId] ON [ServiceCustomer] ([ServiceId]);
GO

CREATE UNIQUE INDEX [IX_User_CustomerId] ON [User] ([CustomerId]) WHERE [CustomerId] IS NOT NULL;
GO

CREATE INDEX [IX_User_RoleId] ON [User] ([RoleId]);
GO

CREATE INDEX [IX_Vehicle_VehicleTypeId] ON [Vehicle] ([VehicleTypeId]);
GO

CREATE UNIQUE INDEX [IX_VehicleType_Name] ON [VehicleType] ([Name]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250502194209_InitialMigration', N'8.0.14');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [Direction] ADD [Lat] float NULL;
GO

ALTER TABLE [Direction] ADD [Lng] float NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250504002738_InitialMigration-pt2', N'8.0.14');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [ReservePrice] ADD [Status] int NOT NULL DEFAULT 1;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250507224207_InitialMigration-pt3', N'8.0.14');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [RefreshToken] (
    [Id] int NOT NULL IDENTITY,
    [Token] nvarchar(100) NOT NULL,
    [UserId] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedByIp] nvarchar(45) NOT NULL,
    [ExpiresAt] datetime2 NOT NULL,
    [RevokedAt] datetime2 NULL,
    [RevokedByIp] nvarchar(45) NULL,
    [ReplacedByToken] nvarchar(100) NULL,
    CONSTRAINT [PK_RefreshToken] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RefreshToken_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [User] ([UserId]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_RefreshToken_UserId] ON [RefreshToken] ([UserId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250510200933_InitialMigration-pt4', N'8.0.14');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DROP INDEX [IX_ReservePrice_ServiceId] ON [ReservePrice];
GO

ALTER TABLE [VehicleType] ADD [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System';
GO

ALTER TABLE [VehicleType] ADD [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE());
GO

ALTER TABLE [VehicleType] ADD [UpdatedBy] VARCHAR(256) NULL;
GO

ALTER TABLE [VehicleType] ADD [UpdatedDate] datetime2 NULL;
GO

ALTER TABLE [Vehicle] ADD [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System';
GO

ALTER TABLE [Vehicle] ADD [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE());
GO

ALTER TABLE [Vehicle] ADD [UpdatedBy] VARCHAR(256) NULL;
GO

ALTER TABLE [Vehicle] ADD [UpdatedDate] datetime2 NULL;
GO

ALTER TABLE [ServiceCustomer] ADD [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System';
GO

ALTER TABLE [ServiceCustomer] ADD [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE());
GO

ALTER TABLE [ServiceCustomer] ADD [UpdatedBy] VARCHAR(256) NULL;
GO

ALTER TABLE [ServiceCustomer] ADD [UpdatedDate] datetime2 NULL;
GO

ALTER TABLE [Service] ADD [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System';
GO

ALTER TABLE [Service] ADD [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE());
GO

ALTER TABLE [Service] ADD [UpdatedBy] VARCHAR(256) NULL;
GO

ALTER TABLE [Service] ADD [UpdatedDate] datetime2 NULL;
GO

ALTER TABLE [Role] ADD [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System';
GO

ALTER TABLE [Role] ADD [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE());
GO

ALTER TABLE [Role] ADD [UpdatedBy] VARCHAR(256) NULL;
GO

ALTER TABLE [Role] ADD [UpdatedDate] datetime2 NULL;
GO

ALTER TABLE [ReservePrice] ADD [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System';
GO

ALTER TABLE [ReservePrice] ADD [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE());
GO

ALTER TABLE [ReservePrice] ADD [UpdatedBy] VARCHAR(256) NULL;
GO

ALTER TABLE [ReservePrice] ADD [UpdatedDate] datetime2 NULL;
GO

ALTER TABLE [Reserve] ADD [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System';
GO

ALTER TABLE [Reserve] ADD [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE());
GO

ALTER TABLE [Reserve] ADD [UpdatedBy] VARCHAR(256) NULL;
GO

ALTER TABLE [Reserve] ADD [UpdatedDate] datetime2 NULL;
GO

ALTER TABLE [RefreshToken] ADD [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System';
GO

ALTER TABLE [RefreshToken] ADD [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE());
GO

ALTER TABLE [RefreshToken] ADD [UpdatedBy] VARCHAR(256) NULL;
GO

ALTER TABLE [RefreshToken] ADD [UpdatedDate] datetime2 NULL;
GO

ALTER TABLE [Holiday] ADD [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System';
GO

ALTER TABLE [Holiday] ADD [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE());
GO

ALTER TABLE [Holiday] ADD [UpdatedBy] VARCHAR(256) NULL;
GO

ALTER TABLE [Holiday] ADD [UpdatedDate] datetime2 NULL;
GO

ALTER TABLE [Driver] ADD [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System';
GO

ALTER TABLE [Driver] ADD [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE());
GO

ALTER TABLE [Driver] ADD [UpdatedBy] VARCHAR(256) NULL;
GO

ALTER TABLE [Driver] ADD [UpdatedDate] datetime2 NULL;
GO

ALTER TABLE [Direction] ADD [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System';
GO

ALTER TABLE [Direction] ADD [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE());
GO

ALTER TABLE [Direction] ADD [UpdatedBy] VARCHAR(256) NULL;
GO

ALTER TABLE [Direction] ADD [UpdatedDate] datetime2 NULL;
GO

DECLARE @var0 sysname;
SELECT @var0 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CustomerReserve]') AND [c].[name] = N'PickupLocationId');
IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [CustomerReserve] DROP CONSTRAINT [' + @var0 + '];');
ALTER TABLE [CustomerReserve] ALTER COLUMN [PickupLocationId] int NULL;
GO

DECLARE @var1 sysname;
SELECT @var1 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CustomerReserve]') AND [c].[name] = N'DropoffLocationId');
IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [CustomerReserve] DROP CONSTRAINT [' + @var1 + '];');
ALTER TABLE [CustomerReserve] ALTER COLUMN [DropoffLocationId] int NULL;
GO

ALTER TABLE [CustomerReserve] ADD [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System';
GO

ALTER TABLE [CustomerReserve] ADD [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE());
GO

ALTER TABLE [CustomerReserve] ADD [UpdatedBy] VARCHAR(256) NULL;
GO

ALTER TABLE [CustomerReserve] ADD [UpdatedDate] datetime2 NULL;
GO

ALTER TABLE [CustomerReserve] ADD [UserId] int NULL;
GO

ALTER TABLE [Customer] ADD [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System';
GO

ALTER TABLE [Customer] ADD [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE());
GO

ALTER TABLE [Customer] ADD [UpdatedBy] VARCHAR(256) NULL;
GO

ALTER TABLE [Customer] ADD [UpdatedDate] datetime2 NULL;
GO

ALTER TABLE [City] ADD [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System';
GO

ALTER TABLE [City] ADD [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE());
GO

ALTER TABLE [City] ADD [UpdatedBy] VARCHAR(256) NULL;
GO

ALTER TABLE [City] ADD [UpdatedDate] datetime2 NULL;
GO

UPDATE [Role] SET [CreatedBy] = 'System', [CreatedDate] = '2025-05-18T22:41:03.2441076Z', [UpdatedBy] = NULL, [UpdatedDate] = NULL
WHERE [RoleId] = 1;
SELECT @@ROWCOUNT;

GO

UPDATE [Role] SET [CreatedBy] = 'System', [CreatedDate] = '2025-05-18T22:41:03.2441079Z', [UpdatedBy] = NULL, [UpdatedDate] = NULL
WHERE [RoleId] = 2;
SELECT @@ROWCOUNT;

GO

CREATE UNIQUE INDEX [IX_ReservePrice_ServiceId] ON [ReservePrice] ([ServiceId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250518224103_AuditableFields', N'8.0.14');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DROP INDEX [IX_Reserve_ServiceId] ON [Reserve];
GO

DECLARE @var2 sysname;
SELECT @var2 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Reserve]') AND [c].[name] = N'Status');
IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [Reserve] DROP CONSTRAINT [' + @var2 + '];');
ALTER TABLE [Reserve] ALTER COLUMN [Status] VARCHAR(20) NOT NULL;
GO

ALTER TABLE [CustomerReserve] ADD [PaymentMethod] VARCHAR(20) NULL;
GO

ALTER TABLE [CustomerReserve] ADD [Status] int NOT NULL DEFAULT 0;
GO

UPDATE [Role] SET [CreatedDate] = '2025-05-24T18:44:56.5585699Z'
WHERE [RoleId] = 1;
SELECT @@ROWCOUNT;

GO

UPDATE [Role] SET [CreatedDate] = '2025-05-24T18:44:56.5585702Z'
WHERE [RoleId] = 2;
SELECT @@ROWCOUNT;

GO

CREATE INDEX [IX_Reserve_ServiceId_ReserveDate] ON [Reserve] ([ServiceId], [ReserveDate]);
GO

CREATE INDEX [IX_Reserve_Status_ReserveDate] ON [Reserve] ([Status], [ReserveDate]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250524184457_AddReserveReportsAndPassengers', N'8.0.14');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DROP INDEX [IX_ReservePrice_ServiceId] ON [ReservePrice];
GO

UPDATE [Role] SET [CreatedDate] = '2025-05-24T19:17:50.5670852Z'
WHERE [RoleId] = 1;
SELECT @@ROWCOUNT;

GO

UPDATE [Role] SET [CreatedDate] = '2025-05-24T19:17:50.5670855Z'
WHERE [RoleId] = 2;
SELECT @@ROWCOUNT;

GO

CREATE INDEX [IX_ReservePrice_ServiceId_ReserveTypeId] ON [ReservePrice] ([ServiceId], [ReserveTypeId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250524191751_AddIndexInServiceIdAndReserveTypeId', N'8.0.14');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DECLARE @var3 sysname;
SELECT @var3 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Direction]') AND [c].[name] = N'Name');
IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [Direction] DROP CONSTRAINT [' + @var3 + '];');
ALTER TABLE [Direction] ALTER COLUMN [Name] VARCHAR(250) NOT NULL;
GO

ALTER TABLE [Direction] ADD [Status] int NOT NULL DEFAULT 0;
GO

UPDATE [Role] SET [CreatedDate] = '2025-05-26T11:33:42.5415717Z'
WHERE [RoleId] = 1;
SELECT @@ROWCOUNT;

GO

UPDATE [Role] SET [CreatedDate] = '2025-05-26T11:33:42.5415719Z'
WHERE [RoleId] = 2;
SELECT @@ROWCOUNT;

GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250526113343_AddStatusInDirection', N'8.0.14');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DECLARE @var4 sysname;
SELECT @var4 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Service]') AND [c].[name] = N'DepartureHour');
IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [Service] DROP CONSTRAINT [' + @var4 + '];');
ALTER TABLE [Service] DROP COLUMN [DepartureHour];
GO

DECLARE @var5 sysname;
SELECT @var5 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Service]') AND [c].[name] = N'EndDay');
IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [Service] DROP CONSTRAINT [' + @var5 + '];');
ALTER TABLE [Service] DROP COLUMN [EndDay];
GO

DECLARE @var6 sysname;
SELECT @var6 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Service]') AND [c].[name] = N'IsHoliday');
IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [Service] DROP CONSTRAINT [' + @var6 + '];');
ALTER TABLE [Service] DROP COLUMN [IsHoliday];
GO

DECLARE @var7 sysname;
SELECT @var7 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Service]') AND [c].[name] = N'StartDay');
IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [Service] DROP CONSTRAINT [' + @var7 + '];');
ALTER TABLE [Service] DROP COLUMN [StartDay];
GO

ALTER TABLE [Reserve] ADD [ServiceScheduleId] int NOT NULL DEFAULT 0;
GO

CREATE TABLE [ServiceSchedule] (
    [ServiceScheduleId] int NOT NULL IDENTITY,
    [ServiceId] int NOT NULL,
    [StartDay] int NOT NULL,
    [EndDay] int NOT NULL,
    [DepartureHour] time NOT NULL,
    [IsHoliday] bit NOT NULL,
    [Status] int NOT NULL,
    [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System',
    [UpdatedBy] VARCHAR(256) NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE()),
    [UpdatedDate] datetime2 NULL,
    CONSTRAINT [PK_ServiceSchedule] PRIMARY KEY ([ServiceScheduleId]),
    CONSTRAINT [FK_ServiceSchedule_Service_ServiceId] FOREIGN KEY ([ServiceId]) REFERENCES [Service] ([ServiceId]) ON DELETE NO ACTION
);
GO

UPDATE [Role] SET [CreatedDate] = '2025-05-30T01:26:03.4101367Z'
WHERE [RoleId] = 1;
SELECT @@ROWCOUNT;

GO

UPDATE [Role] SET [CreatedDate] = '2025-05-30T01:26:03.4101370Z'
WHERE [RoleId] = 2;
SELECT @@ROWCOUNT;

GO

CREATE INDEX [IX_Reserve_ServiceScheduleId] ON [Reserve] ([ServiceScheduleId]);
GO

CREATE INDEX [IX_ServiceSchedule_ServiceId] ON [ServiceSchedule] ([ServiceId]);
GO

ALTER TABLE [Reserve] ADD CONSTRAINT [FK_Reserve_ServiceSchedule_ServiceScheduleId] FOREIGN KEY ([ServiceScheduleId]) REFERENCES [ServiceSchedule] ([ServiceScheduleId]) ON DELETE NO ACTION;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250530012604_AddServiceSchedule', N'8.0.14');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [CustomerReserve] ADD [CustomerEmail] VARCHAR(150) NOT NULL DEFAULT '';
GO

ALTER TABLE [CustomerReserve] ADD [CustomerFullName] VARCHAR(250) NOT NULL DEFAULT '';
GO

ALTER TABLE [CustomerReserve] ADD [DestinationCityName] VARCHAR(100) NOT NULL DEFAULT '';
GO

ALTER TABLE [CustomerReserve] ADD [DocumentNumber] VARCHAR(50) NOT NULL DEFAULT '';
GO

ALTER TABLE [CustomerReserve] ADD [DriverName] VARCHAR(100) NULL;
GO

ALTER TABLE [CustomerReserve] ADD [DropoffAddress] VARCHAR(250) NULL;
GO

ALTER TABLE [CustomerReserve] ADD [OriginCityName] VARCHAR(100) NOT NULL DEFAULT '';
GO

ALTER TABLE [CustomerReserve] ADD [Phone1] VARCHAR(30) NULL;
GO

ALTER TABLE [CustomerReserve] ADD [Phone2] VARCHAR(30) NULL;
GO

ALTER TABLE [CustomerReserve] ADD [PickupAddress] VARCHAR(250) NULL;
GO

ALTER TABLE [CustomerReserve] ADD [ServiceName] VARCHAR(250) NOT NULL DEFAULT '';
GO

ALTER TABLE [CustomerReserve] ADD [VehicleInternalNumber] VARCHAR(20) NOT NULL DEFAULT '';
GO

UPDATE [Role] SET [CreatedDate] = '2025-06-01T20:42:32.3185202Z'
WHERE [RoleId] = 1;
SELECT @@ROWCOUNT;

GO

UPDATE [Role] SET [CreatedDate] = '2025-06-01T20:42:32.3185206Z'
WHERE [RoleId] = 2;
SELECT @@ROWCOUNT;

GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250601204232_DesnormalizarCustomerReserveParaReportes', N'8.0.14');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [Reserve] ADD [DepartureHour] time NOT NULL DEFAULT '00:00:00';
GO

ALTER TABLE [Reserve] ADD [DestinationName] VARCHAR(100) NOT NULL DEFAULT '';
GO

ALTER TABLE [Reserve] ADD [IsHoliday] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [Reserve] ADD [OriginName] VARCHAR(100) NOT NULL DEFAULT '';
GO

ALTER TABLE [Reserve] ADD [ServiceName] VARCHAR(250) NOT NULL DEFAULT '';
GO

UPDATE [Role] SET [CreatedDate] = '2025-06-07T01:18:11.3747054Z'
WHERE [RoleId] = 1;
SELECT @@ROWCOUNT;

GO

UPDATE [Role] SET [CreatedDate] = '2025-06-07T01:18:11.3747058Z'
WHERE [RoleId] = 2;
SELECT @@ROWCOUNT;

GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250607011811_AddColumnsInReserve', N'8.0.14');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

DECLARE @var8 sysname;
SELECT @var8 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CustomerReserve]') AND [c].[name] = N'PaymentMethod');
IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [CustomerReserve] DROP CONSTRAINT [' + @var8 + '];');
ALTER TABLE [CustomerReserve] DROP COLUMN [PaymentMethod];
GO

DECLARE @var9 sysname;
SELECT @var9 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CustomerReserve]') AND [c].[name] = N'StatusPayment');
IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [CustomerReserve] DROP CONSTRAINT [' + @var9 + '];');
ALTER TABLE [CustomerReserve] DROP COLUMN [StatusPayment];
GO

ALTER TABLE [CustomerReserve] ADD [ReferencePaymentId] int NULL;
GO

CREATE TABLE [ReservePayment] (
    [ReservePaymentId] int NOT NULL IDENTITY,
    [ReserveId] int NOT NULL,
    [Method] VARCHAR(20) NOT NULL,
    [Status] VARCHAR(20) NOT NULL,
    [CustomerId] int NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [ParentReservePaymentId] int NULL,
    [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System',
    [UpdatedBy] VARCHAR(256) NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE()),
    [UpdatedDate] datetime2 NULL,
    CONSTRAINT [PK_ReservePayment] PRIMARY KEY ([ReservePaymentId]),
    CONSTRAINT [FK_ReservePayment_Customer_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customer] ([CustomerId]) ON DELETE CASCADE,
    CONSTRAINT [FK_ReservePayment_ReservePayment_ParentReservePaymentId] FOREIGN KEY ([ParentReservePaymentId]) REFERENCES [ReservePayment] ([ReservePaymentId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ReservePayment_Reserve_ReserveId] FOREIGN KEY ([ReserveId]) REFERENCES [Reserve] ([ReserveId]) ON DELETE CASCADE
);
GO

UPDATE [Role] SET [CreatedDate] = '2025-06-18T04:36:00.1032752Z'
WHERE [RoleId] = 1;
SELECT @@ROWCOUNT;

GO

UPDATE [Role] SET [CreatedDate] = '2025-06-18T04:36:00.1032754Z'
WHERE [RoleId] = 2;
SELECT @@ROWCOUNT;

GO

CREATE INDEX [IX_ReservePayment_CustomerId] ON [ReservePayment] ([CustomerId]);
GO

CREATE INDEX [IX_ReservePayment_ParentReservePaymentId] ON [ReservePayment] ([ParentReservePaymentId]);
GO

CREATE INDEX [IX_ReservePayment_ReserveId] ON [ReservePayment] ([ReserveId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250618043600_AddReservePayment', N'8.0.14');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [Customer] ADD [CurrentBalance] decimal(18,2) NOT NULL DEFAULT 0.0;
GO

CREATE TABLE [CustomerAccountTransactions] (
    [CustomerAccountTransactionId] int NOT NULL IDENTITY,
    [CustomerId] int NOT NULL,
    [Date] datetime2 NOT NULL,
    [Type] nvarchar(20) NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [Description] nvarchar(250) NULL,
    [RelatedReserveId] int NULL,
    [ReservePaymentId] int NULL,
    [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System',
    [UpdatedBy] VARCHAR(256) NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE()),
    [UpdatedDate] datetime2 NULL,
    CONSTRAINT [PK_CustomerAccountTransactions] PRIMARY KEY ([CustomerAccountTransactionId]),
    CONSTRAINT [FK_CustomerAccountTransactions_Customer_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customer] ([CustomerId]) ON DELETE CASCADE,
    CONSTRAINT [FK_CustomerAccountTransactions_ReservePayment_ReservePaymentId] FOREIGN KEY ([ReservePaymentId]) REFERENCES [ReservePayment] ([ReservePaymentId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_CustomerAccountTransactions_Reserve_RelatedReserveId] FOREIGN KEY ([RelatedReserveId]) REFERENCES [Reserve] ([ReserveId]) ON DELETE SET NULL
);
GO

UPDATE [Role] SET [CreatedDate] = '2025-06-27T22:28:21.0110427Z'
WHERE [RoleId] = 1;
SELECT @@ROWCOUNT;

GO

UPDATE [Role] SET [CreatedDate] = '2025-06-27T22:28:21.0110429Z'
WHERE [RoleId] = 2;
SELECT @@ROWCOUNT;

GO

CREATE INDEX [IX_CustomerAccountTransactions_CustomerId] ON [CustomerAccountTransactions] ([CustomerId]);
GO

CREATE INDEX [IX_CustomerAccountTransactions_Date] ON [CustomerAccountTransactions] ([Date]);
GO

CREATE INDEX [IX_CustomerAccountTransactions_RelatedReserveId] ON [CustomerAccountTransactions] ([RelatedReserveId]);
GO

CREATE INDEX [IX_CustomerAccountTransactions_ReservePaymentId] ON [CustomerAccountTransactions] ([ReservePaymentId]);
GO

CREATE INDEX [IX_CustomerAccountTransactions_Type] ON [CustomerAccountTransactions] ([Type]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250627222821_AddCtaCte', N'8.0.14');
GO

COMMIT;
GO

