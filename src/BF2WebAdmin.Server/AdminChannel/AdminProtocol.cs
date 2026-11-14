using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace BF2WebAdmin.Server.AdminChannel;

// The OpenBF2 serverext admin channel, protocol version 1. The byte layouts are the approved
// "Wire format (admin protocol version 1)" in openbf2-server-ext docs/admin-channel-draft.md.
// Frame: u32 payload length, u16 type, u16 reserved (0), payload. Little-endian. Strings are
// u16 byte length plus UTF-8. The module dials WebAdmin; WebAdmin is the listener.

public enum AdminMessageType : ushort
{
    Hello = 0x1001,
    Challenge = 0x1002,
    Auth = 0x1003,
    Welcome = 0x1004,

    Snapshot = 0x1101,
    RequestSnapshot = 0x1102,
    Subscribe = 0x1103,
    Unsubscribe = 0x1104,
    PlayerStream = 0x1111,
    VehicleStream = 0x1112,
    ProjectileStream = 0x1113,
    GameStream = 0x1114,
    AimStream = 0x1115,

    EvGameState = 0x1201,
    EvRoundStart = 0x1202,
    EvRoundEnd = 0x1203,
    EvPlayerConnect = 0x1204,
    EvPlayerDisconnect = 0x1205,
    EvSpawn = 0x1206,
    EvDeath = 0x1207,
    EvRevive = 0x1208,
    EvTeamChange = 0x1209,
    EvScoreChange = 0x120A,
    EvKill = 0x120B,
    EvDamage = 0x120C,
    EvEnterVehicle = 0x120D,
    EvExitVehicle = 0x120E,
    EvVehicleDestroyed = 0x120F,
    EvControlPoint = 0x1210,
    EvChat = 0x1211,
    EvProjectileFired = 0x1212,
    EvProjectileHit = 0x1213,
    EvKick = 0x1214,
    EvBan = 0x1215,
    EvBot = 0x1216,
    EvWeaponFire = 0x1217,
    EvWeapons = 0x1218,
    EvProjectileCollision = 0x1219,
    EvProjectileTrace = 0x121A,
    EvScriptFinished = 0x121C,
    EvScriptEvent = 0x121D,

    CmdConsole = 0x1301,
    CmdSay = 0x1302,
    CmdTeleport = 0x1303,
    CmdSetHealth = 0x1304,
    CmdKillPlayer = 0x1305,
    CmdSetTeam = 0x1306,
    CmdSetScore = 0x1307,
    CmdRank = 0x1308,
    CmdMedal = 0x1309,
    CmdGameEvent = 0x130A,
    CmdHudEvent = 0x130B,
    CmdKick = 0x130C,
    CmdBan = 0x130D,
    CmdMap = 0x130E,
    CmdListObjects = 0x130F,
    CmdSetObjectActive = 0x1310,
    CmdSpawnObject = 0x1311,
    CmdDeleteObject = 0x1312,
    CmdStartScript = 0x1318,
    CmdStopScript = 0x1319,
    CmdScriptMessage = 0x131A,
    CmdListScripts = 0x131B,
    CmdSeatPreference = 0x131C,

    Ok = 0x1401,
    ConsoleResult = 0x1402,
    ObjectList = 0x1403,
    ObjectSpawned = 0x1404,
    ScriptList = 0x1405,

    Ping = 0x1501,
    Pong = 0x1502,

    Error = 0xFFFF,
}

public enum AdminErrorCode : ushort
{
    VersionMismatch = 1,
    Malformed = 2,
    UnknownType = 3,
    UnknownPlayer = 4,
    Unavailable = 5,
    Refused = 6,
    InvalidValue = 7,
    AuthenticationFailed = 8,
    Disabled = 9,
}

public enum AdminStream : byte
{
    Players = 1,
    Vehicles = 2,
    Projectiles = 3,
    Game = 4,
    Aim = 5,
    Damage = 16,
    ProjectileEvents = 17,
    WeaponFire = 18,
    Weapons = 19,
    /// <summary>The hit/miss replay: ProjectileCollision and ProjectileTrace; needs projectile template filters.</summary>
    Replay = 20,
}

public enum ObjectRefKind : byte
{
    PlayerRoot = 1,
    Networkable = 2,
    Console = 3,
}

public enum KillKind : byte
{
    Kill = 1,
    TeamKill = 2,
    Suicide = 3,
    RoadKill = 4,
    NoAttacker = 5,
}

public static class AdminProtocol
{
    public const ushort Version = 1;
    public const int HeaderSize = 8;
    public const int NonceSize = 32;
    public const int ProofSize = 32;

    /// <summary>Module to WebAdmin payloads (Snapshot, ObjectList and ConsoleResult can be large).</summary>
    public const int MaxInboundPayload = 1024 * 1024;

    /// <summary>WebAdmin to module payloads, as on the bot channel.</summary>
    public const int MaxOutboundPayload = 64 * 1024;

    public const uint NoConsoleId = 0xFFFFFFFF;
    public const byte NoSeat = 255;

    public static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static bool IsEvent(AdminMessageType type) => (ushort)type is >= 0x1201 and <= 0x12FF;

    public static byte[] Encode(AdminMessage message)
    {
        var w = new AdminWriter();
        w.U32(0);
        w.U16((ushort)message.Type);
        w.U16(0);
        message.Write(w);
        var frame = w.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(frame, (uint)(frame.Length - HeaderSize));
        return frame;
    }

