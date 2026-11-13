using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BF2WebAdmin.Data.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class InitialSqlServer : Migration
    {
        /// <summary>
        /// The schema of WebAdmin's SQL Server databases before EF Core (the old Schema.sql and its startup updates), as
        /// idempotent statements: a new database gets all of it, an existing one only what it lacks (the export cursor, its
        /// numbers for older matches, ServerBan, the foreign key indexes) and keeps its data and its own JoinHistory.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var batch in Batches)
                migrationBuilder.Sql(batch);
        }

        private static readonly string[] Batches =
        {
            @"IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[MapMod]') AND type = N'U')
CREATE TABLE [dbo].[MapMod](
	[Id] [uniqueidentifier] NOT NULL,
	[Name] [nvarchar](50) NOT NULL,
	[CreatedBy] [nvarchar](100) NOT NULL,
	[CreatedDate] [datetime2](7) NOT NULL,
	[EditedDate] [datetime2](7) NOT NULL,
	CONSTRAINT [PK_MapMod] PRIMARY KEY CLUSTERED ([Id])
);",

            @"IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[MapModObject]') AND type = N'U')
CREATE TABLE [dbo].[MapModObject](
	[Id] [uniqueidentifier] NOT NULL,
	[MapModId] [uniqueidentifier] NOT NULL,
	[TemplateName] [nvarchar](100) NOT NULL,
	[Position] [nvarchar](50) NOT NULL,
	[Rotation] [nvarchar](50) NOT NULL,
	CONSTRAINT [PK_MapModObject] PRIMARY KEY CLUSTERED ([Id])
);",

            @"IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Match]') AND type = N'U')
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
	CONSTRAINT [PK_Match] PRIMARY KEY CLUSTERED ([Id])
);",

            @"IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[MatchRound]') AND type = N'U')
CREATE TABLE [dbo].[MatchRound](
	[Id] [uniqueidentifier] NOT NULL,
	[MatchId] [uniqueidentifier] NOT NULL,
	[WinningTeamId] [int] NOT NULL,
	[PositionTrackerInterval] [decimal](10, 5) NOT NULL,
	[RoundStart] [datetime2](3) NOT NULL,
	[RoundEnd] [datetime2](3) NOT NULL,
	CONSTRAINT [PK_MatchRound] PRIMARY KEY CLUSTERED ([Id])
);",

            @"IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[MatchRoundPlayer]') AND type = N'U')
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
	CONSTRAINT [PK_MatchRoundPlayer] PRIMARY KEY CLUSTERED ([RoundId], [PlayerHash])
);",

            @"IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Server]') AND type = N'U')
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
	CONSTRAINT [PK_Server] PRIMARY KEY CLUSTERED ([ServerId])
);",

            // The admin channel (2026-10): the per-server key and the test-server flag
            @"IF COL_LENGTH(N'[dbo].[Server]', N'AdminKey') IS NULL
	ALTER TABLE [dbo].[Server] ADD [AdminKey] [nvarchar](128) NULL;",

            @"IF COL_LENGTH(N'[dbo].[Server]', N'IsTestServer') IS NULL
	ALTER TABLE [dbo].[Server] ADD [IsTestServer] [bit] NOT NULL CONSTRAINT [DF_Server_IsTestServer] DEFAULT 0;",

            @"IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ServerModule]') AND type = N'U')
CREATE TABLE [dbo].[ServerModule](
	[ServerGroup] [nvarchar](50) NOT NULL,
	[Module] [nvarchar](50) NOT NULL,
	[IsEnabled] [bit] NOT NULL,
	CONSTRAINT [PK_ServerModule] PRIMARY KEY CLUSTERED ([ServerGroup], [Module])
);",

            @"IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ServerPlayerAuth]') AND type = N'U')
CREATE TABLE [dbo].[ServerPlayerAuth](
	[ServerGroup] [nvarchar](50) NOT NULL,
	[PlayerHash] [nvarchar](50) NOT NULL,
	[AuthLevel] [int] NOT NULL,
	CONSTRAINT [PK_ServerPlayerAuth] PRIMARY KEY CLUSTERED ([ServerGroup], [PlayerHash])
);",

            // The player joins; a database that already has the table keeps it as it is
            @"IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[JoinHistory]') AND type = N'U')
CREATE TABLE [dbo].[JoinHistory](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](100) NOT NULL,
	[Guid] [nvarchar](32) NOT NULL,
	[IpAddress] [nvarchar](32) NOT NULL,
	[ServerIpAddress] [nvarchar](32) NOT NULL,
	[JoinDate] [datetime2](7) NOT NULL,
	[IsLikelySpoof] [bit] NOT NULL CONSTRAINT [DF_JoinHistory_IsLikelySpoof] DEFAULT 0,
	CONSTRAINT [PK_JoinHistory] PRIMARY KEY CLUSTERED ([Id])
);",

            @"IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ServerBan]') AND type = N'U')
