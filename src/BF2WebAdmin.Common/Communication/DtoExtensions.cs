using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.Shared.Communication.DTOs;

namespace BF2WebAdmin.Common.Communication;

public static class MessageExtensions
{
    public static MessageDto ToDto(this Message message)
    {
        return new MessageDto
        {
            PlayerId = message.Player?.Index,
            PlayerName = message.Player?.Name,
            TeamName = message.Player?.Team?.Name,
            Type = message.Type.ToString(),
            Channel = message.Channel,
            Flags = message.Flags,
            Text = message.Text
        };
    }

    public static PlayerDto ToDto(this Player player)
    {
        return new PlayerDto
        {

            Name = player.Name,
            Index = player.Index,
            IpAddress = player.IpAddress.ToString(),
            Hash = player.Hash,
            Country = player.Country.Code,
            Rank = (int)player.Rank,
            Team = player.Team.Id,
            IsAlive = player.IsAlive,
            Score = player.Score.ToDto()
        };
    }

    public static ScoreDto ToDto(this Score score)
    {
        return new ScoreDto
        {

            Total = score.Total,
            Team = score.Team,
            Kills = score.Kills,
            Deaths = score.Deaths,
            Ping = score.Ping
        };
    }

    public static Vector3 ToDto(this Position position)
    {
        return new Vector3
        {
            X = (float)position.X,
            Y = (float)position.Y,
            Z = (float)position.Height
        };
    }

    public static Vector3 ToDto(this Rotation rotation)
    {
        return new Vector3
        {
            X = (float)rotation.Yaw,
            Y = (float)rotation.Pitch,
            Z = (float)rotation.Roll
        };
    }

    public static VehicleDto ToDto(this Vehicle vehicle)
    {
        return new VehicleDto
        {
            RootVehicleId = vehicle?.RootVehicleId ?? 0,
            RootVehicleTemplate = vehicle?.RootVehicleTemplate,
            SubVehicleTemplate = vehicle?.Template,
        };
    }

    public static TeamDto ToDto(this Team team)
    {
        return new TeamDto
        {
            Id = team.Id,
            Name = team.Name
        };
    }

    public static MapDto ToDto(this Map map)
    {
        return new MapDto
        {
            Index = map.Index,
            Name = map.Name,
            Size = map.Size
        };
    }
}