    /// <summary>Decodes one payload of the given type. Throws <see cref="AdminProtocolException"/> on anything malformed.</summary>
    public static AdminMessage Decode(AdminMessageType type, ReadOnlySpan<byte> payload)
    {
        var r = new AdminReader(payload);
        AdminMessage m = type switch
        {
            AdminMessageType.Hello => Hello.Read(ref r),
            AdminMessageType.Challenge => Challenge.Read(ref r),
            AdminMessageType.Auth => Auth.Read(ref r),
            AdminMessageType.Welcome => Welcome.Read(ref r),
            AdminMessageType.Snapshot => Snapshot.Read(ref r),
            AdminMessageType.RequestSnapshot => new RequestSnapshot(r.U32()),
            AdminMessageType.Subscribe => Subscribe.Read(ref r),
            AdminMessageType.Unsubscribe => new Unsubscribe(r.U32(), (AdminStream)r.U8()),
            AdminMessageType.PlayerStream => PlayerStream.Read(ref r),
            AdminMessageType.VehicleStream => VehicleStream.Read(ref r),
            AdminMessageType.ProjectileStream => ProjectileStream.Read(ref r),
            AdminMessageType.GameStream => new GameStream(r.U32(), r.F64(), AdminGameState.Read(ref r)),
            AdminMessageType.AimStream => AimStream.Read(ref r),
            AdminMessageType.EvGameState => new EvGameState(r.U32(), r.U32(), AdminGameState.Read(ref r), r.U8()),
            AdminMessageType.EvRoundStart => new EvRoundStart(r.U32(), r.U32(), r.Str(), r.Str(), r.I32(), r.I32()),
            AdminMessageType.EvRoundEnd => new EvRoundEnd(r.U32(), r.U32(), r.I32(), r.I32(), r.U8(), r.U8()),
            AdminMessageType.EvPlayerConnect => new EvPlayerConnect(r.U32(), r.U32(), AdminPlayerInfo.Read(ref r)),
            AdminMessageType.EvPlayerDisconnect => new EvPlayerDisconnect(r.U32(), r.U32(), r.U8(), r.U8(), r.U16()),
            AdminMessageType.EvSpawn => new EvSpawn(r.U32(), r.U32(), r.U8(), r.U8(), r.U32(), r.U32(), r.Str(), r.Vec(), r.Vec()),
            AdminMessageType.EvDeath => new EvDeath(r.U32(), r.U32(), r.U8(), r.U8(), r.U32(), r.Str(), r.Vec()),
            AdminMessageType.EvRevive => new EvRevive(r.U32(), r.U32(), r.U8(), r.I16()),
            AdminMessageType.EvTeamChange => new EvTeamChange(r.U32(), r.U32(), r.U8(), r.U8(), r.U8(), r.U8()),
            AdminMessageType.EvScoreChange => new EvScoreChange(r.U32(), r.U32(), r.U8(), AdminPlayerScore.Read(ref r)),
            AdminMessageType.EvKill => EvKill.Read(ref r),
            AdminMessageType.EvDamage => EvDamage.Read(ref r),
            AdminMessageType.EvEnterVehicle => EvVehicle.Read(ref r, AdminMessageType.EvEnterVehicle),
            AdminMessageType.EvExitVehicle => EvVehicle.Read(ref r, AdminMessageType.EvExitVehicle),
            AdminMessageType.EvVehicleDestroyed => new EvVehicleDestroyed(r.U32(), r.U32(), r.U32(), r.U32(), r.Str(), r.I16(), r.Vec()),
            AdminMessageType.EvControlPoint => new EvControlPoint(r.U32(), r.U32(), r.U8(), AdminControlPointState.Read(ref r)),
            AdminMessageType.EvChat => new EvChat(r.U32(), r.U32(), r.I16(), r.U8(), r.U8(), r.Str(), r.U32(), r.U8(), r.Str()),
            AdminMessageType.EvProjectileFired => new EvProjectileFired(r.U32(), r.U32(), AdminProjectileState.Read(ref r)),
            AdminMessageType.EvProjectileHit => new EvProjectileHit(r.U32(), r.U32(), r.U32(), r.Str(), r.I16(), r.U8(), r.U32(), r.Vec()),
            AdminMessageType.EvKick => new EvKick(r.U32(), r.U32(), r.U8(), r.Str()),
            AdminMessageType.EvBan => new EvBan(r.U32(), r.U32(), r.U8(), r.Str(), r.U8(), r.U32()),
            AdminMessageType.EvBot => new EvBot(r.U32(), r.U32(), r.U8(), r.U8(), r.U8(), r.U32()),
            AdminMessageType.EvWeaponFire => new EvWeaponFire(r.U32(), r.U32(), r.I16(), r.U32(), r.Str(), r.Str(), r.U8(), r.Vec(), r.Vec()),
            AdminMessageType.EvWeapons => EvWeapons.Read(ref r),
            AdminMessageType.EvProjectileCollision => new EvProjectileCollision(r.U32(), r.U32(), r.U32(), r.Str(), r.I16(),
                r.U8(), r.U8(), r.U32(), r.U32(), r.Str(), r.Str(), r.I16(), r.Vec(), r.Vec(), r.Vec(), r.U16(), r.F32(),
                r.Vec(), r.Vec(), r.Vec(), r.Vec()),
            AdminMessageType.EvProjectileTrace => EvProjectileTrace.Read(ref r),
            AdminMessageType.EvScriptFinished => new EvScriptFinished(r.U32(), r.U32(), r.U32(), r.Str(), r.U8(), r.Str(), r.U32(), r.U32(), r.U32()),
            AdminMessageType.EvScriptEvent => new EvScriptEvent(r.U32(), r.U32(), r.U32(), r.U8(), r.Str()),
            AdminMessageType.CmdConsole => new CmdConsole(r.U32(), r.Str()),
            AdminMessageType.CmdSay => new CmdSay(r.U32(), r.U8(), r.U8(), r.Str()),
            AdminMessageType.CmdTeleport => new CmdTeleport(r.U32(), ObjectRef.Read(ref r), r.U8(), r.Vec(), r.Vec()),
            AdminMessageType.CmdSetHealth => new CmdSetHealth(r.U32(), ObjectRef.Read(ref r), r.U8(), r.F32()),
            AdminMessageType.CmdKillPlayer => new CmdKillPlayer(r.U32(), r.U8(), r.U8()),
            AdminMessageType.CmdSetTeam => new CmdSetTeam(r.U32(), r.U8(), r.U8(), r.U8()),
            AdminMessageType.CmdSetScore => new CmdSetScore(r.U32(), r.U8(), r.U8(), r.I32(), r.I32(), r.I32(), r.I32(), r.I32()),
            AdminMessageType.CmdRank => new CmdRank(r.U32(), r.U8(), r.U8(), r.U8()),
            AdminMessageType.CmdMedal => new CmdMedal(r.U32(), r.U8(), r.I32(), r.I32()),
            AdminMessageType.CmdGameEvent => new CmdGameEvent(r.U32(), r.U8(), r.U8(), r.I32()),
            AdminMessageType.CmdHudEvent => new CmdHudEvent(r.U32(), r.U8(), r.U8(), r.I32()),
            AdminMessageType.CmdKick => new CmdKick(r.U32(), r.U8(), r.Str()),
            AdminMessageType.CmdBan => new CmdBan(r.U32(), r.U8(), r.U8(), r.U8(), r.U32(), r.Str()),
            AdminMessageType.CmdMap => new CmdMap(r.U32(), r.U8(), r.U16(), r.Str(), r.Str(), r.U16()),
            AdminMessageType.CmdListObjects => new CmdListObjects(r.U32(), r.Str(), r.U16()),
            AdminMessageType.CmdSetObjectActive => new CmdSetObjectActive(r.U32(), ObjectRef.Read(ref r)),
            AdminMessageType.CmdSpawnObject => new CmdSpawnObject(r.U32(), r.Str(), r.Vec(), r.Vec(), r.U8()),
            AdminMessageType.CmdDeleteObject => new CmdDeleteObject(r.U32(), ObjectRef.Read(ref r)),
            AdminMessageType.CmdStartScript => new CmdStartScript(r.U32(), r.U32(), r.U8(), r.U8(), r.Str(), r.Str(), r.Str()),
            AdminMessageType.CmdStopScript => new CmdStopScript(r.U32(), r.U32()),
            AdminMessageType.CmdScriptMessage => new CmdScriptMessage(r.U32(), r.U32(), r.Str()),
            AdminMessageType.CmdListScripts => new CmdListScripts(r.U32()),
            AdminMessageType.CmdSeatPreference => new CmdSeatPreference(r.U32(), r.U8(), r.U8()),
            AdminMessageType.Ok => new OkReply(r.U32()),
            AdminMessageType.ConsoleResult => ConsoleResult.Read(ref r),
            AdminMessageType.ObjectList => ObjectList.Read(ref r),
            AdminMessageType.ObjectSpawned => new ObjectSpawned(r.U32(), r.U32(), r.U32()),
            AdminMessageType.ScriptList => ScriptList.Read(ref r),
            AdminMessageType.Ping => new Ping(r.U32(), r.U64()),
            AdminMessageType.Pong => new Pong(r.U32(), r.U64()),
            AdminMessageType.Error => new ErrorMessage(r.U32(), (AdminErrorCode)r.U16(), r.Str()),
            _ => throw new AdminProtocolException(AdminErrorCode.UnknownType, $"unknown message type 0x{(ushort)type:X4}"),
        };

        if (!r.End)
            throw new AdminProtocolException(AdminErrorCode.Malformed, $"{r.Remaining} trailing bytes in {type}");

        return m;
    }

    /// <summary>
    /// T(label) = label || 0x00 || u16le(len(id)) || id || Nm || Nw. Pw uses the WebAdmin label, Pm the server label.
    /// </summary>
    public static byte[] ProofTranscript(string label, string serverId, ReadOnlySpan<byte> moduleNonce, ReadOnlySpan<byte> webAdminNonce)
    {
        var labelBytes = Encoding.ASCII.GetBytes(label);
        var id = Utf8.GetBytes(serverId);
        var t = new byte[labelBytes.Length + 1 + 2 + id.Length + NonceSize * 2];
        var span = t.AsSpan();
        labelBytes.CopyTo(span);
        span = span[(labelBytes.Length + 1)..];
        BinaryPrimitives.WriteUInt16LittleEndian(span, (ushort)id.Length);
        id.CopyTo(span[2..]);
        span = span[(2 + id.Length)..];
        moduleNonce[..NonceSize].CopyTo(span);
        webAdminNonce[..NonceSize].CopyTo(span[NonceSize..]);
        return t;
    }

    public const string WebAdminProofLabel = "OBF2-ADMIN-1 webadmin";
    public const string ServerProofLabel = "OBF2-ADMIN-1 server";

    public static byte[] Proof(ReadOnlySpan<byte> key, string label, string serverId, ReadOnlySpan<byte> moduleNonce, ReadOnlySpan<byte> webAdminNonce)
    {
        return System.Security.Cryptography.HMACSHA256.HashData(key, ProofTranscript(label, serverId, moduleNonce, webAdminNonce));
    }
}

public class AdminProtocolException(AdminErrorCode code, string message) : Exception(message)
{
    public AdminErrorCode Code { get; } = code;
}

/// <summary>Splits a byte stream into frames. Rejects oversize frames and a nonzero reserved field.</summary>
public sealed class AdminFrameDecoder(int maxPayload = AdminProtocol.MaxInboundPayload)
{
    private byte[] _buffer = new byte[16 * 1024];
    private int _count;

    public void Append(ReadOnlySpan<byte> data)
    {
        if (_count + data.Length > _buffer.Length)
            Array.Resize(ref _buffer, Math.Max(_buffer.Length * 2, _count + data.Length));

        data.CopyTo(_buffer.AsSpan(_count));
        _count += data.Length;
    }

    /// <summary>Returns the next complete frame, or false when more bytes are needed.</summary>
    public bool TryRead(out AdminMessageType type, out byte[] payload)
    {
        type = default;
        payload = [];
        if (_count < AdminProtocol.HeaderSize)
            return false;

        var span = _buffer.AsSpan(0, _count);
        var length = BinaryPrimitives.ReadUInt32LittleEndian(span);
        var reserved = BinaryPrimitives.ReadUInt16LittleEndian(span[6..]);
        if (length > maxPayload)
            throw new AdminProtocolException(AdminErrorCode.Malformed, $"frame length {length} exceeds {maxPayload}");
        if (reserved != 0)
            throw new AdminProtocolException(AdminErrorCode.Malformed, $"nonzero reserved field {reserved}");

        if (_count < AdminProtocol.HeaderSize + length)
            return false;

        type = (AdminMessageType)BinaryPrimitives.ReadUInt16LittleEndian(span[4..]);
        payload = span.Slice(AdminProtocol.HeaderSize, (int)length).ToArray();
        var consumed = AdminProtocol.HeaderSize + (int)length;
        Buffer.BlockCopy(_buffer, consumed, _buffer, 0, _count - consumed);
        _count -= consumed;
        return true;
    }
}

