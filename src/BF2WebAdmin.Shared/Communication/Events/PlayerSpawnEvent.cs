﻿using BF2WebAdmin.Shared.Communication.DTOs;

namespace BF2WebAdmin.Shared.Communication.Events
{
    //[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    //[MessagePackObject(keyAsPropertyName: true)]
    public class PlayerSpawnEvent : IMessagePayload
    {
        public int PlayerId { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Rotation { get; set; }
        public DateTimeOffset TimeStamp { get; set; }
    }
}