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

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250415035110_InitialMigration'
)
BEGIN
    CREATE TABLE [City] (
        [CityId] int NOT NULL IDENTITY,
        [Name] nvarchar(250) NOT NULL,
        CONSTRAINT [PK_City] PRIMARY KEY ([CityId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250415035110_InitialMigration'
)
BEGIN
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
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250415035110_InitialMigration'
)
BEGIN
    CREATE TABLE [Driver] (
        [DriverId] int NOT NULL IDENTITY,
        [FirstName] nvarchar(100) NOT NULL,
        [LastName] nvarchar(100) NOT NULL,
        [DocumentNumber] nvarchar(50) NOT NULL,
        [Status] int NOT NULL,
        CONSTRAINT [PK_Driver] PRIMARY KEY ([DriverId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250415035110_InitialMigration'
)
BEGIN
    CREATE TABLE [Holiday] (
        [HolidayId] int NOT NULL IDENTITY,
        [HolidayDate] datetime2 NOT NULL,
        [Description] nvarchar(255) NOT NULL,
        CONSTRAINT [PK_Holiday] PRIMARY KEY ([HolidayId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250415035110_InitialMigration'
)
BEGIN
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
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250415035110_InitialMigration'
)
BEGIN
    CREATE TABLE [Role] (
        [RoleId] int NOT NULL IDENTITY,
        [Name] nvarchar(250) NOT NULL,
        CONSTRAINT [PK_Role] PRIMARY KEY ([RoleId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250415035110_InitialMigration'
)
BEGIN
    CREATE TABLE [VehicleType] (
        [VehicleTypeId] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [Quantity] int NOT NULL,
        [ImageBase64] text NULL,
        [Status] int NOT NULL,
        CONSTRAINT [PK_VehicleType] PRIMARY KEY ([VehicleTypeId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250415035110_InitialMigration'
)
BEGIN
    CREATE TABLE [Direction] (
        [DirectionId] int NOT NULL IDENTITY,
        [Name] nvarchar(250) NOT NULL,
        [CityId] int NOT NULL,
        CONSTRAINT [PK_Direction] PRIMARY KEY ([DirectionId]),
        CONSTRAINT [FK_Direction_City_CityId] FOREIGN KEY ([CityId]) REFERENCES [City] ([CityId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250415035110_InitialMigration'
)
BEGIN
    CREATE TABLE [Service] (
        [ServiceId] int NOT NULL IDENTITY,
        [Name] nvarchar(250) NOT NULL,
        [DayStart] tinyint NOT NULL,
        [DayEnd] tinyint NOT NULL,
        [OriginId] int NOT NULL,
        [DestinationId] int NOT NULL,
        [EstimatedDuration] time NOT NULL,
        [DepartureHour] time NOT NULL,
        [IsHoliday] bit NOT NULL,
        [VehicleId] int NOT NULL,
        [Status] bit NOT NULL,
        CONSTRAINT [PK_Service] PRIMARY KEY ([ServiceId]),
        CONSTRAINT [FK_Service_City_DestinationId] FOREIGN KEY ([DestinationId]) REFERENCES [City] ([CityId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Service_City_OriginId] FOREIGN KEY ([OriginId]) REFERENCES [City] ([CityId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250415035110_InitialMigration'
)
BEGIN
    CREATE TABLE [User] (
        [UserId] int NOT NULL IDENTITY,
        [CustomerId] int NULL,
        [RoleId] int NOT NULL,
        [Email] nvarchar(max) NOT NULL,
        [Password] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_User] PRIMARY KEY ([UserId]),
        CONSTRAINT [FK_User_Customer_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customer] ([CustomerId]),
        CONSTRAINT [FK_User_Role_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Role] ([RoleId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250415035110_InitialMigration'
)
BEGIN
    CREATE TABLE [Vehicle] (
        [VehicleId] int NOT NULL IDENTITY,
        [VehicleTypeId] int NOT NULL,
        [InternalNumber] nvarchar(50) NOT NULL,
        [Status] int NOT NULL,
        CONSTRAINT [PK_Vehicle] PRIMARY KEY ([VehicleId]),
        CONSTRAINT [FK_Vehicle_VehicleType_VehicleTypeId] FOREIGN KEY ([VehicleTypeId]) REFERENCES [VehicleType] ([VehicleTypeId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250415035110_InitialMigration'
)
BEGIN
    CREATE TABLE [ReservePrice] (
        [ReservePriceId] int NOT NULL IDENTITY,
        [ServiceId] int NOT NULL,
        [Price] decimal(10,2) NOT NULL,
        [ReserveTypeId] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_ReservePrice] PRIMARY KEY ([ReservePriceId]),
        CONSTRAINT [FK_ReservePrice_Service_ServiceId] FOREIGN KEY ([ServiceId]) REFERENCES [Service] ([ServiceId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250415035110_InitialMigration'
)
BEGIN
    CREATE TABLE [ServiceCustomer] (
        [ServiceCustomerId] int NOT NULL IDENTITY,
        [ServiceId] int NOT NULL,
        [CustomerId] int NOT NULL,
        CONSTRAINT [PK_ServiceCustomer] PRIMARY KEY ([ServiceCustomerId]),
        CONSTRAINT [FK_ServiceCustomer_Customer_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customer] ([CustomerId]) ON DELETE CASCADE,
        CONSTRAINT [FK_ServiceCustomer_Service_ServiceId] FOREIGN KEY ([ServiceId]) REFERENCES [Service] ([ServiceId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250415035110_InitialMigration'
)
BEGIN
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
        CONSTRAINT [FK_Reserve_Vehicle_VehicleId] FOREIGN KEY ([VehicleId]) REFERENCES [Vehicle] ([VehicleId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250415035110_InitialMigration'
)
BEGIN
    CREATE TABLE [CustomerReserve] (
        [CustomerReserveId] int NOT NULL IDENTITY,
        [CustomerId] int NOT NULL,
        [ReserveId] int NOT NULL,
        [IsPayment] bit NOT NULL,
        [StatusPayment] nvarchar(50) NOT NULL,
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
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250415035110_InitialMigration'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'RoleId', N'Name') AND [object_id] = OBJECT_ID(N'[Role]'))
        SET IDENTITY_INSERT [Role] ON;
    EXEC(N'INSERT INTO [Role] ([RoleId], [Name])
    VALUES (1, N''Administrador''),
    (2, N''Cliente'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'RoleId', N'Name') AND [object_id] = OBJECT_ID(N'[Role]'))
        SET IDENTITY_INSERT [Role] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250415035110_InitialMigration'
)
BEGIN
    CREATE UNIQUE INDEX [IX_City_Name] ON [City] ([Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250415035110_InitialMigration'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Customer_DocumentNumber] ON [Customer] ([DocumentNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250415035110_InitialMigration'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Customer_Email] ON [Customer] ([Email]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250415035110_InitialMigration'
)
BEGIN
    CREATE INDEX [IX_CustomerReserve_CustomerId] ON [CustomerReserve] ([CustomerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250415035110_InitialMigration'
)
BEGIN
    CREATE INDEX [IX_CustomerReserve_DropoffLocationId] ON [CustomerReserve] ([DropoffLocationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250415035110_InitialMigration'
)
BEGIN
    CREATE INDEX [IX_CustomerReserve_PickupLocationId] ON [CustomerReserve] ([PickupLocationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250415035110_InitialMigration'
)
BEGIN
    CREATE INDEX [IX_CustomerReserve_ReserveId] ON [CustomerReserve] ([ReserveId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250415035110_InitialMigration'
)
BEGIN
    CREATE INDEX [IX_Direction_CityId] ON [Direction] ([CityId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250415035110_InitialMigration'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Driver_DocumentNumber] ON [Driver] ([DocumentNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250415035110_InitialMigration'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Holiday_HolidayDate] ON [Holiday] ([HolidayDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250415035110_InitialMigration'
)
BEGIN
    CREATE INDEX [IX_Reserve_DriverId] ON [Reserve] ([DriverId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250415035110_InitialMigration'
)
BEGIN
    CREATE INDEX [IX_Reserve_ServiceId] ON [Reserve] ([ServiceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250415035110_InitialMigration'
)
BEGIN
    CREATE INDEX [IX_Reserve_VehicleId] ON [Reserve] ([VehicleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250415035110_InitialMigration'
)
BEGIN
    CREATE INDEX [IX_ReservePrice_ServiceId] ON [ReservePrice] ([ServiceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250415035110_InitialMigration'
)
BEGIN
    CREATE INDEX [IX_Service_DestinationId] ON [Service] ([DestinationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250415035110_InitialMigration'
)
BEGIN
    CREATE INDEX [IX_Service_OriginId] ON [Service] ([OriginId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250415035110_InitialMigration'
)
BEGIN
    CREATE INDEX [IX_ServiceCustomer_CustomerId] ON [ServiceCustomer] ([CustomerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250415035110_InitialMigration'
)
BEGIN
    CREATE INDEX [IX_ServiceCustomer_ServiceId] ON [ServiceCustomer] ([ServiceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250415035110_InitialMigration'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_User_CustomerId] ON [User] ([CustomerId]) WHERE [CustomerId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250415035110_InitialMigration'
)
BEGIN
    CREATE INDEX [IX_User_RoleId] ON [User] ([RoleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250415035110_InitialMigration'
)
BEGIN
    CREATE INDEX [IX_Vehicle_VehicleTypeId] ON [Vehicle] ([VehicleTypeId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250415035110_InitialMigration'
)
BEGIN
    CREATE UNIQUE INDEX [IX_VehicleType_Name] ON [VehicleType] ([Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250415035110_InitialMigration'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250415035110_InitialMigration', N'8.0.14');
END;
GO

COMMIT;
GO