public sealed class AdminWriter
{
    private readonly ArrayBufferWriter<byte> _buffer = new(256);

    public byte[] ToArray() => _buffer.WrittenSpan.ToArray();

    private Span<byte> Take(int n)
    {
        var span = _buffer.GetSpan(n)[..n];
        _buffer.Advance(n);
        return span;
    }

    public AdminWriter Byte(byte v) { Take(1)[0] = v; return this; }
    public AdminWriter I16(short v) { BinaryPrimitives.WriteInt16LittleEndian(Take(2), v); return this; }
    public AdminWriter U16(ushort v) { BinaryPrimitives.WriteUInt16LittleEndian(Take(2), v); return this; }
    public AdminWriter I32(int v) { BinaryPrimitives.WriteInt32LittleEndian(Take(4), v); return this; }
    public AdminWriter U32(uint v) { BinaryPrimitives.WriteUInt32LittleEndian(Take(4), v); return this; }
    public AdminWriter U64(ulong v) { BinaryPrimitives.WriteUInt64LittleEndian(Take(8), v); return this; }
    public AdminWriter F32(float v) { BinaryPrimitives.WriteSingleLittleEndian(Take(4), v); return this; }
    public AdminWriter F64(double v) { BinaryPrimitives.WriteDoubleLittleEndian(Take(8), v); return this; }
    public AdminWriter Vec(Vec3 v) => F32(v.X).F32(v.Y).F32(v.Z);
    public AdminWriter Bytes(ReadOnlySpan<byte> v) { v.CopyTo(Take(v.Length)); return this; }

    public AdminWriter Str(string v)
    {
        var bytes = AdminProtocol.Utf8.GetBytes(v);
        if (bytes.Length > ushort.MaxValue)
            throw new ArgumentException($"string of {bytes.Length} bytes is too long for the admin protocol");
        return U16((ushort)bytes.Length).Bytes(bytes);
    }
}

public ref struct AdminReader(ReadOnlySpan<byte> data)
{
    private ReadOnlySpan<byte> _data = data;

    public bool End => _data.IsEmpty;
    public int Remaining => _data.Length;

    private ReadOnlySpan<byte> Take(int n)
    {
        if (_data.Length < n)
            throw new AdminProtocolException(AdminErrorCode.Malformed, $"payload ends {n - _data.Length} bytes early");

        var span = _data[..n];
        _data = _data[n..];
        return span;
    }

    public byte U8() => Take(1)[0];
    public short I16() => BinaryPrimitives.ReadInt16LittleEndian(Take(2));
    public ushort U16() => BinaryPrimitives.ReadUInt16LittleEndian(Take(2));
    public int I32() => BinaryPrimitives.ReadInt32LittleEndian(Take(4));
    public uint U32() => BinaryPrimitives.ReadUInt32LittleEndian(Take(4));
    public ulong U64() => BinaryPrimitives.ReadUInt64LittleEndian(Take(8));
    public float F32() => BinaryPrimitives.ReadSingleLittleEndian(Take(4));
    public double F64() => BinaryPrimitives.ReadDoubleLittleEndian(Take(8));
    public Vec3 Vec() => new(F32(), F32(), F32());
    public byte[] Bytes(int n) => Take(n).ToArray();

    public string Str()
    {
        var bytes = Take(U16());
        try
        {
            return AdminProtocol.Utf8.GetString(bytes);
        }
        catch (DecoderFallbackException e)
        {
            throw new AdminProtocolException(AdminErrorCode.Malformed, $"invalid UTF-8: {e.Message}");
        }
    }
}

/// <summary>x, y (up), z in metres; or yaw, pitch, roll in degrees for rotations.</summary>
public readonly record struct Vec3(float X, float Y, float Z);

// ---------------------------------------------------------------- structures

public sealed record AdminGameState(
    byte Status, byte BeginRound, float TimeToNext, int Tickets1, int Tickets2, float RoundTime, float TimeLimit, byte Winner, byte VictoryType)
{
    public static AdminGameState Read(ref AdminReader r) => new(r.U8(), r.U8(), r.F32(), r.I32(), r.I32(), r.F32(), r.F32(), r.U8(), r.U8());

    public void Write(AdminWriter w) =>
        w.Byte(Status).Byte(BeginRound).F32(TimeToNext).I32(Tickets1).I32(Tickets2).F32(RoundTime).F32(TimeLimit).Byte(Winner).Byte(VictoryType);
}

public sealed record AdminPlayerState(
    byte Team, byte Squad, byte Life, byte Flags, ushort Ping, uint RootNetId, uint RootConsoleId, byte Seat,
    Vec3 Position, Vec3 Rotation, Vec3 Velocity, float Health, byte ViewMode, ushort LagMs)
{
    public const byte LifeDead = 0, LifeAlive = 1, LifeManDown = 2;
    public const byte FlagSeated = 1, FlagCommander = 2, FlagSquadLeader = 4;
    // Camera view modes; ViewUnknown when dead or without a camera.
    public const byte ViewNone = 0, ViewInside = 1, ViewNose = 2, ViewChase = 3, ViewFrontChase = 4, ViewFlyBy = 5,
        ViewTrace = 6, ViewExternTrace = 7, ViewUnknown = 255;

    public bool IsAlive => Life != LifeDead;

    public static AdminPlayerState Read(ref AdminReader r) =>
        new(r.U8(), r.U8(), r.U8(), r.U8(), r.U16(), r.U32(), r.U32(), r.U8(), r.Vec(), r.Vec(), r.Vec(), r.F32(), r.U8(), r.U16());

    public void Write(AdminWriter w) =>
        w.Byte(Team).Byte(Squad).Byte(Life).Byte(Flags).U16(Ping).U32(RootNetId).U32(RootConsoleId).Byte(Seat)
            .Vec(Position).Vec(Rotation).Vec(Velocity).F32(Health).Byte(ViewMode).U16(LagMs);
}

/// <summary>
/// A living player's camera and active-weapon transforms (AimStream). The camera is the server's copy of the view:
/// the eye in views 1-2, the external camera in 3-7. The server fires along the weapon transform, or the camera's
/// for soldiers and weapons that fire in camera DOF.
/// </summary>
public sealed record AdminAimState(byte Player, byte Flags, Vec3 CameraPosition, Vec3 CameraRotation, Vec3 WeaponPosition, Vec3 WeaponRotation)
{
    public const byte FlagCamera = 1, FlagWeapon = 2;

    public static AdminAimState Read(ref AdminReader r) => new(r.U8(), r.U8(), r.Vec(), r.Vec(), r.Vec(), r.Vec());

    public void Write(AdminWriter w) =>
        w.Byte(Player).Byte(Flags).Vec(CameraPosition).Vec(CameraRotation).Vec(WeaponPosition).Vec(WeaponRotation);
}

public sealed record AdminWeaponSlot(byte Slot, string Template);

/// <summary>A target of a ProjectileTrace: a vehicle root that came within 150 m of the projectile.</summary>
public sealed record AdminTraceTarget(uint NetId, string Template);

/// <summary>A target in one trace sample: where it is now ("red") and where this tick's sweep tests it ("blue").</summary>
public sealed record AdminTracePose(Vec3 Position, Vec3 Rotation, Vec3 TestedPosition, Vec3 TestedRotation);

/// <summary>One tick of a ProjectileTrace: the projectile, the shooter's lag compensation and every target's poses.</summary>
public sealed record AdminTraceSample(Vec3 Position, Vec3 Velocity, float Lag, IReadOnlyList<AdminTracePose> Targets);

public sealed record AdminPlayerScore(int Score, int TeamScore, int Kills, int Deaths, int TeamKills, short Rank)
{
    public static AdminPlayerScore Read(ref AdminReader r) => new(r.I32(), r.I32(), r.I32(), r.I32(), r.I32(), r.I16());

    public void Write(AdminWriter w) => w.I32(Score).I32(TeamScore).I32(Kills).I32(Deaths).I32(TeamKills).I16(Rank);
}

public sealed record AdminPlayerInfo(
    byte Id, string Name, byte Flags, int ProfileId, string IpAddress, string CdKeyHash, AdminPlayerScore Score, AdminPlayerState State)
{
    public const byte FlagAi = 1, FlagBotChannel = 2, FlagConnected = 4, FlagIdentity = 8;

    public bool IsAi => (Flags & FlagAi) != 0;

    public static AdminPlayerInfo Read(ref AdminReader r) =>
        new(r.U8(), r.Str(), r.U8(), r.I32(), r.Str(), r.Str(), AdminPlayerScore.Read(ref r), AdminPlayerState.Read(ref r));

    public void Write(AdminWriter w)
    {
        w.Byte(Id).Str(Name).Byte(Flags).I32(ProfileId).Str(IpAddress).Str(CdKeyHash);
        Score.Write(w);
        State.Write(w);
    }
}

