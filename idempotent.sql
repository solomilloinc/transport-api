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
    [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System',
    [UpdatedBy] VARCHAR(256) NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE()),
    [UpdatedDate] datetime2 NULL,
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
    [CurrentBalance] decimal(18,2) NOT NULL DEFAULT 0.0,
    [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System',
    [UpdatedBy] VARCHAR(256) NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE()),
    [UpdatedDate] datetime2 NULL,
    CONSTRAINT [PK_Customer] PRIMARY KEY ([CustomerId])
);
GO

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
GO

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
    [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System',
    [UpdatedBy] VARCHAR(256) NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE()),
    [UpdatedDate] datetime2 NULL,
    CONSTRAINT [PK_Role] PRIMARY KEY ([RoleId])
);
GO

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
GO

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
    [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System',
    [UpdatedBy] VARCHAR(256) NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE()),
    [UpdatedDate] datetime2 NULL,
    CONSTRAINT [PK_Vehicle] PRIMARY KEY ([VehicleId]),
    CONSTRAINT [FK_Vehicle_VehicleType_VehicleTypeId] FOREIGN KEY ([VehicleTypeId]) REFERENCES [VehicleType] ([VehicleTypeId]) ON DELETE CASCADE
);
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
    [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System',
    [UpdatedBy] VARCHAR(256) NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE()),
    [UpdatedDate] datetime2 NULL,
    CONSTRAINT [PK_RefreshToken] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RefreshToken_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [User] ([UserId]) ON DELETE CASCADE
);
GO

