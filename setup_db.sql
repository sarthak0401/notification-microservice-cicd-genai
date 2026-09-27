-- The filtered index UX_Notifications_MessageId requires these session settings.
-- sqlcmd defaults QUOTED_IDENTIFIER to OFF, so set them explicitly.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'NotificationDb')
BEGIN
    CREATE DATABASE NotificationDb;
END;
GO

USE NotificationDb;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'UserDeviceTokens')
BEGIN
    CREATE TABLE UserDeviceTokens
    (
        Id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        UserId UNIQUEIDENTIFIER NOT NULL,
        DeviceId NVARCHAR(255) NOT NULL,
        DeviceToken NVARCHAR(450) NOT NULL,
        DevicePlatform NVARCHAR(50) NULL,
        DeviceModel NVARCHAR(100) NULL,
        IsActive BIT NOT NULL DEFAULT 1,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT PK_UserDeviceTokens PRIMARY KEY (Id)
    );

    CREATE UNIQUE INDEX UX_UserDeviceTokens_User_Token 
    ON UserDeviceTokens(UserId, DeviceToken);
END;

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Notifications')
BEGIN
    CREATE TABLE Notifications
    (
        Id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        MessageId UNIQUEIDENTIFIER NULL,
        RecipientUserId UNIQUEIDENTIFIER NOT NULL,
        ActorUserId UNIQUEIDENTIFIER NOT NULL,
        Type NVARCHAR(100) NOT NULL,
        EntityId UNIQUEIDENTIFIER NOT NULL,
        IsRead BIT NOT NULL DEFAULT 0,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT PK_Notifications PRIMARY KEY (Id)
    );

    CREATE INDEX IX_Notifications_Recipient_CreatedAt 
    ON Notifications(RecipientUserId, CreatedAt DESC);
END;
GO

-- ---------------------------------------------------------------------------
-- Idempotency (applies to databases created before this script was extended).
-- A message may be delivered more than once (retry / redelivery / replay); the
-- filtered unique index guarantees exactly one notification row per message id.
-- ---------------------------------------------------------------------------
IF COL_LENGTH('dbo.Notifications', 'MessageId') IS NULL
BEGIN
    ALTER TABLE Notifications ADD MessageId UNIQUEIDENTIFIER NULL;
END;
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UX_Notifications_MessageId' AND object_id = OBJECT_ID('dbo.Notifications'))
BEGIN
    CREATE UNIQUE INDEX UX_Notifications_MessageId
    ON Notifications(MessageId)
    WHERE MessageId IS NOT NULL;
END;
GO