public sealed record AdminVehicleState(
    uint NetId, uint ConsoleId, string Template, byte Team, byte Flags, Vec3 Position, Vec3 Rotation, Vec3 Velocity,
    float Health, byte Seats, uint OccupiedSeats)
{
    public const byte FlagDestroyed = 1;

    public static AdminVehicleState Read(ref AdminReader r) =>
        new(r.U32(), r.U32(), r.Str(), r.U8(), r.U8(), r.Vec(), r.Vec(), r.Vec(), r.F32(), r.U8(), r.U32());

    public void Write(AdminWriter w) =>
        w.U32(NetId).U32(ConsoleId).Str(Template).Byte(Team).Byte(Flags).Vec(Position).Vec(Rotation).Vec(Velocity)
            .F32(Health).Byte(Seats).U32(OccupiedSeats);
}

public sealed record AdminControlPointState(
    uint ConsoleId, uint NetId, string Template, byte Team, byte FlagTeam, float TakeOver, byte Capturing, Vec3 Position)
{
    public static AdminControlPointState Read(ref AdminReader r) =>
        new(r.U32(), r.U32(), r.Str(), r.U8(), r.U8(), r.F32(), r.U8(), r.Vec());

    public void Write(AdminWriter w) =>
        w.U32(ConsoleId).U32(NetId).Str(Template).Byte(Team).Byte(FlagTeam).F32(TakeOver).Byte(Capturing).Vec(Position);
}

public sealed record AdminProjectileState(
    uint Id, string Template, short Owner, byte OwnerTeam, byte Flags, uint TargetNetId, Vec3 Position, Vec3 Rotation,
    Vec3 Velocity, float TimeToLive)
{
    public static AdminProjectileState Read(ref AdminReader r) =>
        new(r.U32(), r.Str(), r.I16(), r.U8(), r.U8(), r.U32(), r.Vec(), r.Vec(), r.Vec(), r.F32());

    public void Write(AdminWriter w) =>
        w.U32(Id).Str(Template).I16(Owner).Byte(OwnerTeam).Byte(Flags).U32(TargetNetId).Vec(Position).Vec(Rotation)
            .Vec(Velocity).F32(TimeToLive);
}

public sealed record ObjectRef(ObjectRefKind Kind, uint Id)
{
    public static ObjectRef Player(int playerId) => new(ObjectRefKind.PlayerRoot, (uint)playerId);
    public static ObjectRef Networkable(uint id) => new(ObjectRefKind.Networkable, id);
    public static ObjectRef Console(uint id) => new(ObjectRefKind.Console, id);

    public static ObjectRef Read(ref AdminReader r) => new((ObjectRefKind)r.U8(), r.U32());

    public void Write(AdminWriter w) => w.Byte((byte)Kind).U32(Id);
}

public sealed record MapListEntry(string Name, string Mode, ushort Size);

public sealed record ObjectListEntry(uint ConsoleId, uint NetId, string Template, Vec3 Position, Vec3 Rotation, float Health, byte Flags);

// ---------------------------------------------------------------- messages

public abstract record AdminMessage
{
    public abstract AdminMessageType Type { get; }
    public abstract void Write(AdminWriter w);
}

/// <summary>A message that answers a request (or is a command carrying one).</summary>
public interface IAdminRequest
{
    uint RequestId { get; }
}

/// <summary>Every event starts with its sequence number and tick.</summary>
public abstract record AdminEvent(uint Seq, uint Tick) : AdminMessage
{
    public sealed override void Write(AdminWriter w)
    {
        w.U32(Seq).U32(Tick);
        WriteFields(w);
    }

    protected abstract void WriteFields(AdminWriter w);
}

public sealed record Hello(ushort Version, string ServerId, byte[] Nonce, string Build) : AdminMessage
{
    public override AdminMessageType Type => AdminMessageType.Hello;
    public static Hello Read(ref AdminReader r) => new(r.U16(), r.Str(), r.Bytes(AdminProtocol.NonceSize), r.Str());
    public override void Write(AdminWriter w) => w.U16(Version).Str(ServerId).Bytes(Nonce).Str(Build);
}

public sealed record Challenge(byte[] Nonce, byte[] Proof) : AdminMessage
{
    public override AdminMessageType Type => AdminMessageType.Challenge;
    public static Challenge Read(ref AdminReader r) => new(r.Bytes(AdminProtocol.NonceSize), r.Bytes(AdminProtocol.ProofSize));
    public override void Write(AdminWriter w) => w.Bytes(Nonce).Bytes(Proof);
}

public sealed record Auth(byte[] Proof) : AdminMessage
{
    public override AdminMessageType Type => AdminMessageType.Auth;
    public static Auth Read(ref AdminReader r) => new(r.Bytes(AdminProtocol.ProofSize));
    public override void Write(AdminWriter w) => w.Bytes(Proof);
}

public sealed record Welcome(
    ushort Version, uint Session, uint Tick, float TickRate, string ServerName, ushort GamePort, ushort GameSpyPort,
    byte MaxPlayers, byte Capabilities, string Map, string Mode, ushort MapSize, ushort MapIndex,
    IReadOnlyList<MapListEntry> Maps, string Team1, string Team2, AdminGameState Game) : AdminMessage
{
    public const byte CapConsole = 1, CapObjects = 2, CapIdentity = 4;

    /// <summary>The module runs game scripts (StartScript and the other script messages; docs/scripts.md).</summary>
    public const byte CapScripts = 8;

    /// <summary>The module takes seat preferences (<see cref="CmdSeatPreference"/>).</summary>
    public const byte CapSeats = 16;

    public override AdminMessageType Type => AdminMessageType.Welcome;

    public static Welcome Read(ref AdminReader r)
    {
        var version = r.U16();
        var session = r.U32();
        var tick = r.U32();
        var tickRate = r.F32();
        var name = r.Str();
        var gamePort = r.U16();
        var gameSpyPort = r.U16();
        var maxPlayers = r.U8();
        var caps = r.U8();
        var map = r.Str();
        var mode = r.Str();
        var mapSize = r.U16();
        var mapIndex = r.U16();
        var maps = new MapListEntry[r.U16()];
        for (var i = 0; i < maps.Length; i++)
            maps[i] = new MapListEntry(r.Str(), r.Str(), r.U16());
        return new Welcome(version, session, tick, tickRate, name, gamePort, gameSpyPort, maxPlayers, caps, map, mode, mapSize, mapIndex,
            maps, r.Str(), r.Str(), AdminGameState.Read(ref r));
    }

    public override void Write(AdminWriter w)
    {
        w.U16(Version).U32(Session).U32(Tick).F32(TickRate).Str(ServerName).U16(GamePort).U16(GameSpyPort).Byte(MaxPlayers)
            .Byte(Capabilities).Str(Map).Str(Mode).U16(MapSize).U16(MapIndex).U16((ushort)Maps.Count);
        foreach (var m in Maps)
            w.Str(m.Name).Str(m.Mode).U16(m.Size);
        w.Str(Team1).Str(Team2);
        Game.Write(w);
    }
}

public sealed record Snapshot(
    uint RequestId, uint LastSeq, uint Tick, double Time, AdminGameState Game, IReadOnlyList<AdminPlayerInfo> Players,
    IReadOnlyList<AdminVehicleState> Vehicles, IReadOnlyList<AdminControlPointState> ControlPoints,
    IReadOnlyList<AdminProjectileState> Projectiles) : AdminMessage, IAdminRequest
{
    public override AdminMessageType Type => AdminMessageType.Snapshot;

    public static Snapshot Read(ref AdminReader r)
    {
        var request = r.U32();
        var lastSeq = r.U32();
        var tick = r.U32();
        var time = r.F64();
        var game = AdminGameState.Read(ref r);
        var players = new AdminPlayerInfo[r.U8()];
        for (var i = 0; i < players.Length; i++)
            players[i] = AdminPlayerInfo.Read(ref r);
        var vehicles = new AdminVehicleState[r.U16()];
        for (var i = 0; i < vehicles.Length; i++)
            vehicles[i] = AdminVehicleState.Read(ref r);
        var cps = new AdminControlPointState[r.U16()];
        for (var i = 0; i < cps.Length; i++)
            cps[i] = AdminControlPointState.Read(ref r);
        var projectiles = new AdminProjectileState[r.U16()];
        for (var i = 0; i < projectiles.Length; i++)
            projectiles[i] = AdminProjectileState.Read(ref r);
        return new Snapshot(request, lastSeq, tick, time, game, players, vehicles, cps, projectiles);
    }

    public override void Write(AdminWriter w)
    {
        w.U32(RequestId).U32(LastSeq).U32(Tick).F64(Time);
        Game.Write(w);
        w.Byte((byte)Players.Count);
        foreach (var p in Players) p.Write(w);
        w.U16((ushort)Vehicles.Count);
        foreach (var v in Vehicles) v.Write(w);
        w.U16((ushort)ControlPoints.Count);
        foreach (var c in ControlPoints) c.Write(w);
        w.U16((ushort)Projectiles.Count);
        foreach (var p in Projectiles) p.Write(w);
    }
}

