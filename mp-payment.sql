BEGIN TRANSACTION;
GO

CREATE TABLE [Payment] (
    [PaymentId] int NOT NULL IDENTITY,
    [PaymentMpId] bigint NULL,
    [ExternalReference] nvarchar(100) NULL,
    [Email] nvarchar(100) NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [Currency] nvarchar(3) NOT NULL,
    [Status] nvarchar(50) NOT NULL,
    [StatusDetail] nvarchar(50) NOT NULL,
    [PaymentTypeId] nvarchar(50) NULL,
    [PaymentMethodId] nvarchar(50) NULL,
    [Installments] int NULL,
    [CardLastFourDigits] nvarchar(4) NULL,
    [CardHolderName] nvarchar(100) NULL,
    [AuthorizationCode] nvarchar(50) NULL,
    [FeeAmount] decimal(18,2) NULL,
    [NetReceivedAmount] decimal(18,2) NULL,
    [RefundedAmount] decimal(18,2) NULL,
    [Captured] bit NULL,
    [DateCreatedMp] datetime NULL,
    [DateApproved] datetime NULL,
    [DateLastUpdated] datetime NULL,
    [RawJson] nvarchar(max) NOT NULL,
    [TransactionDetails] nvarchar(max) NULL,
    [CreatedBy] VARCHAR(256) NOT NULL DEFAULT 'System',
    [UpdatedBy] VARCHAR(256) NULL,
    [CreatedDate] datetime2 NOT NULL DEFAULT (GETDATE()),
    [UpdatedDate] datetime2 NULL,
    CONSTRAINT [PK_Payment] PRIMARY KEY ([PaymentId])
);
GO

UPDATE [Role] SET [CreatedDate] = '2025-05-22T02:32:28.2387920Z'
WHERE [RoleId] = 1;
SELECT @@ROWCOUNT;

GO

UPDATE [Role] SET [CreatedDate] = '2025-05-22T02:32:28.2387922Z'
WHERE [RoleId] = 2;
SELECT @@ROWCOUNT;

GO

CREATE INDEX [IX_Payment_Email] ON [Payment] ([Email]);
GO

CREATE INDEX [IX_Payment_ExternalReference] ON [Payment] ([ExternalReference]);
GO

CREATE INDEX [IX_Payment_PaymentMpId] ON [Payment] ([PaymentMpId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250522023228_AddMpPayment', N'8.0.14');
GO

COMMIT;
GO