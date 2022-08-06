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
 CONSTRAINT [PK_Server] PRIMARY KEY CLUSTERED 
(
	[ServerId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
END
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
