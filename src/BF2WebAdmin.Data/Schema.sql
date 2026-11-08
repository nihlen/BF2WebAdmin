--USE [bf2]
GO
/****** Object:  Table [dbo].[MapMod]    Script Date: 2022-08-06 02:21:05 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[MapMod]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[MapMod](
	[Id] [uniqueidentifier] NOT NULL,
	[Name] [nvarchar](50) NOT NULL,
	[CreatedBy] [nvarchar](100) NOT NULL,
	[CreatedDate] [datetime2](7) NOT NULL,
	[EditedDate] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_MapMod] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
END
GO
/****** Object:  Table [dbo].[MapModObject]    Script Date: 2022-08-06 02:21:05 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[MapModObject]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[MapModObject](
	[Id] [uniqueidentifier] NOT NULL,
	[MapModId] [uniqueidentifier] NOT NULL,
	[TemplateName] [nvarchar](100) NOT NULL,
	[Position] [nvarchar](50) NOT NULL,
	[Rotation] [nvarchar](50) NOT NULL,
 CONSTRAINT [PK_MapModObject] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
END
GO
/****** Object:  Table [dbo].[Match]    Script Date: 2022-08-06 02:21:05 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Match]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[Match](
	[Id] [uniqueidentifier] NOT NULL,
	[ServerId] [nvarchar](20) NOT NULL,
	[ServerName] [nvarchar](100) NOT NULL,
	[Map] [nvarchar](20) NOT NULL,
	[Type] [nvarchar](20) NOT NULL,
	[TeamAHash] [nvarchar](100) NOT NULL,
	[TeamAName] [nvarchar](100) NOT NULL,
	[TeamAScore] [int] NOT NULL,
	[TeamBHash] [nvarchar](100) NOT NULL,
	[TeamBName] [nvarchar](100) NOT NULL,
	[TeamBScore] [int] NOT NULL,
	[MatchStart] [datetime2](3) NOT NULL,
	[MatchEnd] [datetime2](3) NULL,
 CONSTRAINT [PK_Match] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
END
GO
/****** Object:  Table [dbo].[MatchRound]    Script Date: 2022-08-06 02:21:05 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[MatchRound]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[MatchRound](
	[Id] [uniqueidentifier] NOT NULL,
	[MatchId] [uniqueidentifier] NOT NULL,
	[WinningTeamId] [int] NOT NULL,
	[PositionTrackerInterval] [decimal](10, 5) NOT NULL,
	[RoundStart] [datetime2](3) NOT NULL,
	[RoundEnd] [datetime2](3) NOT NULL,
 CONSTRAINT [PK_MatchRound] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
END
GO
/****** Object:  Table [dbo].[MatchRoundPlayer]    Script Date: 2022-08-06 02:21:05 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[MatchRoundPlayer]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[MatchRoundPlayer](
	[RoundId] [uniqueidentifier] NOT NULL,
	[PlayerHash] [nvarchar](50) NOT NULL,
	[MatchId] [uniqueidentifier] NOT NULL,
	[PlayerName] [nvarchar](50) NOT NULL,
	[TeamId] [int] NOT NULL,
	[SubVehicle] [nvarchar](50) NULL,
	[SaidGo] [bit] NOT NULL,
	[StartPosition] [nvarchar](32) NULL,
	[DeathPosition] [nvarchar](32) NULL,
	[DeathTime] [datetime2](3) NULL,
	[KillerHash] [nvarchar](50) NULL,
	[KillerWeapon] [nvarchar](50) NULL,
	[KillerPosition] [nvarchar](32) NULL,
	[MovementPathJson] [nvarchar](max) NULL,
	[ProjectilePathsJson] [nvarchar](max) NULL,
 CONSTRAINT [PK_MatchRoundPlayer] PRIMARY KEY CLUSTERED 
(
	[RoundId] ASC,
	[PlayerHash] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
END
GO
/****** Object:  Table [dbo].[Server]    Script Date: 2022-08-06 02:21:05 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Server]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[Server](
	[ServerId] [nvarchar](20) NOT NULL,
	[ServerGroup] [nvarchar](50) NOT NULL,
	[IpAddress] [nvarchar](100) NULL,
	[GamePort] [int] NOT NULL,
	[QueryPort] [int] NOT NULL,
	[RconPort] [int] NOT NULL,
	[RconPassword] [nvarchar](100) NULL,
	[DiscordBotToken] [nvarchar](200) NULL,
	[DiscordAdminChannel] [nvarchar](50) NULL,
	[DiscordNotificationChannel] [nvarchar](50) NULL,
	[DiscordMatchResultChannel] [nvarchar](50) NULL,
	[AdminKey] [nvarchar](128) NULL,
	[IsTestServer] [bit] NOT NULL CONSTRAINT [DF_Server_IsTestServer] DEFAULT 0,
 CONSTRAINT [PK_Server] PRIMARY KEY CLUSTERED 
(
	[ServerId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
END
GO
-- Admin channel (OpenBF2 serverext): the per-server key, and the test-server flag that allows name keys for players without a CD-key hash.
IF COL_LENGTH(N'[dbo].[Server]', N'AdminKey') IS NULL
	ALTER TABLE [dbo].[Server] ADD [AdminKey] [nvarchar](128) NULL;
GO
IF COL_LENGTH(N'[dbo].[Server]', N'IsTestServer') IS NULL
	ALTER TABLE [dbo].[Server] ADD [IsTestServer] [bit] NOT NULL CONSTRAINT [DF_Server_IsTestServer] DEFAULT 0;
GO
-- The export cursor on Match (/api/export/v1/matches): a number from MatchChangeSeq on every write of a match or its
-- rounds. WebAdmin applies these batches itself at startup (SqlServerSchemaUpdates.cs); they are here for a new database.
IF COL_LENGTH(N'[dbo].[Match]', N'ChangeSeq') IS NULL
	ALTER TABLE [dbo].[Match] ADD [ChangeSeq] [bigint] NULL;
GO
IF NOT EXISTS (SELECT * FROM sys.sequences WHERE object_id = OBJECT_ID(N'[dbo].[MatchChangeSeq]'))
	CREATE SEQUENCE [dbo].[MatchChangeSeq] AS [bigint] START WITH 1 INCREMENT BY 1;
GO
IF EXISTS (SELECT * FROM [dbo].[Match] WHERE [ChangeSeq] IS NULL)
BEGIN
	DECLARE @Base bigint = (SELECT ISNULL(MAX([ChangeSeq]), 0) FROM [dbo].[Match]);
	WITH Numbered AS (SELECT [ChangeSeq], ROW_NUMBER() OVER (ORDER BY [MatchStart], [Id]) AS [RowNumber] FROM [dbo].[Match] WHERE [ChangeSeq] IS NULL)
	UPDATE Numbered SET [ChangeSeq] = @Base + [RowNumber];
	DECLARE @Next bigint = (SELECT MAX([ChangeSeq]) + 1 FROM [dbo].[Match]);
	DECLARE @Current bigint = (SELECT CAST([current_value] AS bigint) FROM sys.sequences WHERE object_id = OBJECT_ID(N'[dbo].[MatchChangeSeq]'));
	IF @Current >= @Next SET @Next = @Current + 1;
	DECLARE @Restart nvarchar(200) = N'ALTER SEQUENCE [dbo].[MatchChangeSeq] RESTART WITH ' + CAST(@Next AS nvarchar(20));
	EXEC (@Restart);
END
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Match]') AND name = N'IX_Match_ChangeSeq')
	CREATE UNIQUE NONCLUSTERED INDEX [IX_Match_ChangeSeq] ON [dbo].[Match] ([ChangeSeq]) WHERE [ChangeSeq] IS NOT NULL;
GO
/****** Object:  Table [dbo].[ServerModule]    Script Date: 2022-08-06 02:21:05 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ServerModule]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[ServerModule](
	[ServerGroup] [nvarchar](50) NOT NULL,
	[Module] [nvarchar](50) NOT NULL,
	[IsEnabled] [bit] NOT NULL,
 CONSTRAINT [PK_ServerModule] PRIMARY KEY CLUSTERED 
(
	[ServerGroup] ASC,
	[Module] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
END
GO
/****** Object:  Table [dbo].[ServerPlayerAuth]    Script Date: 2022-08-06 02:21:05 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ServerPlayerAuth]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[ServerPlayerAuth](
	[ServerGroup] [nvarchar](50) NOT NULL,
	[PlayerHash] [nvarchar](50) NOT NULL,
	[AuthLevel] [int] NOT NULL,
 CONSTRAINT [PK_ServerPlayerAuth] PRIMARY KEY CLUSTERED 
(
	[ServerGroup] ASC,
	[PlayerHash] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
END
GO
/****** Object:  Table [dbo].[JoinHistory] ******/
-- The BF2v2 site's player joins (country flags, aliases). WebAdmin inserts a row per human player connect; BF2v2 reads
-- the table and sets IsLikelySpoof. Created here only when missing, so fresh databases have it; an existing table is left as it is.
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[JoinHistory]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[JoinHistory](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](100) NOT NULL,
	[Guid] [nvarchar](32) NOT NULL,
	[IpAddress] [nvarchar](32) NOT NULL,
	[ServerIpAddress] [nvarchar](32) NOT NULL,
	[JoinDate] [datetime2](7) NOT NULL,
	[IsLikelySpoof] [bit] NOT NULL CONSTRAINT [DF_JoinHistory_IsLikelySpoof] DEFAULT 0,
 CONSTRAINT [PK_JoinHistory] PRIMARY KEY CLUSTERED
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
END
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_MapModObject_MapMod]') AND parent_object_id = OBJECT_ID(N'[dbo].[MapModObject]'))
ALTER TABLE [dbo].[MapModObject]  WITH CHECK ADD  CONSTRAINT [FK_MapModObject_MapMod] FOREIGN KEY([MapModId])
REFERENCES [dbo].[MapMod] ([Id])
GO
IF  EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_MapModObject_MapMod]') AND parent_object_id = OBJECT_ID(N'[dbo].[MapModObject]'))
ALTER TABLE [dbo].[MapModObject] CHECK CONSTRAINT [FK_MapModObject_MapMod]
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_MatchRound_Match]') AND parent_object_id = OBJECT_ID(N'[dbo].[MatchRound]'))
ALTER TABLE [dbo].[MatchRound]  WITH CHECK ADD  CONSTRAINT [FK_MatchRound_Match] FOREIGN KEY([MatchId])
REFERENCES [dbo].[Match] ([Id])
GO
IF  EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_MatchRound_Match]') AND parent_object_id = OBJECT_ID(N'[dbo].[MatchRound]'))
ALTER TABLE [dbo].[MatchRound] CHECK CONSTRAINT [FK_MatchRound_Match]
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_MatchRoundPlayer_Match]') AND parent_object_id = OBJECT_ID(N'[dbo].[MatchRoundPlayer]'))
ALTER TABLE [dbo].[MatchRoundPlayer]  WITH CHECK ADD  CONSTRAINT [FK_MatchRoundPlayer_Match] FOREIGN KEY([MatchId])
REFERENCES [dbo].[Match] ([Id])
GO
IF  EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_MatchRoundPlayer_Match]') AND parent_object_id = OBJECT_ID(N'[dbo].[MatchRoundPlayer]'))
ALTER TABLE [dbo].[MatchRoundPlayer] CHECK CONSTRAINT [FK_MatchRoundPlayer_Match]
GO
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_MatchRoundPlayer_MatchRound]') AND parent_object_id = OBJECT_ID(N'[dbo].[MatchRoundPlayer]'))
ALTER TABLE [dbo].[MatchRoundPlayer]  WITH CHECK ADD  CONSTRAINT [FK_MatchRoundPlayer_MatchRound] FOREIGN KEY([RoundId])
REFERENCES [dbo].[MatchRound] ([Id])
GO
IF  EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_MatchRoundPlayer_MatchRound]') AND parent_object_id = OBJECT_ID(N'[dbo].[MatchRoundPlayer]'))
ALTER TABLE [dbo].[MatchRoundPlayer] CHECK CONSTRAINT [FK_MatchRoundPlayer_MatchRound]
GO
