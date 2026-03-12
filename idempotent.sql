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
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE TABLE [City] (
        [CityId] int NOT NULL IDENTITY,
        [Code] nvarchar(50) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Status] int NOT NULL,
        [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System',
        [UpdatedBy] VARCHAR(256) NULL,
        [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE()),
        [UpdatedDate] datetime2 NULL,
        CONSTRAINT [PK_City] PRIMARY KEY ([CityId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
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
        [CurrentBalance] decimal(18,2) NOT NULL DEFAULT 0.0,
        [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System',
        [UpdatedBy] VARCHAR(256) NULL,
        [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE()),
        [UpdatedDate] datetime2 NULL,
        CONSTRAINT [PK_Customer] PRIMARY KEY ([CustomerId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE TABLE [Driver] (
        [DriverId] int NOT NULL IDENTITY,
        [FirstName] nvarchar(100) NOT NULL,
        [LastName] nvarchar(100) NOT NULL,
        [DocumentNumber] nvarchar(50) NOT NULL,
        [Status] int NOT NULL,
        [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System',
        [UpdatedBy] VARCHAR(256) NULL,
        [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE()),
        [UpdatedDate] datetime2 NULL,
        CONSTRAINT [PK_Driver] PRIMARY KEY ([DriverId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE TABLE [Holiday] (
        [HolidayId] int NOT NULL IDENTITY,
        [HolidayDate] datetime2 NOT NULL,
        [Description] nvarchar(255) NOT NULL,
        [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System',
        [UpdatedBy] VARCHAR(256) NULL,
        [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE()),
        [UpdatedDate] datetime2 NULL,
        CONSTRAINT [PK_Holiday] PRIMARY KEY ([HolidayId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
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
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE TABLE [Role] (
        [RoleId] int NOT NULL IDENTITY,
        [Name] nvarchar(250) NOT NULL,
        [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System',
        [UpdatedBy] VARCHAR(256) NULL,
        [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE()),
        [UpdatedDate] datetime2 NULL,
        CONSTRAINT [PK_Role] PRIMARY KEY ([RoleId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE TABLE [VehicleType] (
        [VehicleTypeId] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [Quantity] int NOT NULL,
        [ImageBase64] text NULL,
        [Status] int NOT NULL,
        [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System',
        [UpdatedBy] VARCHAR(256) NULL,
        [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE()),
        [UpdatedDate] datetime2 NULL,
        CONSTRAINT [PK_VehicleType] PRIMARY KEY ([VehicleTypeId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE TABLE [Direction] (
        [DirectionId] int NOT NULL IDENTITY,
        [Name] VARCHAR(250) NOT NULL,
        [Lat] float NULL,
        [Lng] float NULL,
        [CityId] int NOT NULL,
        [Status] int NOT NULL,
        [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System',
        [UpdatedBy] VARCHAR(256) NULL,
        [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE()),
        [UpdatedDate] datetime2 NULL,
        CONSTRAINT [PK_Direction] PRIMARY KEY ([DirectionId]),
        CONSTRAINT [FK_Direction_City_CityId] FOREIGN KEY ([CityId]) REFERENCES [City] ([CityId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE TABLE [Trip] (
        [TripId] int NOT NULL IDENTITY,
        [Description] nvarchar(200) NOT NULL,
        [OriginCityId] int NOT NULL,
        [DestinationCityId] int NOT NULL,
        [Status] nvarchar(max) NOT NULL,
        [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System',
        [UpdatedBy] VARCHAR(256) NULL,
        [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE()),
        [UpdatedDate] datetime2 NULL,
        CONSTRAINT [PK_Trip] PRIMARY KEY ([TripId]),
        CONSTRAINT [FK_Trip_City_DestinationCityId] FOREIGN KEY ([DestinationCityId]) REFERENCES [City] ([CityId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Trip_City_OriginCityId] FOREIGN KEY ([OriginCityId]) REFERENCES [City] ([CityId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
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
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE TABLE [Vehicle] (
        [VehicleId] int NOT NULL IDENTITY,
        [VehicleTypeId] int NOT NULL,
        [InternalNumber] nvarchar(50) NOT NULL,
        [Status] int NOT NULL,
        [AvailableQuantity] int NOT NULL,
        [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System',
        [UpdatedBy] VARCHAR(256) NULL,
        [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE()),
        [UpdatedDate] datetime2 NULL,
        CONSTRAINT [PK_Vehicle] PRIMARY KEY ([VehicleId]),
        CONSTRAINT [FK_Vehicle_VehicleType_VehicleTypeId] FOREIGN KEY ([VehicleTypeId]) REFERENCES [VehicleType] ([VehicleTypeId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE TABLE [TripPrice] (
        [TripPriceId] int NOT NULL IDENTITY,
        [TripId] int NOT NULL,
        [CityId] int NOT NULL,
        [DirectionId] int NULL,
        [ReserveTypeId] nvarchar(450) NOT NULL,
        [Price] decimal(18,2) NOT NULL,
        [Order] int NOT NULL,
        [Status] nvarchar(max) NOT NULL,
        [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System',
        [UpdatedBy] VARCHAR(256) NULL,
        [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE()),
        [UpdatedDate] datetime2 NULL,
        CONSTRAINT [PK_TripPrice] PRIMARY KEY ([TripPriceId]),
        CONSTRAINT [FK_TripPrice_City_CityId] FOREIGN KEY ([CityId]) REFERENCES [City] ([CityId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TripPrice_Direction_DirectionId] FOREIGN KEY ([DirectionId]) REFERENCES [Direction] ([DirectionId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TripPrice_Trip_TripId] FOREIGN KEY ([TripId]) REFERENCES [Trip] ([TripId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
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
        [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System',
        [UpdatedBy] VARCHAR(256) NULL,
        [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE()),
        [UpdatedDate] datetime2 NULL,
        CONSTRAINT [PK_RefreshToken] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RefreshToken_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [User] ([UserId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE TABLE [Service] (
        [ServiceId] int NOT NULL IDENTITY,
        [Name] nvarchar(250) NOT NULL,
        [TripId] int NOT NULL,
        [OriginId] int NOT NULL,
        [DestinationId] int NOT NULL,
        [EstimatedDuration] time NOT NULL,
        [VehicleId] int NOT NULL,
        [StartDay] int NOT NULL,
        [EndDay] int NOT NULL,
        [Status] int NOT NULL,
        [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System',
        [UpdatedBy] VARCHAR(256) NULL,
        [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE()),
        [UpdatedDate] datetime2 NULL,
        CONSTRAINT [PK_Service] PRIMARY KEY ([ServiceId]),
        CONSTRAINT [FK_Service_City_DestinationId] FOREIGN KEY ([DestinationId]) REFERENCES [City] ([CityId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Service_City_OriginId] FOREIGN KEY ([OriginId]) REFERENCES [City] ([CityId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Service_Trip_TripId] FOREIGN KEY ([TripId]) REFERENCES [Trip] ([TripId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Service_Vehicle_VehicleId] FOREIGN KEY ([VehicleId]) REFERENCES [Vehicle] ([VehicleId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE TABLE [ServiceCustomer] (
        [ServiceCustomerId] int NOT NULL IDENTITY,
        [ServiceId] int NOT NULL,
        [CustomerId] int NOT NULL,
        [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System',
        [UpdatedBy] VARCHAR(256) NULL,
        [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE()),
        [UpdatedDate] datetime2 NULL,
        CONSTRAINT [PK_ServiceCustomer] PRIMARY KEY ([ServiceCustomerId]),
        CONSTRAINT [FK_ServiceCustomer_Customer_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customer] ([CustomerId]) ON DELETE CASCADE,
        CONSTRAINT [FK_ServiceCustomer_Service_ServiceId] FOREIGN KEY ([ServiceId]) REFERENCES [Service] ([ServiceId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE TABLE [ServiceSchedule] (
        [ServiceScheduleId] int NOT NULL IDENTITY,
        [ServiceId] int NOT NULL,
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
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE TABLE [Reserve] (
        [ReserveId] int NOT NULL IDENTITY,
        [ReserveDate] datetime2 NOT NULL,
        [VehicleId] int NOT NULL,
        [DriverId] int NULL,
        [ServiceId] int NULL,
        [ServiceScheduleId] int NULL,
        [TripId] int NOT NULL,
        [OriginId] int NOT NULL,
        [DestinationId] int NOT NULL,
        [Status] VARCHAR(20) NOT NULL,
        [ServiceName] VARCHAR(250) NOT NULL,
        [OriginName] VARCHAR(100) NOT NULL,
        [DestinationName] VARCHAR(100) NOT NULL,
        [DepartureHour] time NOT NULL,
        [EstimatedDuration] time NOT NULL,
        [IsHoliday] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System',
        [UpdatedBy] VARCHAR(256) NULL,
        [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE()),
        [UpdatedDate] datetime2 NULL,
        CONSTRAINT [PK_Reserve] PRIMARY KEY ([ReserveId]),
        CONSTRAINT [FK_Reserve_City_DestinationId] FOREIGN KEY ([DestinationId]) REFERENCES [City] ([CityId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Reserve_City_OriginId] FOREIGN KEY ([OriginId]) REFERENCES [City] ([CityId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Reserve_Driver_DriverId] FOREIGN KEY ([DriverId]) REFERENCES [Driver] ([DriverId]) ON DELETE SET NULL,
        CONSTRAINT [FK_Reserve_ServiceSchedule_ServiceScheduleId] FOREIGN KEY ([ServiceScheduleId]) REFERENCES [ServiceSchedule] ([ServiceScheduleId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Reserve_Service_ServiceId] FOREIGN KEY ([ServiceId]) REFERENCES [Service] ([ServiceId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Reserve_Trip_TripId] FOREIGN KEY ([TripId]) REFERENCES [Trip] ([TripId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Reserve_Vehicle_VehicleId] FOREIGN KEY ([VehicleId]) REFERENCES [Vehicle] ([VehicleId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE TABLE [CashBox] (
        [CashBoxId] int NOT NULL IDENTITY,
        [Description] NVARCHAR(200) NULL,
        [OpenedAt] datetime2 NOT NULL,
        [ClosedAt] datetime2 NULL,
        [Status] VARCHAR(20) NOT NULL,
        [OpenedByUserId] int NOT NULL,
        [ClosedByUserId] int NULL,
        [ReserveId] int NULL,
        [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System',
        [UpdatedBy] VARCHAR(256) NULL,
        [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE()),
        [UpdatedDate] datetime2 NULL,
        CONSTRAINT [PK_CashBox] PRIMARY KEY ([CashBoxId]),
        CONSTRAINT [FK_CashBox_Reserve_ReserveId] FOREIGN KEY ([ReserveId]) REFERENCES [Reserve] ([ReserveId]) ON DELETE SET NULL,
        CONSTRAINT [FK_CashBox_User_ClosedByUserId] FOREIGN KEY ([ClosedByUserId]) REFERENCES [User] ([UserId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CashBox_User_OpenedByUserId] FOREIGN KEY ([OpenedByUserId]) REFERENCES [User] ([UserId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE TABLE [Passenger] (
        [PassengerId] int NOT NULL IDENTITY,
        [ReserveId] int NOT NULL,
        [ReserveRelatedId] int NULL,
        [FirstName] VARCHAR(100) NOT NULL,
        [LastName] VARCHAR(100) NOT NULL,
        [DocumentNumber] VARCHAR(50) NOT NULL,
        [Email] VARCHAR(150) NULL,
        [Phone] VARCHAR(30) NULL,
        [PickupLocationId] int NULL,
        [DropoffLocationId] int NULL,
        [PickupAddress] VARCHAR(250) NULL,
        [DropoffAddress] VARCHAR(250) NULL,
        [HasTraveled] bit NOT NULL DEFAULT CAST(0 AS bit),
        [Price] decimal(18,2) NOT NULL,
        [Status] VARCHAR(20) NOT NULL,
        [CustomerId] int NULL,
        [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System',
        [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE()),
        [UpdatedBy] VARCHAR(256) NULL,
        [UpdatedDate] datetime2 NULL,
        [DirectionId] int NULL,
        [DirectionId1] int NULL,
        CONSTRAINT [PK_Passenger] PRIMARY KEY ([PassengerId]),
        CONSTRAINT [FK_Passenger_Customer_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customer] ([CustomerId]),
        CONSTRAINT [FK_Passenger_Direction_DirectionId] FOREIGN KEY ([DirectionId]) REFERENCES [Direction] ([DirectionId]),
        CONSTRAINT [FK_Passenger_Direction_DirectionId1] FOREIGN KEY ([DirectionId1]) REFERENCES [Direction] ([DirectionId]),
        CONSTRAINT [FK_Passenger_Direction_DropoffLocationId] FOREIGN KEY ([DropoffLocationId]) REFERENCES [Direction] ([DirectionId]),
        CONSTRAINT [FK_Passenger_Direction_PickupLocationId] FOREIGN KEY ([PickupLocationId]) REFERENCES [Direction] ([DirectionId]),
        CONSTRAINT [FK_Passenger_Reserve_ReserveId] FOREIGN KEY ([ReserveId]) REFERENCES [Reserve] ([ReserveId]),
        CONSTRAINT [FK_Passenger_Reserve_ReserveRelatedId] FOREIGN KEY ([ReserveRelatedId]) REFERENCES [Reserve] ([ReserveId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE TABLE [ReserveSlotLock] (
        [ReserveSlotLockId] int NOT NULL IDENTITY,
        [LockToken] VARCHAR(50) NOT NULL,
        [OutboundReserveId] int NOT NULL,
        [ReturnReserveId] int NULL,
        [SlotsLocked] int NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [Status] VARCHAR(20) NOT NULL,
        [RowVersion] rowversion NOT NULL,
        [UserEmail] VARCHAR(100) NULL,
        [UserDocumentNumber] VARCHAR(20) NULL,
        [CustomerId] int NULL,
        [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System',
        [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE()),
        [UpdatedBy] VARCHAR(256) NULL,
        [UpdatedDate] datetime2 NULL,
        CONSTRAINT [PK_ReserveSlotLock] PRIMARY KEY ([ReserveSlotLockId]),
        CONSTRAINT [FK_ReserveSlotLock_Customer_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customer] ([CustomerId]) ON DELETE SET NULL,
        CONSTRAINT [FK_ReserveSlotLock_Reserve_OutboundReserveId] FOREIGN KEY ([OutboundReserveId]) REFERENCES [Reserve] ([ReserveId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ReserveSlotLock_Reserve_ReturnReserveId] FOREIGN KEY ([ReturnReserveId]) REFERENCES [Reserve] ([ReserveId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE TABLE [ReservePayment] (
        [ReservePaymentId] int NOT NULL IDENTITY,
        [ReserveId] int NOT NULL,
        [Method] VARCHAR(20) NOT NULL,
        [Status] VARCHAR(20) NOT NULL,
        [StatusDetail] VARCHAR(MAX) NULL,
        [ResultApiExternalRawJson] NVARCHAR(MAX) NULL,
        [CustomerId] int NULL,
        [Amount] decimal(18,2) NOT NULL,
        [PaymentExternalId] BIGINT NULL,
        [PayerName] VARCHAR(150) NULL,
        [PayerDocumentNumber] VARCHAR(50) NULL,
        [PayerEmail] VARCHAR(150) NULL,
        [ParentReservePaymentId] int NULL,
        [CashBoxId] int NULL,
        [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System',
        [UpdatedBy] VARCHAR(256) NULL,
        [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE()),
        [UpdatedDate] datetime2 NULL,
        CONSTRAINT [PK_ReservePayment] PRIMARY KEY ([ReservePaymentId]),
        CONSTRAINT [FK_ReservePayment_CashBox_CashBoxId] FOREIGN KEY ([CashBoxId]) REFERENCES [CashBox] ([CashBoxId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ReservePayment_Customer_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customer] ([CustomerId]),
        CONSTRAINT [FK_ReservePayment_ReservePayment_ParentReservePaymentId] FOREIGN KEY ([ParentReservePaymentId]) REFERENCES [ReservePayment] ([ReservePaymentId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ReservePayment_Reserve_ReserveId] FOREIGN KEY ([ReserveId]) REFERENCES [Reserve] ([ReserveId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
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
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'RoleId', N'CreatedBy', N'CreatedDate', N'Name', N'UpdatedBy', N'UpdatedDate') AND [object_id] = OBJECT_ID(N'[Role]'))
        SET IDENTITY_INSERT [Role] ON;
    EXEC(N'INSERT INTO [Role] ([RoleId], [CreatedBy], [CreatedDate], [Name], [UpdatedBy], [UpdatedDate])
    VALUES (1, ''System'', ''2026-01-19T22:28:02.1829444Z'', N''Administrador'', NULL, NULL),
    (2, ''System'', ''2026-01-19T22:28:02.1829448Z'', N''Cliente'', NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'RoleId', N'CreatedBy', N'CreatedDate', N'Name', N'UpdatedBy', N'UpdatedDate') AND [object_id] = OBJECT_ID(N'[Role]'))
        SET IDENTITY_INSERT [Role] OFF;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CashBox_ClosedByUserId] ON [CashBox] ([ClosedByUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CashBox_OpenedAt] ON [CashBox] ([OpenedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CashBox_OpenedByUserId] ON [CashBox] ([OpenedByUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CashBox_ReserveId] ON [CashBox] ([ReserveId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CashBox_Status] ON [CashBox] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CashBox_Status_OpenedAt] ON [CashBox] ([Status], [OpenedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_City_Code] ON [City] ([Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Customer_DocumentNumber] ON [Customer] ([DocumentNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Customer_Email] ON [Customer] ([Email]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CustomerAccountTransactions_CustomerId] ON [CustomerAccountTransactions] ([CustomerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CustomerAccountTransactions_Date] ON [CustomerAccountTransactions] ([Date]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CustomerAccountTransactions_RelatedReserveId] ON [CustomerAccountTransactions] ([RelatedReserveId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CustomerAccountTransactions_ReservePaymentId] ON [CustomerAccountTransactions] ([ReservePaymentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CustomerAccountTransactions_Type] ON [CustomerAccountTransactions] ([Type]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Direction_CityId] ON [Direction] ([CityId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Driver_DocumentNumber] ON [Driver] ([DocumentNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Holiday_HolidayDate] ON [Holiday] ([HolidayDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Passenger_CustomerId] ON [Passenger] ([CustomerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Passenger_DirectionId] ON [Passenger] ([DirectionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Passenger_DirectionId1] ON [Passenger] ([DirectionId1]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Passenger_DropoffLocationId] ON [Passenger] ([DropoffLocationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Passenger_PickupLocationId] ON [Passenger] ([PickupLocationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Passenger_ReserveId] ON [Passenger] ([ReserveId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Passenger_ReserveId_DocumentNumber] ON [Passenger] ([ReserveId], [DocumentNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Passenger_ReserveRelatedId] ON [Passenger] ([ReserveRelatedId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Passenger_Status] ON [Passenger] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RefreshToken_UserId] ON [RefreshToken] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reserve_DestinationId] ON [Reserve] ([DestinationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reserve_DriverId] ON [Reserve] ([DriverId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reserve_OriginId_DestinationId_ReserveDate] ON [Reserve] ([OriginId], [DestinationId], [ReserveDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reserve_ServiceId_ReserveDate] ON [Reserve] ([ServiceId], [ReserveDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reserve_ServiceScheduleId] ON [Reserve] ([ServiceScheduleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reserve_Status_ReserveDate] ON [Reserve] ([Status], [ReserveDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reserve_TripId_ReserveDate] ON [Reserve] ([TripId], [ReserveDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reserve_VehicleId] ON [Reserve] ([VehicleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ReservePayment_CashBoxId] ON [ReservePayment] ([CashBoxId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ReservePayment_CustomerId] ON [ReservePayment] ([CustomerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ReservePayment_ParentReservePaymentId] ON [ReservePayment] ([ParentReservePaymentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ReservePayment_PaymentExternalId] ON [ReservePayment] ([PaymentExternalId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ReservePayment_ReserveId] ON [ReservePayment] ([ReserveId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ReservePayment_Status] ON [ReservePayment] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ReserveSlotLock_CreatedDate] ON [ReserveSlotLock] ([CreatedDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ReserveSlotLock_CustomerId] ON [ReserveSlotLock] ([CustomerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ReserveSlotLock_LockToken] ON [ReserveSlotLock] ([LockToken]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ReserveSlotLock_OutboundReserveId] ON [ReserveSlotLock] ([OutboundReserveId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ReserveSlotLock_ReturnReserveId] ON [ReserveSlotLock] ([ReturnReserveId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ReserveSlotLock_Status_ExpiresAt] ON [ReserveSlotLock] ([Status], [ExpiresAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Service_DestinationId] ON [Service] ([DestinationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Service_OriginId] ON [Service] ([OriginId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Service_TripId] ON [Service] ([TripId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Service_VehicleId] ON [Service] ([VehicleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ServiceCustomer_CustomerId] ON [ServiceCustomer] ([CustomerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ServiceCustomer_ServiceId] ON [ServiceCustomer] ([ServiceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ServiceSchedule_ServiceId] ON [ServiceSchedule] ([ServiceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Trip_DestinationCityId] ON [Trip] ([DestinationCityId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Trip_OriginCityId_DestinationCityId] ON [Trip] ([OriginCityId], [DestinationCityId]) WHERE [Status] = ''Active''');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TripPrice_CityId] ON [TripPrice] ([CityId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TripPrice_DirectionId] ON [TripPrice] ([DirectionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_TripPrice_TripId_CityId_DirectionId_ReserveTypeId] ON [TripPrice] ([TripId], [CityId], [DirectionId], [ReserveTypeId]) WHERE [Status] = ''Active''');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_User_CustomerId] ON [User] ([CustomerId]) WHERE [CustomerId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_User_RoleId] ON [User] ([RoleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Vehicle_VehicleTypeId] ON [Vehicle] ([VehicleTypeId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_VehicleType_Name] ON [VehicleType] ([Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119222802_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260119222802_InitialCreate', N'8.0.14');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260123202229_AddDirectionsInReserveAndService'
)
BEGIN
    CREATE TABLE [ReserveDirection] (
        [ReserveDirectionId] int NOT NULL IDENTITY,
        [ReserveId] int NOT NULL,
        [DirectionId] int NOT NULL,
        [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System',
        [UpdatedBy] VARCHAR(256) NULL,
        [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE()),
        [UpdatedDate] datetime2 NULL,
        CONSTRAINT [PK_ReserveDirection] PRIMARY KEY ([ReserveDirectionId]),
        CONSTRAINT [FK_ReserveDirection_Direction_DirectionId] FOREIGN KEY ([DirectionId]) REFERENCES [Direction] ([DirectionId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ReserveDirection_Reserve_ReserveId] FOREIGN KEY ([ReserveId]) REFERENCES [Reserve] ([ReserveId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260123202229_AddDirectionsInReserveAndService'
)
BEGIN
    CREATE TABLE [ServiceDirection] (
        [ServiceDirectionId] int NOT NULL IDENTITY,
        [ServiceId] int NOT NULL,
        [DirectionId] int NOT NULL,
        [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System',
        [UpdatedBy] VARCHAR(256) NULL,
        [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE()),
        [UpdatedDate] datetime2 NULL,
        CONSTRAINT [PK_ServiceDirection] PRIMARY KEY ([ServiceDirectionId]),
        CONSTRAINT [FK_ServiceDirection_Direction_DirectionId] FOREIGN KEY ([DirectionId]) REFERENCES [Direction] ([DirectionId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ServiceDirection_Service_ServiceId] FOREIGN KEY ([ServiceId]) REFERENCES [Service] ([ServiceId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260123202229_AddDirectionsInReserveAndService'
)
BEGIN
    EXEC(N'UPDATE [Role] SET [CreatedDate] = ''2026-01-23T20:22:29.3484391Z''
    WHERE [RoleId] = 1;
    SELECT @@ROWCOUNT');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260123202229_AddDirectionsInReserveAndService'
)
BEGIN
    EXEC(N'UPDATE [Role] SET [CreatedDate] = ''2026-01-23T20:22:29.3484393Z''
    WHERE [RoleId] = 2;
    SELECT @@ROWCOUNT');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260123202229_AddDirectionsInReserveAndService'
)
BEGIN
    CREATE INDEX [IX_ReserveDirection_DirectionId] ON [ReserveDirection] ([DirectionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260123202229_AddDirectionsInReserveAndService'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ReserveDirection_ReserveId_DirectionId] ON [ReserveDirection] ([ReserveId], [DirectionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260123202229_AddDirectionsInReserveAndService'
)
BEGIN
    CREATE INDEX [IX_ServiceDirection_DirectionId] ON [ServiceDirection] ([DirectionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260123202229_AddDirectionsInReserveAndService'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ServiceDirection_ServiceId_DirectionId] ON [ServiceDirection] ([ServiceId], [DirectionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260123202229_AddDirectionsInReserveAndService'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260123202229_AddDirectionsInReserveAndService', N'8.0.14');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260123221228_DeleteOriginIdAndDestionationServiceAndReserve'
)
BEGIN
    ALTER TABLE [Reserve] DROP CONSTRAINT [FK_Reserve_City_DestinationId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260123221228_DeleteOriginIdAndDestionationServiceAndReserve'
)
BEGIN
    ALTER TABLE [Reserve] DROP CONSTRAINT [FK_Reserve_City_OriginId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260123221228_DeleteOriginIdAndDestionationServiceAndReserve'
)
BEGIN
    ALTER TABLE [Service] DROP CONSTRAINT [FK_Service_City_DestinationId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260123221228_DeleteOriginIdAndDestionationServiceAndReserve'
)
BEGIN
    ALTER TABLE [Service] DROP CONSTRAINT [FK_Service_City_OriginId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260123221228_DeleteOriginIdAndDestionationServiceAndReserve'
)
BEGIN
    DROP INDEX [IX_Service_DestinationId] ON [Service];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260123221228_DeleteOriginIdAndDestionationServiceAndReserve'
)
BEGIN
    DROP INDEX [IX_Service_OriginId] ON [Service];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260123221228_DeleteOriginIdAndDestionationServiceAndReserve'
)
BEGIN
    DROP INDEX [IX_Reserve_DestinationId] ON [Reserve];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260123221228_DeleteOriginIdAndDestionationServiceAndReserve'
)
BEGIN
    DROP INDEX [IX_Reserve_OriginId_DestinationId_ReserveDate] ON [Reserve];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260123221228_DeleteOriginIdAndDestionationServiceAndReserve'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Service]') AND [c].[name] = N'DestinationId');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [Service] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [Service] DROP COLUMN [DestinationId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260123221228_DeleteOriginIdAndDestionationServiceAndReserve'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Service]') AND [c].[name] = N'OriginId');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Service] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [Service] DROP COLUMN [OriginId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260123221228_DeleteOriginIdAndDestionationServiceAndReserve'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Reserve]') AND [c].[name] = N'DestinationId');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [Reserve] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [Reserve] DROP COLUMN [DestinationId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260123221228_DeleteOriginIdAndDestionationServiceAndReserve'
)
BEGIN
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Reserve]') AND [c].[name] = N'OriginId');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [Reserve] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [Reserve] DROP COLUMN [OriginId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260123221228_DeleteOriginIdAndDestionationServiceAndReserve'
)
BEGIN
    EXEC(N'UPDATE [Role] SET [CreatedDate] = ''2026-01-23T22:12:27.8790375Z''
    WHERE [RoleId] = 1;
    SELECT @@ROWCOUNT');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260123221228_DeleteOriginIdAndDestionationServiceAndReserve'
)
BEGIN
    EXEC(N'UPDATE [Role] SET [CreatedDate] = ''2026-01-23T22:12:27.8790378Z''
    WHERE [RoleId] = 2;
    SELECT @@ROWCOUNT');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260123221228_DeleteOriginIdAndDestionationServiceAndReserve'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260123221228_DeleteOriginIdAndDestionationServiceAndReserve', N'8.0.14');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260311034846_AddTripDirection'
)
BEGIN
    CREATE TABLE [TripDirection] (
        [TripDirectionId] int NOT NULL IDENTITY,
        [TripId] int NOT NULL,
        [DirectionId] int NOT NULL,
        [Order] int NOT NULL,
        [PickupTimeOffset] time NOT NULL,
        [Status] nvarchar(max) NOT NULL,
        [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System',
        [UpdatedBy] VARCHAR(256) NULL,
        [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE()),
        [UpdatedDate] datetime2 NULL,
        CONSTRAINT [PK_TripDirection] PRIMARY KEY ([TripDirectionId]),
        CONSTRAINT [FK_TripDirection_Direction_DirectionId] FOREIGN KEY ([DirectionId]) REFERENCES [Direction] ([DirectionId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TripDirection_Trip_TripId] FOREIGN KEY ([TripId]) REFERENCES [Trip] ([TripId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260311034846_AddTripDirection'
)
BEGIN
    EXEC(N'UPDATE [Role] SET [CreatedDate] = ''2026-03-11T03:48:45.9582998Z''
    WHERE [RoleId] = 1;
    SELECT @@ROWCOUNT');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260311034846_AddTripDirection'
)
BEGIN
    EXEC(N'UPDATE [Role] SET [CreatedDate] = ''2026-03-11T03:48:45.9583000Z''
    WHERE [RoleId] = 2;
    SELECT @@ROWCOUNT');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260311034846_AddTripDirection'
)
BEGIN
    CREATE INDEX [IX_TripDirection_DirectionId] ON [TripDirection] ([DirectionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260311034846_AddTripDirection'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_TripDirection_TripId_DirectionId] ON [TripDirection] ([TripId], [DirectionId]) WHERE [Status] = ''Active''');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260311034846_AddTripDirection'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260311034846_AddTripDirection', N'8.0.14');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260312034834_RenameTripDirectionToTripPickupStop'
)
BEGIN
    EXEC sp_rename N'[TripDirection]', N'TripPickupStop';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260312034834_RenameTripDirectionToTripPickupStop'
)
BEGIN
    EXEC sp_rename N'[TripPickupStop].[TripDirectionId]', N'TripPickupStopId', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260312034834_RenameTripDirectionToTripPickupStop'
)
BEGIN
    EXEC sp_rename N'[TripPickupStop].[IX_TripDirection_DirectionId]', N'IX_TripPickupStop_DirectionId', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260312034834_RenameTripDirectionToTripPickupStop'
)
BEGIN
    EXEC sp_rename N'[TripPickupStop].[IX_TripDirection_TripId_DirectionId]', N'IX_TripPickupStop_TripId_DirectionId', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260312034834_RenameTripDirectionToTripPickupStop'
)
BEGIN
    ALTER TABLE [TripPickupStop] DROP CONSTRAINT [FK_TripDirection_Direction_DirectionId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260312034834_RenameTripDirectionToTripPickupStop'
)
BEGIN
    ALTER TABLE [TripPickupStop] DROP CONSTRAINT [FK_TripDirection_Trip_TripId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260312034834_RenameTripDirectionToTripPickupStop'
)
BEGIN
    ALTER TABLE [TripPickupStop] ADD CONSTRAINT [FK_TripPickupStop_Direction_DirectionId] FOREIGN KEY ([DirectionId]) REFERENCES [Direction] ([DirectionId]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260312034834_RenameTripDirectionToTripPickupStop'
)
BEGIN
    ALTER TABLE [TripPickupStop] ADD CONSTRAINT [FK_TripPickupStop_Trip_TripId] FOREIGN KEY ([TripId]) REFERENCES [Trip] ([TripId]) ON DELETE CASCADE;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260312034834_RenameTripDirectionToTripPickupStop'
)
BEGIN
    EXEC sp_rename N'PK_TripDirection', N'PK_TripPickupStop', N'OBJECT'
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260312034834_RenameTripDirectionToTripPickupStop'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260312034834_RenameTripDirectionToTripPickupStop', N'8.0.14');
END;
GO

COMMIT;
GO