public sealed record RequestSnapshot(uint RequestId) : AdminMessage, IAdminRequest
{
    public override AdminMessageType Type => AdminMessageType.RequestSnapshot;
    public override void Write(AdminWriter w) => w.U32(RequestId);
}

public sealed record Subscribe(uint RequestId, AdminStream Stream, byte RateHz, IReadOnlyList<string> Filters) : AdminMessage, IAdminRequest
{
    public override AdminMessageType Type => AdminMessageType.Subscribe;

    public static Subscribe Read(ref AdminReader r)
    {
        var request = r.U32();
        var stream = (AdminStream)r.U8();
        var rate = r.U8();
        var filters = new string[r.U8()];
        for (var i = 0; i < filters.Length; i++)
            filters[i] = r.Str();
        return new Subscribe(request, stream, rate, filters);
    }

    public override void Write(AdminWriter w)
    {
        w.U32(RequestId).Byte((byte)Stream).Byte(RateHz).Byte((byte)Filters.Count);
        foreach (var f in Filters) w.Str(f);
    }
}

public sealed record Unsubscribe(uint RequestId, AdminStream Stream) : AdminMessage, IAdminRequest
{
    public override AdminMessageType Type => AdminMessageType.Unsubscribe;
    public override void Write(AdminWriter w) => w.U32(RequestId).Byte((byte)Stream);
}

public sealed record PlayerStream(uint Tick, double Time, IReadOnlyList<(byte Id, AdminPlayerState State)> Players) : AdminMessage
{
    public override AdminMessageType Type => AdminMessageType.PlayerStream;

    public static PlayerStream Read(ref AdminReader r)
    {
        var tick = r.U32();
        var time = r.F64();
        var players = new (byte, AdminPlayerState)[r.U8()];
        for (var i = 0; i < players.Length; i++)
            players[i] = (r.U8(), AdminPlayerState.Read(ref r));
        return new PlayerStream(tick, time, players);
    }

    public override void Write(AdminWriter w)
    {
        w.U32(Tick).F64(Time).Byte((byte)Players.Count);
        foreach (var (id, state) in Players)
        {
            w.Byte(id);
            state.Write(w);
        }
    }
}

public sealed record VehicleStream(uint Tick, double Time, IReadOnlyList<AdminVehicleState> Vehicles) : AdminMessage
{
    public override AdminMessageType Type => AdminMessageType.VehicleStream;

    public static VehicleStream Read(ref AdminReader r)
    {
        var tick = r.U32();
        var time = r.F64();
        var vehicles = new AdminVehicleState[r.U16()];
        for (var i = 0; i < vehicles.Length; i++)
            vehicles[i] = AdminVehicleState.Read(ref r);
        return new VehicleStream(tick, time, vehicles);
    }

    public override void Write(AdminWriter w)
    {
        w.U32(Tick).F64(Time).U16((ushort)Vehicles.Count);
        foreach (var v in Vehicles) v.Write(w);
    }
}

public sealed record ProjectileStream(uint Tick, double Time, IReadOnlyList<AdminProjectileState> Projectiles) : AdminMessage
{
    public override AdminMessageType Type => AdminMessageType.ProjectileStream;

    public static ProjectileStream Read(ref AdminReader r)
    {
        var tick = r.U32();
        var time = r.F64();
        var projectiles = new AdminProjectileState[r.U16()];
        for (var i = 0; i < projectiles.Length; i++)
            projectiles[i] = AdminProjectileState.Read(ref r);
        return new ProjectileStream(tick, time, projectiles);
    }

    public override void Write(AdminWriter w)
    {
        w.U32(Tick).F64(Time).U16((ushort)Projectiles.Count);
        foreach (var p in Projectiles) p.Write(w);
    }
}

public sealed record GameStream(uint Tick, double Time, AdminGameState Game) : AdminMessage
{
    public override AdminMessageType Type => AdminMessageType.GameStream;

    public override void Write(AdminWriter w)
    {
        w.U32(Tick).F64(Time);
        Game.Write(w);
    }
}

public sealed record AimStream(uint Tick, double Time, IReadOnlyList<AdminAimState> Aims) : AdminMessage
{
    public override AdminMessageType Type => AdminMessageType.AimStream;

    public static AimStream Read(ref AdminReader r)
    {
        var tick = r.U32();
        var time = r.F64();
        var aims = new AdminAimState[r.U8()];
        for (var i = 0; i < aims.Length; i++)
            aims[i] = AdminAimState.Read(ref r);
        return new AimStream(tick, time, aims);
    }

    public override void Write(AdminWriter w)
    {
        w.U32(Tick).F64(Time).Byte((byte)Aims.Count);
        foreach (var a in Aims) a.Write(w);
    }
}

// ---------------------------------------------------------------- events

public sealed record EvGameState(uint Seq, uint Tick, AdminGameState Game, byte PreviousStatus) : AdminEvent(Seq, Tick)
{
    public override AdminMessageType Type => AdminMessageType.EvGameState;

    protected override void WriteFields(AdminWriter w)
    {
        Game.Write(w);
        w.Byte(PreviousStatus);
    }
}

public sealed record EvRoundStart(uint Seq, uint Tick, string Map, string Mode, int Tickets1, int Tickets2) : AdminEvent(Seq, Tick)
{
    public override AdminMessageType Type => AdminMessageType.EvRoundStart;
    protected override void WriteFields(AdminWriter w) => w.Str(Map).Str(Mode).I32(Tickets1).I32(Tickets2);
}

public sealed record EvRoundEnd(uint Seq, uint Tick, int Tickets1, int Tickets2, byte Winner, byte VictoryType) : AdminEvent(Seq, Tick)
{
    public override AdminMessageType Type => AdminMessageType.EvRoundEnd;
    protected override void WriteFields(AdminWriter w) => w.I32(Tickets1).I32(Tickets2).Byte(Winner).Byte(VictoryType);
}

public sealed record EvPlayerConnect(uint Seq, uint Tick, AdminPlayerInfo Player) : AdminEvent(Seq, Tick)
{
    public override AdminMessageType Type => AdminMessageType.EvPlayerConnect;
    protected override void WriteFields(AdminWriter w) => Player.Write(w);
}

public sealed record EvPlayerDisconnect(uint Seq, uint Tick, byte Player, byte Reason, ushort EngineReason) : AdminEvent(Seq, Tick)
{
    public override AdminMessageType Type => AdminMessageType.EvPlayerDisconnect;
    protected override void WriteFields(AdminWriter w) => w.Byte(Player).Byte(Reason).U16(EngineReason);
}

public sealed record EvSpawn(uint Seq, uint Tick, byte Player, byte Team, uint RootNetId, uint RootConsoleId, string RootTemplate, Vec3 Position, Vec3 Rotation)
    : AdminEvent(Seq, Tick)
{
    public override AdminMessageType Type => AdminMessageType.EvSpawn;

    protected override void WriteFields(AdminWriter w) =>
        w.Byte(Player).Byte(Team).U32(RootNetId).U32(RootConsoleId).Str(RootTemplate).Vec(Position).Vec(Rotation);
}

public sealed record EvDeath(uint Seq, uint Tick, byte Player, byte Team, uint NetId, string Template, Vec3 Position) : AdminEvent(Seq, Tick)
{
    public override AdminMessageType Type => AdminMessageType.EvDeath;
    protected override void WriteFields(AdminWriter w) => w.Byte(Player).Byte(Team).U32(NetId).Str(Template).Vec(Position);
}

public sealed record EvRevive(uint Seq, uint Tick, byte Player, short Medic) : AdminEvent(Seq, Tick)
{
    public override AdminMessageType Type => AdminMessageType.EvRevive;
    protected override void WriteFields(AdminWriter w) => w.Byte(Player).I16(Medic);
}

public sealed record EvTeamChange(uint Seq, uint Tick, byte Player, byte OldTeam, byte NewTeam, byte Flags) : AdminEvent(Seq, Tick)
{
    public override AdminMessageType Type => AdminMessageType.EvTeamChange;
    protected override void WriteFields(AdminWriter w) => w.Byte(Player).Byte(OldTeam).Byte(NewTeam).Byte(Flags);
}

public sealed record EvScoreChange(uint Seq, uint Tick, byte Player, AdminPlayerScore Score) : AdminEvent(Seq, Tick)
{
    public override AdminMessageType Type => AdminMessageType.EvScoreChange;

    protected override void WriteFields(AdminWriter w)
    {
        w.Byte(Player);
        Score.Write(w);
    }
}

