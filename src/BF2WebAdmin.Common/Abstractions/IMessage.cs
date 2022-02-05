//using BF2WebAdmin.Common.Communication.Messages;
//using MessagePack;
//using ProtoBuf;

//namespace BF2WebAdmin.Common.Abstractions
//{
//    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
//    public interface IMessage
//    {
//        string Type { get; }
//        string ServerId { get; }
//        IMessagePayload Payload { get; set; }
//    }

//    [
//        Union(1, typeof(PlayerJoinEvent)),
//        Union(2, typeof(PlayerLeftEvent)),
//        Union(3, typeof(PlayerPositionEvent)),
//        Union(4, typeof(PlayerVehicleEvent)),
//        Union(5, typeof(PlayerKillEvent)),
//        Union(6, typeof(PlayerDeathEvent)),
//        Union(7, typeof(PlayerSpawnEvent)),
//        Union(8, typeof(PlayerTeamEvent)),
//        Union(9, typeof(PlayerScoreEvent)),
//        Union(10, typeof(ProjectilePositionEvent)),
//        Union(11, typeof(ChatEvent)),
//        Union(12, typeof(ServerUpdateEvent)),
//        Union(13, typeof(GameStateEvent)),
//        Union(14, typeof(MapChangeEvent)),
//        Union(101, typeof(UserConnectAction)),
//        Union(102, typeof(UserDisconnectAction)),

//        ProtoInclude(1, typeof(PlayerJoinEvent)),
//        ProtoInclude(2, typeof(PlayerLeftEvent)),
//        ProtoInclude(3, typeof(PlayerPositionEvent)),
//        ProtoInclude(4, typeof(PlayerVehicleEvent)),
//        ProtoInclude(5, typeof(PlayerKillEvent)),
//        ProtoInclude(6, typeof(PlayerDeathEvent)),
//        ProtoInclude(7, typeof(PlayerSpawnEvent)),
//        ProtoInclude(8, typeof(PlayerTeamEvent)),
//        ProtoInclude(9, typeof(PlayerScoreEvent)),
//        ProtoInclude(10, typeof(ProjectilePositionEvent)),
//        ProtoInclude(11, typeof(ChatEvent)),
//        ProtoInclude(12, typeof(ServerUpdateEvent)),
//        ProtoInclude(13, typeof(GameStateEvent)),
//        ProtoInclude(14, typeof(MapChangeEvent)),
//        ProtoInclude(101, typeof(UserConnectAction)),
//        ProtoInclude(102, typeof(UserDisconnectAction)),

//        ProtoContract(ImplicitFields = ImplicitFields.AllPublic)
//    ]
//    public interface IMessagePayload { }
//}