CREATE TABLE [dbo].[ServerBan](
	[ServerId] [nvarchar](50) NOT NULL,
	[Identifier] [nvarchar](64) NOT NULL,
	[ByCdKey] [bit] NOT NULL,
	[Name] [nvarchar](100) NULL,
	[BannedBy] [nvarchar](100) NULL,
	[Reason] [nvarchar](200) NULL,
	[BannedAt] [datetime2](7) NOT NULL,
	CONSTRAINT [PK_ServerBan] PRIMARY KEY CLUSTERED ([ServerId], [Identifier])
);",

            // The export cursor (2026-11): a number from MatchChangeSeq on every write of a match or its rounds
            @"IF COL_LENGTH(N'[dbo].[Match]', N'ChangeSeq') IS NULL
	ALTER TABLE [dbo].[Match] ADD [ChangeSeq] [bigint] NULL;",

            @"IF NOT EXISTS (SELECT * FROM sys.sequences WHERE object_id = OBJECT_ID(N'[dbo].[MatchChangeSeq]'))
	CREATE SEQUENCE [dbo].[MatchChangeSeq] AS [bigint] START WITH 1 INCREMENT BY 1;",

            // Existing matches get numbers in start order, after any number already given; the sequence then continues above all of them
            @"IF EXISTS (SELECT * FROM [dbo].[Match] WHERE [ChangeSeq] IS NULL)
BEGIN
	DECLARE @Base bigint = (SELECT ISNULL(MAX([ChangeSeq]), 0) FROM [dbo].[Match]);
	WITH Numbered AS (SELECT [ChangeSeq], ROW_NUMBER() OVER (ORDER BY [MatchStart], [Id]) AS [RowNumber] FROM [dbo].[Match] WHERE [ChangeSeq] IS NULL)
	UPDATE Numbered SET [ChangeSeq] = @Base + [RowNumber];
	DECLARE @Next bigint = (SELECT MAX([ChangeSeq]) + 1 FROM [dbo].[Match]);
	DECLARE @Current bigint = (SELECT CAST([current_value] AS bigint) FROM sys.sequences WHERE object_id = OBJECT_ID(N'[dbo].[MatchChangeSeq]'));
	IF @Current >= @Next SET @Next = @Current + 1;
	DECLARE @Restart nvarchar(200) = N'ALTER SEQUENCE [dbo].[MatchChangeSeq] RESTART WITH ' + CAST(@Next AS nvarchar(20));
	EXEC (@Restart);
END",

            @"IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[Match]') AND name = N'IX_Match_ChangeSeq')
	CREATE UNIQUE NONCLUSTERED INDEX [IX_Match_ChangeSeq] ON [dbo].[Match] ([ChangeSeq]) WHERE [ChangeSeq] IS NOT NULL;",

            @"IF OBJECT_ID(N'[dbo].[FK_MapModObject_MapMod]', N'F') IS NULL
	ALTER TABLE [dbo].[MapModObject] WITH CHECK ADD CONSTRAINT [FK_MapModObject_MapMod] FOREIGN KEY ([MapModId]) REFERENCES [dbo].[MapMod] ([Id]);",

            @"IF OBJECT_ID(N'[dbo].[FK_MatchRound_Match]', N'F') IS NULL
	ALTER TABLE [dbo].[MatchRound] WITH CHECK ADD CONSTRAINT [FK_MatchRound_Match] FOREIGN KEY ([MatchId]) REFERENCES [dbo].[Match] ([Id]);",

            @"IF OBJECT_ID(N'[dbo].[FK_MatchRoundPlayer_Match]', N'F') IS NULL
	ALTER TABLE [dbo].[MatchRoundPlayer] WITH CHECK ADD CONSTRAINT [FK_MatchRoundPlayer_Match] FOREIGN KEY ([MatchId]) REFERENCES [dbo].[Match] ([Id]);",

            @"IF OBJECT_ID(N'[dbo].[FK_MatchRoundPlayer_MatchRound]', N'F') IS NULL
	ALTER TABLE [dbo].[MatchRoundPlayer] WITH CHECK ADD CONSTRAINT [FK_MatchRoundPlayer_MatchRound] FOREIGN KEY ([RoundId]) REFERENCES [dbo].[MatchRound] ([Id]);",

            // The indexes of the foreign keys, new with EF Core
            @"IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[MapModObject]') AND name = N'IX_MapModObject_MapModId')
	CREATE NONCLUSTERED INDEX [IX_MapModObject_MapModId] ON [dbo].[MapModObject] ([MapModId]);",

            @"IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[MatchRound]') AND name = N'IX_MatchRound_MatchId')
	CREATE NONCLUSTERED INDEX [IX_MatchRound_MatchId] ON [dbo].[MatchRound] ([MatchId]);",

            @"IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[MatchRoundPlayer]') AND name = N'IX_MatchRoundPlayer_MatchId')
	CREATE NONCLUSTERED INDEX [IX_MatchRoundPlayer_MatchId] ON [dbo].[MatchRoundPlayer] ([MatchId]);",
        };

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JoinHistory",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "MapModObject",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "MatchRoundPlayer",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Server",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ServerBan",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ServerModule",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ServerPlayerAuth",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "MapMod",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "MatchRound",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Match",
                schema: "dbo");

            migrationBuilder.DropSequence(
                name: "MatchChangeSeq",
                schema: "dbo");
        }
    }
}