public sealed record EvKill(
    uint Seq, uint Tick, byte Victim, short Attacker, KillKind Kind, string Weapon, byte VictimTeam, uint VictimNetId,
    string VictimTemplate, Vec3 VictimPosition, byte AttackerTeam, uint AttackerVehicleNetId, string AttackerVehicleTemplate,
    Vec3 AttackerPosition, IReadOnlyList<(byte Player, int Score)> Assists) : AdminEvent(Seq, Tick)
{
    public override AdminMessageType Type => AdminMessageType.EvKill;

    public static EvKill Read(ref AdminReader r)
    {
        var seq = r.U32();
        var tick = r.U32();
        var victim = r.U8();
        var attacker = r.I16();
        var kind = (KillKind)r.U8();
        var weapon = r.Str();
        var victimTeam = r.U8();
        var victimNet = r.U32();
        var victimTemplate = r.Str();
        var victimPos = r.Vec();
        var attackerTeam = r.U8();
        var attackerVehicle = r.U32();
        var attackerVehicleTemplate = r.Str();
        var attackerPos = r.Vec();
        var assists = new (byte, int)[r.U8()];
        for (var i = 0; i < assists.Length; i++)
            assists[i] = (r.U8(), r.I32());
        return new EvKill(seq, tick, victim, attacker, kind, weapon, victimTeam, victimNet, victimTemplate, victimPos, attackerTeam,
            attackerVehicle, attackerVehicleTemplate, attackerPos, assists);
    }

    protected override void WriteFields(AdminWriter w)
    {
        w.Byte(Victim).I16(Attacker).Byte((byte)Kind).Str(Weapon).Byte(VictimTeam).U32(VictimNetId).Str(VictimTemplate).Vec(VictimPosition)
            .Byte(AttackerTeam).U32(AttackerVehicleNetId).Str(AttackerVehicleTemplate).Vec(AttackerPosition).Byte((byte)Assists.Count);
        foreach (var (player, score) in Assists)
            w.Byte(player).I32(score);
    }
}

public sealed record EvDamage(
    uint Seq, uint Tick, short VictimPlayer, uint VictimNetId, uint VictimConsoleId, string VictimTemplate, short Attacker,
    string Weapon, string Projectile, float Amount, float Before, float After, float Max, Vec3 Position, byte Flags) : AdminEvent(Seq, Tick)
{
    public override AdminMessageType Type => AdminMessageType.EvDamage;

    public static EvDamage Read(ref AdminReader r) =>
        new(r.U32(), r.U32(), r.I16(), r.U32(), r.U32(), r.Str(), r.I16(), r.Str(), r.Str(), r.F32(), r.F32(), r.F32(), r.F32(), r.Vec(), r.U8());

    protected override void WriteFields(AdminWriter w) =>
        w.I16(VictimPlayer).U32(VictimNetId).U32(VictimConsoleId).Str(VictimTemplate).I16(Attacker).Str(Weapon).Str(Projectile)
            .F32(Amount).F32(Before).F32(After).F32(Max).Vec(Position).Byte(Flags);
}

/// <summary>EnterVehicle and ExitVehicle share one layout.</summary>
public sealed record EvVehicle(
    AdminMessageType EventType, uint Seq, uint Tick, byte Player, byte Seat, byte Flags, uint RootNetId, uint RootConsoleId,
    string RootTemplate, string SeatTemplate) : AdminEvent(Seq, Tick)
{
    public override AdminMessageType Type => EventType;

    public static EvVehicle Read(ref AdminReader r, AdminMessageType type) =>
        new(type, r.U32(), r.U32(), r.U8(), r.U8(), r.U8(), r.U32(), r.U32(), r.Str(), r.Str());

    protected override void WriteFields(AdminWriter w) =>
        w.Byte(Player).Byte(Seat).Byte(Flags).U32(RootNetId).U32(RootConsoleId).Str(RootTemplate).Str(SeatTemplate);
}

public sealed record EvVehicleDestroyed(uint Seq, uint Tick, uint NetId, uint ConsoleId, string Template, short Attacker, Vec3 Position)
    : AdminEvent(Seq, Tick)
{
    public override AdminMessageType Type => AdminMessageType.EvVehicleDestroyed;
    protected override void WriteFields(AdminWriter w) => w.U32(NetId).U32(ConsoleId).Str(Template).I16(Attacker).Vec(Position);
}

public sealed record EvControlPoint(uint Seq, uint Tick, byte Kind, AdminControlPointState ControlPoint) : AdminEvent(Seq, Tick)
{
    public override AdminMessageType Type => AdminMessageType.EvControlPoint;

    protected override void WriteFields(AdminWriter w)
    {
        w.Byte(Kind);
        ControlPoint.Write(w);
    }
}

public sealed record EvChat(
    uint Seq, uint Tick, short Sender, byte SenderTeam, byte Channel, string ChannelName, uint EngineFlags, byte TextFlags, string Text)
    : AdminEvent(Seq, Tick)
{
    public override AdminMessageType Type => AdminMessageType.EvChat;

    protected override void WriteFields(AdminWriter w) =>
        w.I16(Sender).Byte(SenderTeam).Byte(Channel).Str(ChannelName).U32(EngineFlags).Byte(TextFlags).Str(Text);
}

public sealed record EvProjectileFired(uint Seq, uint Tick, AdminProjectileState Projectile) : AdminEvent(Seq, Tick)
{
    public override AdminMessageType Type => AdminMessageType.EvProjectileFired;
    protected override void WriteFields(AdminWriter w) => Projectile.Write(w);
}

public sealed record EvProjectileHit(uint Seq, uint Tick, uint Id, string Template, short Owner, byte Flags, uint VictimNetId, Vec3 Position)
    : AdminEvent(Seq, Tick)
{
    public override AdminMessageType Type => AdminMessageType.EvProjectileHit;
    protected override void WriteFields(AdminWriter w) => w.U32(Id).Str(Template).I16(Owner).Byte(Flags).U32(VictimNetId).Vec(Position);
}

public sealed record EvKick(uint Seq, uint Tick, byte Player, string Name) : AdminEvent(Seq, Tick)
{
    public override AdminMessageType Type => AdminMessageType.EvKick;
    protected override void WriteFields(AdminWriter w) => w.Byte(Player).Str(Name);
}

public sealed record EvBan(uint Seq, uint Tick, byte Player, string Name, byte BanType, uint Expiry) : AdminEvent(Seq, Tick)
{
    public override AdminMessageType Type => AdminMessageType.EvBan;
    protected override void WriteFields(AdminWriter w) => w.Byte(Player).Str(Name).Byte(BanType).U32(Expiry);
}

public sealed record EvBot(uint Seq, uint Tick, byte Player, byte Kind, byte Team, uint Connection) : AdminEvent(Seq, Tick)
{
    public override AdminMessageType Type => AdminMessageType.EvBot;
    protected override void WriteFields(AdminWriter w) => w.Byte(Player).Byte(Kind).Byte(Team).U32(Connection);
}

/// <summary>A shooter's launches from one weapon in one tick (stream 18): bullets, rockets, missiles and flares.</summary>
public sealed record EvWeaponFire(uint Seq, uint Tick, short Shooter, uint RootNetId, string Weapon, string Projectile, byte Rounds,
    Vec3 Origin, Vec3 Velocity) : AdminEvent(Seq, Tick)
{
    public override AdminMessageType Type => AdminMessageType.EvWeaponFire;
    protected override void WriteFields(AdminWriter w) =>
        w.I16(Shooter).U32(RootNetId).Str(Weapon).Str(Projectile).Byte(Rounds).Vec(Origin).Vec(Velocity);
}

/// <summary>The weapons in a player's active slots 0-2 (stream 19); empty when dead.</summary>
public sealed record EvWeapons(uint Seq, uint Tick, byte Player, IReadOnlyList<AdminWeaponSlot> Slots) : AdminEvent(Seq, Tick)
{
    public override AdminMessageType Type => AdminMessageType.EvWeapons;

    public static EvWeapons Read(ref AdminReader r)
    {
        var seq = r.U32();
        var tick = r.U32();
        var player = r.U8();
        var slots = new AdminWeaponSlot[r.U8()];
        for (var i = 0; i < slots.Length; i++)
            slots[i] = new AdminWeaponSlot(r.U8(), r.Str());
        return new EvWeapons(seq, tick, player, slots);
    }

    protected override void WriteFields(AdminWriter w)
    {
        w.Byte(Player).Byte((byte)Slots.Count);
        foreach (var s in Slots) w.Byte(s.Slot).Str(s.Template);
    }
}

/// <summary>
/// One collision the server handed to a subscribed projectile (stream 20): the part touched (a hull, a child part such
/// as a rotor or a dummy missile, a soldier; Kind 0 for terrain), whether it was tested at its rewound pose, the pose
/// it was tested at and the root's pose now. Damage is booked to the vehicle whatever part was hit.
/// </summary>
public sealed record EvProjectileCollision(uint Seq, uint Tick, uint Id, string Template, short Shooter, byte Kind, byte Flags,
    uint RootNetId, uint RootConsoleId, string RootTemplate, string PartTemplate, short PartPlayer, Vec3 Position, Vec3 Normal,
    Vec3 Speed, ushort Material, float Latency, Vec3 PartPosition, Vec3 PartRotation, Vec3 RootPosition, Vec3 RootRotation)
    : AdminEvent(Seq, Tick)
{
    public const byte KindTerrain = 0, KindObject = 1;
    public const byte FlagRoot = 1, FlagSoldier = 2, FlagRewound = 4;

    public override AdminMessageType Type => AdminMessageType.EvProjectileCollision;

    protected override void WriteFields(AdminWriter w) =>
        w.U32(Id).Str(Template).I16(Shooter).Byte(Kind).Byte(Flags).U32(RootNetId).U32(RootConsoleId).Str(RootTemplate)
            .Str(PartTemplate).I16(PartPlayer).Vec(Position).Vec(Normal).Vec(Speed).U16(Material).F32(Latency)
            .Vec(PartPosition).Vec(PartRotation).Vec(RootPosition).Vec(RootRotation);
}