CREATE TABLE [Service] (
    [ServiceId] int NOT NULL IDENTITY,
    [Name] nvarchar(250) NOT NULL,
    [OriginId] int NOT NULL,
    [DestinationId] int NOT NULL,
    [EstimatedDuration] time NOT NULL,
    [VehicleId] int NOT NULL,
    [Status] int NOT NULL,
    [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System',
    [UpdatedBy] VARCHAR(256) NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE()),
    [UpdatedDate] datetime2 NULL,
    CONSTRAINT [PK_Service] PRIMARY KEY ([ServiceId]),
    CONSTRAINT [FK_Service_City_DestinationId] FOREIGN KEY ([DestinationId]) REFERENCES [City] ([CityId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Service_City_OriginId] FOREIGN KEY ([OriginId]) REFERENCES [City] ([CityId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Service_Vehicle_VehicleId] FOREIGN KEY ([VehicleId]) REFERENCES [Vehicle] ([VehicleId]) ON DELETE CASCADE
);
GO

CREATE TABLE [ReservePrice] (
    [ReservePriceId] int NOT NULL IDENTITY,
    [ServiceId] int NOT NULL,
    [Price] decimal(10,2) NOT NULL,
    [ReserveTypeId] int NOT NULL,
    [Status] int NOT NULL,
    [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System',
    [UpdatedBy] VARCHAR(256) NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE()),
    [UpdatedDate] datetime2 NULL,
    CONSTRAINT [PK_ReservePrice] PRIMARY KEY ([ReservePriceId]),
    CONSTRAINT [FK_ReservePrice_Service_ServiceId] FOREIGN KEY ([ServiceId]) REFERENCES [Service] ([ServiceId]) ON DELETE CASCADE
);
GO

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

CREATE TABLE [Reserve] (
    [ReserveId] int NOT NULL IDENTITY,
    [ReserveDate] datetime2 NOT NULL,
    [VehicleId] int NOT NULL,
    [DriverId] int NULL,
    [ServiceId] int NOT NULL,
    [ServiceScheduleId] int NOT NULL,
    [Status] VARCHAR(20) NOT NULL,
    [ServiceName] VARCHAR(250) NOT NULL,
    [OriginName] VARCHAR(100) NOT NULL,
    [DestinationName] VARCHAR(100) NOT NULL,
    [DepartureHour] time NOT NULL,
    [IsHoliday] bit NOT NULL,
    [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System',
    [UpdatedBy] VARCHAR(256) NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE()),
    [UpdatedDate] datetime2 NULL,
    CONSTRAINT [PK_Reserve] PRIMARY KEY ([ReserveId]),
    CONSTRAINT [FK_Reserve_Driver_DriverId] FOREIGN KEY ([DriverId]) REFERENCES [Driver] ([DriverId]) ON DELETE SET NULL,
    CONSTRAINT [FK_Reserve_ServiceSchedule_ServiceScheduleId] FOREIGN KEY ([ServiceScheduleId]) REFERENCES [ServiceSchedule] ([ServiceScheduleId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Reserve_Service_ServiceId] FOREIGN KEY ([ServiceId]) REFERENCES [Service] ([ServiceId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Reserve_Vehicle_VehicleId] FOREIGN KEY ([VehicleId]) REFERENCES [Vehicle] ([VehicleId]) ON DELETE NO ACTION
);
GO

CREATE TABLE [Passenger] (
    [PassengerId] int NOT NULL IDENTITY,
    [ReserveId] int NOT NULL,
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
    CONSTRAINT [FK_Passenger_Reserve_ReserveId] FOREIGN KEY ([ReserveId]) REFERENCES [Reserve] ([ReserveId])
);
GO

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
    [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System',
    [UpdatedBy] VARCHAR(256) NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE()),
    [UpdatedDate] datetime2 NULL,
    CONSTRAINT [PK_ReservePayment] PRIMARY KEY ([ReservePaymentId]),
    CONSTRAINT [FK_ReservePayment_Customer_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customer] ([CustomerId]),
    CONSTRAINT [FK_ReservePayment_ReservePayment_ParentReservePaymentId] FOREIGN KEY ([ParentReservePaymentId]) REFERENCES [ReservePayment] ([ReservePaymentId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ReservePayment_Reserve_ReserveId] FOREIGN KEY ([ReserveId]) REFERENCES [Reserve] ([ReserveId]) ON DELETE NO ACTION
);
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

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'RoleId', N'CreatedBy', N'CreatedDate', N'Name', N'UpdatedBy', N'UpdatedDate') AND [object_id] = OBJECT_ID(N'[Role]'))
    SET IDENTITY_INSERT [Role] ON;
INSERT INTO [Role] ([RoleId], [CreatedBy], [CreatedDate], [Name], [UpdatedBy], [UpdatedDate])
VALUES (1, 'System', '2025-08-31T21:09:34.7052618Z', N'Administrador', NULL, NULL),
(2, 'System', '2025-08-31T21:09:34.7052620Z', N'Cliente', NULL, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'RoleId', N'CreatedBy', N'CreatedDate', N'Name', N'UpdatedBy', N'UpdatedDate') AND [object_id] = OBJECT_ID(N'[Role]'))
    SET IDENTITY_INSERT [Role] OFF;
GO

CREATE UNIQUE INDEX [IX_City_Code] ON [City] ([Code]);
GO

CREATE UNIQUE INDEX [IX_Customer_DocumentNumber] ON [Customer] ([DocumentNumber]);
GO

CREATE UNIQUE INDEX [IX_Customer_Email] ON [Customer] ([Email]);
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

CREATE INDEX [IX_Direction_CityId] ON [Direction] ([CityId]);
GO

CREATE UNIQUE INDEX [IX_Driver_DocumentNumber] ON [Driver] ([DocumentNumber]);
GO

CREATE UNIQUE INDEX [IX_Holiday_HolidayDate] ON [Holiday] ([HolidayDate]);
GO

CREATE INDEX [IX_Passenger_CustomerId] ON [Passenger] ([CustomerId]);
GO

CREATE INDEX [IX_Passenger_DirectionId] ON [Passenger] ([DirectionId]);
GO

CREATE INDEX [IX_Passenger_DirectionId1] ON [Passenger] ([DirectionId1]);
GO

CREATE INDEX [IX_Passenger_DropoffLocationId] ON [Passenger] ([DropoffLocationId]);
GO

CREATE INDEX [IX_Passenger_PickupLocationId] ON [Passenger] ([PickupLocationId]);
GO

CREATE INDEX [IX_Passenger_ReserveId] ON [Passenger] ([ReserveId]);
GO

CREATE UNIQUE INDEX [IX_Passenger_ReserveId_DocumentNumber] ON [Passenger] ([ReserveId], [DocumentNumber]);
GO

CREATE INDEX [IX_Passenger_Status] ON [Passenger] ([Status]);
GO

CREATE INDEX [IX_RefreshToken_UserId] ON [RefreshToken] ([UserId]);
GO

CREATE INDEX [IX_Reserve_DriverId] ON [Reserve] ([DriverId]);
GO

CREATE INDEX [IX_Reserve_ServiceId_ReserveDate] ON [Reserve] ([ServiceId], [ReserveDate]);
GO

CREATE INDEX [IX_Reserve_ServiceScheduleId] ON [Reserve] ([ServiceScheduleId]);
GO

CREATE INDEX [IX_Reserve_Status_ReserveDate] ON [Reserve] ([Status], [ReserveDate]);
GO

CREATE INDEX [IX_Reserve_VehicleId] ON [Reserve] ([VehicleId]);
GO

CREATE INDEX [IX_ReservePayment_CustomerId] ON [ReservePayment] ([CustomerId]);
GO

CREATE INDEX [IX_ReservePayment_ParentReservePaymentId] ON [ReservePayment] ([ParentReservePaymentId]);
GO

CREATE INDEX [IX_ReservePayment_PaymentExternalId] ON [ReservePayment] ([PaymentExternalId]);
GO

CREATE INDEX [IX_ReservePayment_ReserveId] ON [ReservePayment] ([ReserveId]);
GO

CREATE INDEX [IX_ReservePayment_Status] ON [ReservePayment] ([Status]);
GO

CREATE INDEX [IX_ReservePrice_ServiceId_ReserveTypeId] ON [ReservePrice] ([ServiceId], [ReserveTypeId]);
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

CREATE INDEX [IX_ServiceSchedule_ServiceId] ON [ServiceSchedule] ([ServiceId]);
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
VALUES (N'20250831210935_InitialMigration', N'8.0.14');
GO

COMMIT;
GO

