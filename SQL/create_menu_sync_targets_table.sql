-- =============================================
-- Create Menu Sync Target Servers Table
-- =============================================
USE [dev_Restaurant]
GO

-- Check if table exists
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[MenuSyncTargets]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[MenuSyncTargets](
        [Id] [int] IDENTITY(1,1) NOT NULL,
        [ServerName] [nvarchar](255) NOT NULL,
        [ServerIP] [nvarchar](255) NOT NULL,
        [DatabaseName] [nvarchar](255) NOT NULL,
        [Username] [nvarchar](255) NULL,
        [Password] [nvarchar](255) NULL,
        [IsActive] [bit] NOT NULL DEFAULT(1),
        [IsDefault] [bit] NOT NULL DEFAULT(0),
        [Description] [nvarchar](500) NULL,
        [CreatedAt] [datetime] NOT NULL DEFAULT(GETDATE()),
        [UpdatedAt] [datetime] NOT NULL DEFAULT(GETDATE()),
        [LastSyncDate] [datetime] NULL,
        [LastSyncStatus] [nvarchar](50) NULL,
        CONSTRAINT [PK_MenuSyncTargets] PRIMARY KEY CLUSTERED ([Id] ASC)
    )
    
    PRINT 'MenuSyncTargets table created successfully.';
END
ELSE
BEGIN
    PRINT 'MenuSyncTargets table already exists.';
END
GO

-- Create index for better performance
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[MenuSyncTargets]') AND name = N'IX_MenuSyncTargets_IsActive')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_MenuSyncTargets_IsActive]
    ON [dbo].[MenuSyncTargets] ([IsActive])
    INCLUDE ([ServerName], [ServerIP], [DatabaseName])
    
    PRINT 'Index IX_MenuSyncTargets_IsActive created successfully.';
END
GO