/// <summary>
/// A subscribed projectile's whole flight (stream 20), sent once when it ends. Sample k is the state at the end of tick
/// FirstTick + k; the sweep from sample k to k + 1 meets each target at sample k's tested pose.
/// </summary>
public sealed record EvProjectileTrace(uint Seq, uint Tick, uint Id, string Template, short Shooter, uint FirstTick, byte Flags,
    IReadOnlyList<AdminTraceTarget> Targets, IReadOnlyList<AdminTraceSample> Samples) : AdminEvent(Seq, Tick)
{
    public const byte FlagCut = 1, FlagCollided = 2;

    public override AdminMessageType Type => AdminMessageType.EvProjectileTrace;

    public static EvProjectileTrace Read(ref AdminReader r)
    {
        var seq = r.U32();
        var tick = r.U32();
        var id = r.U32();
        var template = r.Str();
        var shooter = r.I16();
        var firstTick = r.U32();
        var flags = r.U8();
        var targets = new AdminTraceTarget[r.U8()];
        for (var i = 0; i < targets.Length; i++)
            targets[i] = new AdminTraceTarget(r.U32(), r.Str());
        var samples = new AdminTraceSample[r.U16()];
        for (var i = 0; i < samples.Length; i++)
        {
            var position = r.Vec();
            var velocity = r.Vec();
            var lag = r.F32();
            var poses = new AdminTracePose[targets.Length];
            for (var k = 0; k < poses.Length; k++)
                poses[k] = new AdminTracePose(r.Vec(), r.Vec(), r.Vec(), r.Vec());
            samples[i] = new AdminTraceSample(position, velocity, lag, poses);
        }
        return new EvProjectileTrace(seq, tick, id, template, shooter, firstTick, flags, targets, samples);
    }

    protected override void WriteFields(AdminWriter w)
    {
        w.U32(Id).Str(Template).I16(Shooter).U32(FirstTick).Byte(Flags).Byte((byte)Targets.Count);
        foreach (var t in Targets) w.U32(t.NetId).Str(t.Template);
        w.U16((ushort)Samples.Count);
        foreach (var s in Samples)
        {
            w.Vec(s.Position).Vec(s.Velocity).F32(s.Lag);
            foreach (var p in s.Targets) w.Vec(p.Position).Vec(p.Rotation).Vec(p.TestedPosition).Vec(p.TestedRotation);
        }
    }
}

/// <summary>
/// A game script ended. Result is the JSON text of the script's finish value (empty without one) for
/// <see cref="ReasonFinished"/>, the error message with its stack for <see cref="ReasonError"/> and
/// <see cref="ReasonOverBudget"/>, and empty otherwise. The CPU times are the game thread's, in microseconds.
/// </summary>
public sealed record EvScriptFinished(uint Seq, uint Tick, uint Instance, string Name, byte Reason, string Result, uint Calls,
    uint CpuMicroseconds, uint MaxCallMicroseconds) : AdminEvent(Seq, Tick)
{
    public const byte ReasonFinished = 1, ReasonStopped = 2, ReasonError = 3, ReasonOverBudget = 4, ReasonLevelUnload = 5;

    public override AdminMessageType Type => AdminMessageType.EvScriptFinished;

    protected override void WriteFields(AdminWriter w) =>
        w.U32(Instance).Str(Name).Byte(Reason).Str(Result).U32(Calls).U32(CpuMicroseconds).U32(MaxCallMicroseconds);
}

/// <summary>What a running game script emitted (JSON text) or logged (one line).</summary>
public sealed record EvScriptEvent(uint Seq, uint Tick, uint Instance, byte Kind, string Payload) : AdminEvent(Seq, Tick)
{
    public const byte KindData = 1, KindLog = 2;

    public override AdminMessageType Type => AdminMessageType.EvScriptEvent;
    protected override void WriteFields(AdminWriter w) => w.U32(Instance).Byte(Kind).Str(Payload);
}

// ---------------------------------------------------------------- commands (WebAdmin -> module)

public abstract record AdminCommand(uint RequestId) : AdminMessage, IAdminRequest;

public sealed record CmdConsole(uint RequestId, string Line) : AdminCommand(RequestId)
{
    public override AdminMessageType Type => AdminMessageType.CmdConsole;
    public override void Write(AdminWriter w) => w.U32(RequestId).Str(Line);
}

public sealed record CmdSay(uint RequestId, byte Target, byte TeamOrPlayer, string Text) : AdminCommand(RequestId)
{
    public const byte TargetAll = 0, TargetTeam = 1, TargetPlayer = 2;
    public override AdminMessageType Type => AdminMessageType.CmdSay;
    public override void Write(AdminWriter w) => w.U32(RequestId).Byte(Target).Byte(TeamOrPlayer).Str(Text);
}

public sealed record CmdTeleport(uint RequestId, ObjectRef Target, byte Flags, Vec3 Position, Vec3 Rotation) : AdminCommand(RequestId)
{
    public const byte FlagPosition = 1, FlagRotation = 2;
    public override AdminMessageType Type => AdminMessageType.CmdTeleport;

    public override void Write(AdminWriter w)
    {
        w.U32(RequestId);
        Target.Write(w);
        w.Byte(Flags).Vec(Position).Vec(Rotation);
    }
}

public sealed record CmdSetHealth(uint RequestId, ObjectRef Target, byte Flags, float Value) : AdminCommand(RequestId)
{
    public const byte FlagFraction = 1;
    public override AdminMessageType Type => AdminMessageType.CmdSetHealth;

    public override void Write(AdminWriter w)
    {
        w.U32(RequestId);
        Target.Write(w);
        w.Byte(Flags).F32(Value);
    }
}

public sealed record CmdKillPlayer(uint RequestId, byte Player, byte Flags) : AdminCommand(RequestId)
{
    public const byte FlagDestroyVehicle = 1;
    public override AdminMessageType Type => AdminMessageType.CmdKillPlayer;
    public override void Write(AdminWriter w) => w.U32(RequestId).Byte(Player).Byte(Flags);
}

public sealed record CmdSetTeam(uint RequestId, byte Player, byte Team, byte Flags) : AdminCommand(RequestId)
{
    public const byte FlagKill = 1;
    public override AdminMessageType Type => AdminMessageType.CmdSetTeam;
    public override void Write(AdminWriter w) => w.U32(RequestId).Byte(Player).Byte(Team).Byte(Flags);
}

public sealed record CmdSetScore(uint RequestId, byte Player, byte Mask, int Score, int TeamScore, int Kills, int Deaths, int TeamKills)
    : AdminCommand(RequestId)
{
    public const byte MaskScore = 1, MaskTeamScore = 2, MaskKills = 4, MaskDeaths = 8, MaskTeamKills = 16;
    public override AdminMessageType Type => AdminMessageType.CmdSetScore;
    public override void Write(AdminWriter w) => w.U32(RequestId).Byte(Player).Byte(Mask).I32(Score).I32(TeamScore).I32(Kills).I32(Deaths).I32(TeamKills);
}

public sealed record CmdRank(uint RequestId, byte Player, byte Rank, byte Flags) : AdminCommand(RequestId)
{
    public override AdminMessageType Type => AdminMessageType.CmdRank;
    public override void Write(AdminWriter w) => w.U32(RequestId).Byte(Player).Byte(Rank).Byte(Flags);
}

public sealed record CmdMedal(uint RequestId, byte Player, int Medal, int Value) : AdminCommand(RequestId)
{
    public override AdminMessageType Type => AdminMessageType.CmdMedal;
    public override void Write(AdminWriter w) => w.U32(RequestId).Byte(Player).I32(Medal).I32(Value);
}

public sealed record CmdGameEvent(uint RequestId, byte Player, byte Event, int Data) : AdminCommand(RequestId)
{
    public override AdminMessageType Type => AdminMessageType.CmdGameEvent;
    public override void Write(AdminWriter w) => w.U32(RequestId).Byte(Player).Byte(Event).I32(Data);
}

public sealed record CmdHudEvent(uint RequestId, byte Player, byte Event, int Data) : AdminCommand(RequestId)
{
    public override AdminMessageType Type => AdminMessageType.CmdHudEvent;
    public override void Write(AdminWriter w) => w.U32(RequestId).Byte(Player).Byte(Event).I32(Data);
}

