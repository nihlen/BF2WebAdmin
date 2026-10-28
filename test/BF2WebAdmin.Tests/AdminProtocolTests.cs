using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Buffers.Binary;
using BF2WebAdmin.Server.AdminChannel;
using Xunit;

namespace BF2WebAdmin.Tests;

/// <summary>
/// The admin channel codec against the golden frames shared with the C++ module
/// (openbf2-server-ext tests/fixtures/admin-v1-golden.txt at cbf435c, written by
/// tools/adminclient/adminclient.py golden_samples). Every frame is encoded from the same sample
/// values and compared byte for byte, then decoded and re-encoded.
/// </summary>
public class AdminProtocolTests
{
    private static readonly Dictionary<string, (ushort Type, byte[] Frame)> Golden = LoadGolden();

    private static Dictionary<string, (ushort, byte[])> LoadGolden()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "admin-v1-golden.txt");
        return File.ReadAllLines(path)
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .Select(l => l.TrimEnd('\r').Split(' '))
            .ToDictionary(p => p[0], p => (Convert.ToUInt16(p[1], 16), Convert.FromHexString(p[2])));
    }

    // The sample values of adminclient.py golden_samples().
    private static readonly AdminPlayerState PState = new(2, 3, 2, 5, 48, 1819, 29328, 1,
        new Vec3(1.5f, 100.25f, -3.0f), new Vec3(90.0f, -5.0f, 2.5f), new Vec3(10.0f, 0.5f, -1.0f), 0.75f, 3, 245);
    private static readonly AdminPlayerScore Score = new(12, 4, 5, 2, 1, 7);
    private static readonly AdminPlayerInfo PInfo = new(3, "§netsky", AdminPlayerInfo.FlagConnected | AdminPlayerInfo.FlagIdentity, 43001234,
        "10.0.0.7", "0123456789abcdef0123456789abcdef", Score, PState);
    private static readonly AdminVehicleState Vehicle = new(1820, 31000, "ahe_ah1z", 1, 0, new Vec3(4, 50, 6), new Vec3(45, 1, -1),
        new Vec3(0, -0.5f, 3), 0.9f, 2, 1);
    private static readonly AdminControlPointState Cp = new(400, 0, "cpname_dalian_harbour", 2, 2, 0.5f, 1, new Vec3(1, 2, 3));
    private static readonly AdminProjectileState Proj = new(0x10000 + 77, "agm114_hellfire_tv", 254, 1, 1, 1820, new Vec3(5, 6, 7),
        new Vec3(10, 20, 30), new Vec3(100, 0, -1), 9.5f);
    private static readonly AdminGameState Game = new(1, 3, 12.5f, 150, 0, 61.0f, 1800.0f, 2, 3);
    private static readonly Vec3 V123 = new(1, 2, 3), V456 = new(4, 5, 6);

    private static byte[] Range(int start, int count) => Enumerable.Range(start, count).Select(i => (byte)i).ToArray();

    public static IEnumerable<object[]> Samples() => SampleMessages().Select(kv => new object[] { kv.Key });

    private static Dictionary<string, AdminMessage> SampleMessages() => new()
    {
        ["hello"] = new Hello(1, "127.0.0.1:16573", Range(0, 32), "libserverext admin 1"),
        ["challenge"] = new Challenge(Range(0xC0, 32), Range(0x40, 32)),
        ["auth"] = new Auth(Range(0x10, 32)),
        ["welcome"] = new Welcome(1, 3, 900, 30.0f, "serverext admin", 16573, 29905, 16, 3, "dalian_2_v_2", "gpm_cq", 16, 0,
            [new MapListEntry("dalian_2_v_2", "gpm_cq", 16), new MapListEntry("daqing_2_v_2", "gpm_cq", 16)], "CH", "US", Game),
        ["snapshot"] = new Snapshot(17, 42, 1000, 33.5, Game, [PInfo], [Vehicle], [Cp], [Proj]),
        ["player_stream"] = new PlayerStream(5, 1.0, [(7, PState)]),
        ["vehicle_stream"] = new VehicleStream(5, 1.0, [Vehicle]),
        ["projectile_stream"] = new ProjectileStream(5, 1.0, [Proj]),
        ["game_stream"] = new GameStream(5, 1.0, Game),
        ["ev_game_state"] = new EvGameState(1, 10, Game, 3),
        ["ev_round_start"] = new EvRoundStart(2, 10, "dalian_2_v_2", "gpm_cq", 100, 90),
        ["ev_round_end"] = new EvRoundEnd(3, 10, 100, 90, 1, 2),
        ["ev_player_connect"] = new EvPlayerConnect(4, 10, PInfo),
        ["ev_player_disconnect"] = new EvPlayerDisconnect(5, 10, 3, 2, 21),
        ["ev_spawn"] = new EvSpawn(6, 10, 3, 2, 1800, 5000, "us_heavy_soldier", V123, V456),
        ["ev_death"] = new EvDeath(7, 10, 3, 2, 1800, "us_heavy_soldier", V123),
        ["ev_revive"] = new EvRevive(8, 10, 3, -1),
        ["ev_team_change"] = new EvTeamChange(9, 10, 255, 2, 1, 1),
        ["ev_score_change"] = new EvScoreChange(10, 10, 3, Score),
        ["ev_kill"] = new EvKill(11, 10, 3, 255, KillKind.Kill, "ahe_z10_S8Launcher", 2, 1800, "us_heavy_soldier", V123, 1, 1819, "ahe_z10",
            V456, [(254, 2)]),
        ["ev_damage"] = new EvDamage(12, 10, 3, 1800, 5000, "us_heavy_soldier", 255, "w", "p", 10, 100, 90, 100, V123, 0),
        ["ev_enter_vehicle"] = new EvVehicle(AdminMessageType.EvEnterVehicle, 13, 10, 255, 0, 0, 1819, 29328, "ahe_z10", "ahe_z10"),
        ["ev_exit_vehicle"] = new EvVehicle(AdminMessageType.EvExitVehicle, 14, 10, 255, 0, 0, 1819, 29328, "ahe_z10", "ahe_z10"),
        ["ev_vehicle_destroyed"] = new EvVehicleDestroyed(15, 10, 1819, 29328, "ahe_z10", -1, V123),
        ["ev_control_point"] = new EvControlPoint(16, 10, 1, Cp),
        ["ev_chat"] = new EvChat(17, 10, -1, 0, 12, "ServerMessage", 0, 0, "rdy"),
        ["ev_projectile_fired"] = new EvProjectileFired(18, 10, Proj),
        ["ev_projectile_hit"] = new EvProjectileHit(19, 10, 0x10000 + 77, "agm114_hellfire_tv", 254, 3, 1820, new Vec3(5, 6, 7)),
        ["ev_kick"] = new EvKick(20, 10, 3, "x"),
        ["ev_ban"] = new EvBan(21, 10, 3, "x", 1, 0),
        ["ev_bot"] = new EvBot(22, 10, 255, 1, 1, 2),
        ["aim_stream"] = new AimStream(5, 1.0, [new AdminAimState(7, AdminAimState.FlagCamera | AdminAimState.FlagWeapon,
            new Vec3(1.5f, 101.5f, -3.0f), new Vec3(90.0f, -10.0f, 0.0f), new Vec3(1.5f, 99.0f, -1.0f), new Vec3(85.0f, -12.5f, 2.5f))]),
        ["ev_weapon_fire"] = new EvWeaponFire(23, 10, 255, 1819, "ahe_z10_30mm", "ahe_z10_30mm_projectile", 3, V123,
            new Vec3(400.0f, -5.0f, 10.0f)),
        ["ev_weapons"] = new EvWeapons(24, 10, 255, [new AdminWeaponSlot(0, "ahe_z10_30mm"), new AdminWeaponSlot(1, "ahe_z10_FlareLauncher")]),
        ["ev_projectile_collision"] = new EvProjectileCollision(25, 10, 1852, "agm114_hellfire_tv", 254,
            EvProjectileCollision.KindObject, EvProjectileCollision.FlagRewound, 1799, 29312, "ahe_ah1z", "agm114_hellfiredummy", -1,
            new Vec3(210.5f, 199.25f, 112.0f), new Vec3(1.0f, 0.0f, 0.0f), new Vec3(-30.0f, 2.5f, 120.0f), 56, 0.25f,
            new Vec3(211.5f, 199.0f, 112.25f), new Vec3(180.0f, 2.0f, 25.5f), new Vec3(206.0f, 199.5f, 110.0f), new Vec3(180.0f, 2.0f, 25.5f)),
        ["ev_projectile_trace"] = new EvProjectileTrace(26, 10, 1852, "agm114_hellfire_tv", 254, 5000, EvProjectileTrace.FlagCollided,
            [new AdminTraceTarget(1799, "ahe_ah1z")],
            [.. Enumerable.Range(0, 2).Select(k => new AdminTraceSample(new Vec3(100.0f + k, 200.0f, 50.0f + 4 * k), new Vec3(0.0f, -1.0f, 125.0f), 0.25f,
                [new AdminTracePose(new Vec3(210.0f - k, 199.0f, 112.0f), new Vec3(180.0f, 2.0f, 25.5f), new Vec3(217.5f - k, 199.5f, 111.0f),
                    new Vec3(180.0f, 1.5f, 25.0f))]))]),
        ["ok"] = new OkReply(9),
        ["console_result"] = new ConsoleResult(10, 1, ["a", "", "§"]),
        ["object_list"] = new ObjectList(11, 1, [new ObjectListEntry(5, 0, "fence", V123, V456, -1.0f, 0)]),
        ["object_spawned"] = new ObjectSpawned(12, 300, 0),
        ["ping"] = new Ping(13, 0x0102030405060708),
        ["pong"] = new Pong(13, 0x0102030405060708),
        ["error"] = new ErrorMessage(14, AdminErrorCode.Disabled, "off"),
        ["cmd_request_snapshot"] = new RequestSnapshot(1),
        ["cmd_subscribe"] = new Subscribe(3, AdminStream.Projectiles, 5, ["agm114_hellfire_tv"]),
        ["cmd_unsubscribe"] = new Unsubscribe(5, AdminStream.Vehicles),
        ["cmd_console"] = new CmdConsole(6, "sv.serverName"),
        ["cmd_say"] = new CmdSay(7, CmdSay.TargetPlayer, 3, "§C1001hi"),
        ["cmd_teleport"] = new CmdTeleport(8, ObjectRef.Player(3), CmdTeleport.FlagPosition | CmdTeleport.FlagRotation, new Vec3(1, 1, 1), new Vec3(1, 1, 1)),
        ["cmd_set_health"] = new CmdSetHealth(9, ObjectRef.Networkable(1819), CmdSetHealth.FlagFraction, 0.5f),
        ["cmd_kill_player"] = new CmdKillPlayer(10, 3, CmdKillPlayer.FlagDestroyVehicle),
        ["cmd_set_team"] = new CmdSetTeam(11, 3, 2, CmdSetTeam.FlagKill),
        ["cmd_set_score"] = new CmdSetScore(12, 3, 0x1F, 0, -1, -2, -3, -4),
        ["cmd_rank"] = new CmdRank(13, 3, 21, 1),
        ["cmd_medal"] = new CmdMedal(14, 3, 2051907, 1),
        ["cmd_game_event"] = new CmdGameEvent(15, 3, 0, 0),
        ["cmd_hud_event"] = new CmdHudEvent(16, 3, 78, -1),
        ["cmd_kick"] = new CmdKick(17, 3, ""),
        ["cmd_ban"] = new CmdBan(18, 3, CmdBan.ByCdKey, CmdBan.DurationSeconds, 3600, "tk"),
        ["cmd_map"] = new CmdMap(19, CmdMap.ActionByName, 0, "dalian_2_v_2", "gpm_cq", 16),
        ["cmd_list_objects"] = new CmdListObjects(20, "fence_wood", 100),
        ["cmd_set_object_active"] = new CmdSetObjectActive(21, ObjectRef.Console(29328)),
        ["cmd_spawn_object"] = new CmdSpawnObject(22, "ahe_ah1z", new Vec3(2, 2, 2), new Vec3(2, 2, 2), 1),
        ["cmd_delete_object"] = new CmdDeleteObject(23, ObjectRef.Networkable(5)),
        ["cmd_ping"] = new Ping(24, 99),
        ["cmd_subscribe_fire"] = new Subscribe(25, AdminStream.WeaponFire, 0, ["ahe_z10_FlareLauncher"]),
        ["cmd_subscribe_replay"] = new Subscribe(26, AdminStream.Replay, 0, ["agm114_hellfire_tv"]),
    };

    [Fact]
    public void GoldenFileHasEveryFrame()
    {
        Assert.Equal(67, Golden.Count);
        Assert.Equal(Golden.Keys.OrderBy(k => k), SampleMessages().Keys.OrderBy(k => k));
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void EncodesGoldenFrame(string name)
    {
        var (type, frame) = Golden[name];
        var message = SampleMessages()[name];
        Assert.Equal(type, (ushort)message.Type);
        Assert.Equal(Convert.ToHexString(frame), Convert.ToHexString(AdminProtocol.Encode(message)));
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void DecodesGoldenFrame(string name)
    {
        var (type, frame) = Golden[name];
        var decoder = new AdminFrameDecoder();
        decoder.Append(frame);
        Assert.True(decoder.TryRead(out var t, out var payload));
        Assert.Equal(type, (ushort)t);
        var message = AdminProtocol.Decode(t, payload);
        Assert.Equal(Convert.ToHexString(frame), Convert.ToHexString(AdminProtocol.Encode(message)));
        Assert.False(decoder.TryRead(out _, out _));
    }

    [Fact]
    public void DecodesFieldValues()
    {
        var snapshot = (Snapshot)Decode("snapshot");
        Assert.Equal(42u, snapshot.LastSeq);
        var p = Assert.Single(snapshot.Players);
        Assert.Equal("§netsky", p.Name);
        Assert.Equal("0123456789abcdef0123456789abcdef", p.CdKeyHash);
        Assert.Equal(new Vec3(1.5f, 100.25f, -3.0f), p.State.Position);
        Assert.Equal(29328u, p.State.RootConsoleId);
        Assert.Equal("ahe_ah1z", Assert.Single(snapshot.Vehicles).Template);
        Assert.Equal((short)254, Assert.Single(snapshot.Projectiles).Owner);

        var welcome = (Welcome)Decode("welcome");
        Assert.Equal(["dalian_2_v_2", "daqing_2_v_2"], welcome.Maps.Select(m => m.Name));
        Assert.Equal(("CH", "US"), (welcome.Team1, welcome.Team2));

        var kill = (EvKill)Decode("ev_kill");
        Assert.Equal(KillKind.Kill, kill.Kind);
        Assert.Equal((short)255, kill.Attacker);
        Assert.Equal([((byte)254, 2)], kill.Assists);

        Assert.Equal(["a", "", "§"], ((ConsoleResult)Decode("console_result")).Lines);
        Assert.Equal((short)-1, ((EvChat)Decode("ev_chat")).Sender);
    }

    private static AdminMessage Decode(string name)
    {
        var (type, frame) = Golden[name];
        return AdminProtocol.Decode((AdminMessageType)type, frame.AsSpan(AdminProtocol.HeaderSize));
    }

    [Fact]
    public void FrameDecoderHandlesBytesOneByOne()
    {
        var all = Golden.Values.SelectMany(g => g.Frame).ToArray();
        var decoder = new AdminFrameDecoder();
        var count = 0;
        foreach (var b in all)
        {
            decoder.Append([b]);
            while (decoder.TryRead(out var type, out var payload))
            {
                AdminProtocol.Decode(type, payload);
                count++;
            }
        }

        Assert.Equal(Golden.Count, count);
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void RejectsShortAndLongPayloads(string name)
    {
        var (type, frame) = Golden[name];
        var payload = frame.AsSpan(AdminProtocol.HeaderSize).ToArray();
        for (var n = 0; n < payload.Length; n++)
        {
            var e = Assert.Throws<AdminProtocolException>(() => AdminProtocol.Decode((AdminMessageType)type, payload.AsSpan(0, n)));
            Assert.Equal(AdminErrorCode.Malformed, e.Code);
        }

        var longer = payload.Append((byte)0).ToArray();
        Assert.Equal(AdminErrorCode.Malformed, Assert.Throws<AdminProtocolException>(() => AdminProtocol.Decode((AdminMessageType)type, longer)).Code);
    }

    [Fact]
    public void RejectsOversizeFrame()
    {
        var header = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(header, AdminProtocol.MaxInboundPayload + 1);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(4), (ushort)AdminMessageType.Snapshot);
        var decoder = new AdminFrameDecoder();
        decoder.Append(header);
        Assert.Equal(AdminErrorCode.Malformed, Assert.Throws<AdminProtocolException>(() => decoder.TryRead(out _, out _)).Code);

        // Exactly the limit is a valid length (it waits for the payload).
        BinaryPrimitives.WriteUInt32LittleEndian(header, AdminProtocol.MaxInboundPayload);
        decoder = new AdminFrameDecoder();
        decoder.Append(header);
        Assert.False(decoder.TryRead(out _, out _));
    }

    [Fact]
    public void RejectsNonzeroReserved()
    {
        var frame = Golden["ok"].Frame.ToArray();
        frame[6] = 1;
        var decoder = new AdminFrameDecoder();
        decoder.Append(frame);
        Assert.Equal(AdminErrorCode.Malformed, Assert.Throws<AdminProtocolException>(() => decoder.TryRead(out _, out _)).Code);
    }

    [Fact]
    public void RejectsUnknownType()
    {
        Assert.Equal(AdminErrorCode.UnknownType, Assert.Throws<AdminProtocolException>(() => AdminProtocol.Decode((AdminMessageType)0x121B, [])).Code);
        Assert.Equal(AdminErrorCode.UnknownType, Assert.Throws<AdminProtocolException>(() => AdminProtocol.Decode((AdminMessageType)0x0001, [])).Code);
    }

    [Theory]
    [InlineData(new byte[] { 0xC3 })] // truncated sequence
    [InlineData(new byte[] { 0xA7 })] // a bare Windows-1252 section sign
    [InlineData(new byte[] { 0xC0, 0x80 })] // overlong NUL
    [InlineData(new byte[] { 0xED, 0xA0, 0x80 })] // UTF-16 surrogate
    [InlineData(new byte[] { 0xF4, 0x90, 0x80, 0x80 })] // beyond U+10FFFF
    public void RejectsInvalidUtf8(byte[] text)
    {
        var w = new AdminWriter().U32(1).U16((ushort)text.Length).Bytes(text);
        var e = Assert.Throws<AdminProtocolException>(() => AdminProtocol.Decode(AdminMessageType.ConsoleResult, new AdminWriter().U32(1).Byte(0).U16(1).U16((ushort)text.Length).Bytes(text).ToArray()));
        Assert.Equal(AdminErrorCode.Malformed, e.Code);
        Assert.Throws<AdminProtocolException>(() => AdminProtocol.Decode(AdminMessageType.CmdConsole, w.ToArray()));
    }

    [Fact]
    public void SectionSignIsUtf8OnTheWire()
    {
        var frame = AdminProtocol.Encode(new CmdSay(1, CmdSay.TargetAll, 0, "§C1001x"));
        Assert.Equal([0xC2, 0xA7], frame.AsSpan(AdminProtocol.HeaderSize + 4 + 2 + 2, 2).ToArray());
    }

    [Fact]
    public void ProofTranscriptLayout()
    {
        var nm = Range(0, 32);
        var nw = Enumerable.Range(0, 32).Select(i => (byte)(0xFF - i)).ToArray();
        var t = AdminProtocol.ProofTranscript(AdminProtocol.ServerProofLabel, "ab", nm, nw);
        var label = AdminProtocol.ServerProofLabel;
        Assert.Equal(label.Length + 1 + 2 + 2 + 64, t.Length);
        Assert.Equal(label, System.Text.Encoding.ASCII.GetString(t, 0, label.Length));
        Assert.Equal(0, t[label.Length]);
        Assert.Equal(2, t[label.Length + 1]);
        Assert.Equal(0, t[label.Length + 2]);
        Assert.Equal((byte)'a', t[label.Length + 3]);
        Assert.Equal(0, t[label.Length + 5]);
        Assert.Equal(0xE0, t[^1]);
    }

    [Fact]
    public void ProofMatchesPythonReference()
    {
        // python3 -c "import hmac,hashlib; k=bytes(range(16)); t=b'OBF2-ADMIN-1 webadmin\0'+(15).to_bytes(2,'little')+b'127.0.0.1:16569'+bytes(range(32))+bytes(range(0x40,0x60)); print(hmac.new(k,t,hashlib.sha256).hexdigest())"
        var key = Range(0, 16);
        var pw = AdminProtocol.Proof(key, AdminProtocol.WebAdminProofLabel, "127.0.0.1:16569", Range(0, 32), Range(0x40, 32));
        var transcript = AdminProtocol.ProofTranscript(AdminProtocol.WebAdminProofLabel, "127.0.0.1:16569", Range(0, 32), Range(0x40, 32));
        Assert.Equal(System.Security.Cryptography.HMACSHA256.HashData(key, transcript), pw);
        Assert.Equal(ExpectedWebAdminProof, Convert.ToHexString(pw).ToLowerInvariant());
        Assert.NotEqual(pw, AdminProtocol.Proof(key, AdminProtocol.ServerProofLabel, "127.0.0.1:16569", Range(0, 32), Range(0x40, 32)));
    }

    internal const string ExpectedWebAdminProof = "68d65ad691c252021732b3810d995ac40dbc2f293011cc2a94d5bc28bcc9e196";
}