public sealed record CmdKick(uint RequestId, byte Player, string Reason) : AdminCommand(RequestId)
{
    public override AdminMessageType Type => AdminMessageType.CmdKick;
    public override void Write(AdminWriter w) => w.U32(RequestId).Byte(Player).Str(Reason);
}

public sealed record CmdBan(uint RequestId, byte Player, byte By, byte Duration, uint Seconds, string Reason) : AdminCommand(RequestId)
{
    public const byte ByAddress = 0, ByCdKey = 1;
    public const byte DurationPermanent = 0, DurationRound = 1, DurationSeconds = 2;
    public override AdminMessageType Type => AdminMessageType.CmdBan;
    public override void Write(AdminWriter w) => w.U32(RequestId).Byte(Player).Byte(By).Byte(Duration).U32(Seconds).Str(Reason);
}

public sealed record CmdMap(uint RequestId, byte Action, ushort Index, string Name, string Mode, ushort Size) : AdminCommand(RequestId)
{
    public const byte ActionRestart = 1, ActionNext = 2, ActionIndex = 3, ActionByName = 4;
    public override AdminMessageType Type => AdminMessageType.CmdMap;
    public override void Write(AdminWriter w) => w.U32(RequestId).Byte(Action).U16(Index).Str(Name).Str(Mode).U16(Size);
}

public sealed record CmdListObjects(uint RequestId, string Template, ushort MaxEntries) : AdminCommand(RequestId)
{
    public override AdminMessageType Type => AdminMessageType.CmdListObjects;
    public override void Write(AdminWriter w) => w.U32(RequestId).Str(Template).U16(MaxEntries);
}

public sealed record CmdSetObjectActive(uint RequestId, ObjectRef Target) : AdminCommand(RequestId)
{
    public override AdminMessageType Type => AdminMessageType.CmdSetObjectActive;

    public override void Write(AdminWriter w)
    {
        w.U32(RequestId);
        Target.Write(w);
    }
}

public sealed record CmdSpawnObject(uint RequestId, string Template, Vec3 Position, Vec3 Rotation, byte Team) : AdminCommand(RequestId)
{
    public override AdminMessageType Type => AdminMessageType.CmdSpawnObject;
    public override void Write(AdminWriter w) => w.U32(RequestId).Str(Template).Vec(Position).Vec(Rotation).Byte(Team);
}

public sealed record CmdDeleteObject(uint RequestId, ObjectRef Target) : AdminCommand(RequestId)
{
    public override AdminMessageType Type => AdminMessageType.CmdDeleteObject;

    public override void Write(AdminWriter w)
    {
        w.U32(RequestId);
        Target.Write(w);
    }
}

/// <summary>
/// Runs a game script (JavaScript source) on the server. Instance is WebAdmin's id for it, not 0 and not a running
/// script's. Arguments is JSON text, or empty for none. Only sent to a module with <see cref="Welcome.CapScripts"/>.
/// </summary>
public sealed record CmdStartScript(uint RequestId, uint Instance, byte ApiVersion, byte Flags, string Name, string Arguments, string Source)
    : AdminCommand(RequestId)
{
    /// <summary>The script API version WebAdmin's scripts are written for.</summary>
    public const byte Api = 1;

    public const int MaxNameBytes = 64, MaxSourceBytes = 60000, MaxMessageBytes = 8192;

    public override AdminMessageType Type => AdminMessageType.CmdStartScript;
    public override void Write(AdminWriter w) => w.U32(RequestId).U32(Instance).Byte(ApiVersion).Byte(Flags).Str(Name).Str(Arguments).Str(Source);
}

public sealed record CmdStopScript(uint RequestId, uint Instance) : AdminCommand(RequestId)
{
    public override AdminMessageType Type => AdminMessageType.CmdStopScript;
    public override void Write(AdminWriter w) => w.U32(RequestId).U32(Instance);
}

/// <summary>A message (JSON text) for a running script's message handlers.</summary>
public sealed record CmdScriptMessage(uint RequestId, uint Instance, string Message) : AdminCommand(RequestId)
{
    public override AdminMessageType Type => AdminMessageType.CmdScriptMessage;
    public override void Write(AdminWriter w) => w.U32(RequestId).U32(Instance).Str(Message);
}

public sealed record CmdListScripts(uint RequestId) : AdminCommand(RequestId)
{
    public override AdminMessageType Type => AdminMessageType.CmdListScripts;
    public override void Write(AdminWriter w) => w.U32(RequestId);
}

/// <summary>
/// The seat a player wants when teammates spawn into the same vehicle together: the module spawns them in that order
/// (0 the first seat, a heli's pilot; 1 the second, its gunner; <see cref="None"/> clears it). Kept by the module until
/// the player disconnects. Only sent to a module with <see cref="Welcome.CapSeats"/>.
/// </summary>
public sealed record CmdSeatPreference(uint RequestId, byte Player, byte Seat) : AdminCommand(RequestId)
{
    public const byte First = 0, Second = 1, None = 255;
    public override AdminMessageType Type => AdminMessageType.CmdSeatPreference;
    public override void Write(AdminWriter w) => w.U32(RequestId).Byte(Player).Byte(Seat);
}

// ---------------------------------------------------------------- replies and keepalive

public sealed record OkReply(uint RequestId) : AdminMessage, IAdminRequest
{
    public override AdminMessageType Type => AdminMessageType.Ok;
    public override void Write(AdminWriter w) => w.U32(RequestId);
}

public sealed record ConsoleResult(uint RequestId, byte Flags, IReadOnlyList<string> Lines) : AdminMessage, IAdminRequest
{
    public const byte FlagTruncated = 1;
    public override AdminMessageType Type => AdminMessageType.ConsoleResult;

    public static ConsoleResult Read(ref AdminReader r)
    {
        var request = r.U32();
        var flags = r.U8();
        var lines = new string[r.U16()];
        for (var i = 0; i < lines.Length; i++)
            lines[i] = r.Str();
        return new ConsoleResult(request, flags, lines);
    }

    public override void Write(AdminWriter w)
    {
        w.U32(RequestId).Byte(Flags).U16((ushort)Lines.Count);
        foreach (var line in Lines) w.Str(line);
    }
}

public sealed record ObjectList(uint RequestId, byte Flags, IReadOnlyList<ObjectListEntry> Objects) : AdminMessage, IAdminRequest
{
    public const byte FlagMore = 1;
    public override AdminMessageType Type => AdminMessageType.ObjectList;

    public static ObjectList Read(ref AdminReader r)
    {
        var request = r.U32();
        var flags = r.U8();
        var objects = new ObjectListEntry[r.U16()];
        for (var i = 0; i < objects.Length; i++)
            objects[i] = new ObjectListEntry(r.U32(), r.U32(), r.Str(), r.Vec(), r.Vec(), r.F32(), r.U8());
        return new ObjectList(request, flags, objects);
    }

    public override void Write(AdminWriter w)
    {
        w.U32(RequestId).Byte(Flags).U16((ushort)Objects.Count);
        foreach (var o in Objects)
            w.U32(o.ConsoleId).U32(o.NetId).Str(o.Template).Vec(o.Position).Vec(o.Rotation).F32(o.Health).Byte(o.Flags);
    }
}

public sealed record ObjectSpawned(uint RequestId, uint ConsoleId, uint NetId) : AdminMessage, IAdminRequest
{
    public override AdminMessageType Type => AdminMessageType.ObjectSpawned;
    public override void Write(AdminWriter w) => w.U32(RequestId).U32(ConsoleId).U32(NetId);
}

/// <summary>The scripts that run now, in start order (the reply to ListScripts).</summary>
public sealed record ScriptList(uint RequestId, IReadOnlyList<(uint Instance, string Name)> Scripts) : AdminMessage, IAdminRequest
{
    public override AdminMessageType Type => AdminMessageType.ScriptList;

    public static ScriptList Read(ref AdminReader r)
    {
        var request = r.U32();
        var scripts = new (uint, string)[r.U8()];
        for (var i = 0; i < scripts.Length; i++)
            scripts[i] = (r.U32(), r.Str());
        return new ScriptList(request, scripts);
    }

    public override void Write(AdminWriter w)
    {
        w.U32(RequestId).Byte((byte)Scripts.Count);
        foreach (var (instance, name) in Scripts)
            w.U32(instance).Str(name);
    }
}

public sealed record Ping(uint RequestId, ulong Clock) : AdminMessage, IAdminRequest
{
    public override AdminMessageType Type => AdminMessageType.Ping;
    public override void Write(AdminWriter w) => w.U32(RequestId).U64(Clock);
}

public sealed record Pong(uint RequestId, ulong Clock) : AdminMessage, IAdminRequest
{
    public override AdminMessageType Type => AdminMessageType.Pong;
    public override void Write(AdminWriter w) => w.U32(RequestId).U64(Clock);
}

public sealed record ErrorMessage(uint RequestId, AdminErrorCode Code, string Message) : AdminMessage, IAdminRequest
{
    public override AdminMessageType Type => AdminMessageType.Error;
    public override void Write(AdminWriter w) => w.U32(RequestId).U16((ushort)Code).Str(Message);
}
