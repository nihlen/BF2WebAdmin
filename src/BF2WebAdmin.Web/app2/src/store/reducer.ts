import { combineReducers } from "redux";
import { ISerializer, JsonSerializer } from "./serialization";
import {
  ChatEvent,
  GameStateEvent,
  IMessage,
  MapChangeEvent,
  PlayerDeathEvent,
  PlayerJoinEvent,
  PlayerKillEvent,
  PlayerLeftEvent,
  PlayerPositionEvent,
  PlayerSpawnEvent,
  PlayerTeamEvent,
  PlayerVehicleEvent,
  ProjectilePositionEvent,
  ServerUpdateEvent
} from "./protos/protos";

export type Action =
  | { type: "WEBSOCKET_START" }
  | { type: "WEBSOCKET_STOP" }
  | { type: "WEBSOCKET_MESSAGE"; payload: string };

const serializer: ISerializer<any> = new JsonSerializer();

export interface GameServer {
  id: string;
  name: string;
  ipAddress: string;
  gamePort: number;
  queryPort: number;
  map: string;
  state: string;
  players: Player[];
  projectiles: Projectile[];
  chat: ChatMessage[];
}

export interface ChatMessage {
  playerId: number;
  name: string;
  channel: string;
  time: Date;
  text: string;
}

export interface Player {
  index: number;
  name: string;
  team: number;
  teamScore: number;
  kills: number;
  deaths: number;
  totalScore: number;
  ping: number;
  position: number[];
  rotation: number[];
  isAlive: boolean;
  vehicleTemplate: string;
  vehicleId: number;

  // For interpolation
  previousPosition: number[];
  previousRotation: number[];
  updateTimestamp: number;
}

export interface Projectile {
  projectileId: number;
  template: string;
  position: number[];
  rotation: number[];
  positionHistory: number[][];

  // For interpolation
  previousPosition: number[];
  previousRotation: number[];
  updateTimestamp: number;
}

export interface State {
  readonly isConnected: boolean;
  readonly servers: GameServer[];
}

export const reducer = combineReducers<State>({
  isConnected: (state = false, action: Action) => {
    switch (action.type) {
      case "WEBSOCKET_START":
        return true;
      case "WEBSOCKET_STOP":
        return false;

      default:
        return state;
    }
  },
  servers: (state = [], action: Action) => {
    switch (action.type) {
      case "WEBSOCKET_MESSAGE":
        return parseGameEvent(state, action);

      default:
        return state;
    }
  }
});

export default reducer;

const parseGameEvent = (state: GameServer[], action: any): GameServer[] => {
  const servers = [...state];

  const message = serializer.deserialize<IMessage>(action.payload);
  const otherServers = servers.filter(s => s.id !== message.ServerId);
  let server = servers.find(s => s.id === message.ServerId);

  // console.log('action', action);
  // console.log('message', message);

  if (!server) {
    server = {
      id: message.ServerId,
      ipAddress: "",
      map: "",
      state: "",
      name: "",
      gamePort: 0,
      queryPort: 0,
      players: [],
      projectiles: [],
      chat: []
    };
    servers.push(server);
  }

  switch (message.Type) {
    case "ServerUpdateEvent":
      return [...otherServers, handleServerUpdateEvent(message.Payload as ServerUpdateEvent, server)];

    case "PlayerJoinEvent":
      return [...otherServers, handlePlayerJoinEvent(message.Payload as PlayerJoinEvent, server)];

    case "PlayerLeftEvent":
      return [...otherServers, handlePlayerLeftEvent(message.Payload as PlayerLeftEvent, server)];

    case "PlayerPositionEvent":
      return [...otherServers, handlePlayerPositionEvent(message.Payload as PlayerPositionEvent, server)];

    case "PlayerVehicleEvent":
      return [...otherServers, handlePlayerVehicleEvent(message.Payload as PlayerVehicleEvent, server)];

    case "PlayerKillEvent":
      return [...otherServers, handlePlayerKillEvent(message.Payload as PlayerKillEvent, server)];

    case "PlayerDeathEvent":
      return [...otherServers, handlePlayerDeathEvent(message.Payload as PlayerDeathEvent, server)];

    case "PlayerSpawnEvent":
      return [...otherServers, handlePlayerSpawnEvent(message.Payload as PlayerSpawnEvent, server)];

    case "PlayerTeamEvent":
      return [...otherServers, handlePlayerTeamEvent(message.Payload as PlayerTeamEvent, server)];

    case "ProjectilePositionEvent":
      return [...otherServers, handleProjectilePositionEvent(message.Payload as ProjectilePositionEvent, server)];

    case "ChatEvent":
      return [...otherServers, handleChatEvent(message.Payload as ChatEvent, server)];

    case "GameStateEvent":
      return [...otherServers, handleGameStateEvent(message.Payload as GameStateEvent, server)];

    case "MapChangeEvent":
      return [...otherServers, handleMapChangeEvent(message.Payload as MapChangeEvent, server)];

    default:
      console.log("Unknown game event type: ", message.Type);
  }

  return servers;
};

const handleServerUpdateEvent = (event: ServerUpdateEvent, server: GameServer) => {
  return {
    ...server,
    id: event.Id,
    ipAddress: event.IpAddress,
    gamePort: event.GamePort,
    queryPort: event.QueryPort,
    name: event.Name,
    map: event.Map
    // players: event.Players;
    // maxPlayers: event.MaxPlayers;
  };
};

const handlePlayerJoinEvent = (event: PlayerJoinEvent, server: GameServer) => {
  // const player = server.players.find(p => p.index === event.Player!.Index);
  return {
    ...server,
    players: [
      ...server.players,
      {
        index: event.Player!.Index || 0,
        name: event.Player!.Name || "",
        team: event.Player!.Team || 0,
        isAlive: event.Player!.IsAlive || false,
        position: [0, 0, 0],
        rotation: [0, 0, 0],
        teamScore: event.Player!.Score!.Team || 0,
        kills: event.Player!.Score!.Kills || 0,
        deaths: event.Player!.Score!.Deaths || 0,
        totalScore: event.Player!.Score!.Total || 0,
        ping: event.Player!.Score!.Ping || 0,
        vehicleTemplate: "",
        vehicleId: 0,
        previousPosition: /*(player && player.position) ||*/ [],
        previousRotation: /*(player && player.rotation) ||*/ [],
        updateTimestamp: 0
      }
    ]
  };
};

const handlePlayerLeftEvent = (event: PlayerLeftEvent, server: GameServer) => {
  return {
    ...server,
    players: [...server.players.filter(p => p.index !== event.PlayerId)]
  };
};

const handlePlayerPositionEvent = (event: PlayerPositionEvent, server: GameServer) => {
  const player = server.players.find(p => p.index === event.PlayerId);
  return {
    ...server,
    players: [
      ...server.players.filter(p => p.index !== event.PlayerId),
      {
        ...player!,
        position: [event.Position!.X || 0, event.Position!.Y || 0, event.Position!.Z || 0],
        rotation: [event.Rotation!.X || 0, event.Rotation!.Y || 0, event.Rotation!.Z || 0],
        ping: event.Ping,
        updateTimestamp: window.performance.now()
      }
    ]
  };
};

const handlePlayerVehicleEvent = (event: PlayerVehicleEvent, server: GameServer) => {
  const player = server.players.find(p => p.index === event.PlayerId);
  return {
    ...server,
    players: [
      ...server.players.filter(p => p.index !== event.PlayerId),
      {
        ...player!,
        vehicleId: event.Vehicle!.RootVehicleId || 0,
        vehicleTemplate: event.Vehicle!.RootVehicleTemplate || ""
      }
    ]
  };
};

const handlePlayerKillEvent = (event: PlayerKillEvent, server: GameServer) => {
  const attacker = server.players.find(p => p.index === event.AttackerId);
  const victim = server.players.find(p => p.index === event.VictimId);
  console.log(`${attacker!.name} killed ${victim!.name}`);
  return {
    ...server,
    players: [
      ...server.players.filter(p => p.index !== event.VictimId),
      {
        ...victim!,
        isAlive: false
      }
    ]
  };
};

const handlePlayerDeathEvent = (event: PlayerDeathEvent, server: GameServer) => {
  const player = server.players.find(p => p.index === event.PlayerId);
  console.log(`${player!.name} died`);
  return {
    ...server,
    players: [
      ...server.players.filter(p => p.index !== event.PlayerId),
      {
        ...player!,
        isAlive: false
      }
    ],
    // TODO: Find a better way to clear projectiles
    projectiles: []
  };
};

const handlePlayerSpawnEvent = (event: PlayerSpawnEvent, server: GameServer) => {
  const player = server.players.find(p => p.index === event.PlayerId);
  console.log(`${player!.name} spawned`);
  return {
    ...server,
    players: [
      ...server.players.filter(p => p.index !== event.PlayerId),
      {
        ...player!,
        isAlive: true
      }
    ]
  };
};

const handlePlayerTeamEvent = (event: PlayerTeamEvent, server: GameServer) => {
  const player = server.players.find(p => p.index === event.PlayerId);
  console.log(`${player!.name} joined team ${event.TeamId}`);
  return {
    ...server,
    players: [
      ...server.players.filter(p => p.index !== event.PlayerId),
      {
        ...player!,
        team: event.TeamId
      }
    ]
  };
};

const handleProjectilePositionEvent = (event: ProjectilePositionEvent, server: GameServer) => {
  const projectile = server.projectiles.find(p => p.projectileId === event.ProjectileId);
  return {
    ...server,
    projectiles: [
      ...server.projectiles.filter(p => p.projectileId !== event.ProjectileId),
      {
        projectileId: event.ProjectileId,
        template: event.Template,
        position: [event.Position!.X || 0, event.Position!.Y || 0, event.Position!.Z || 0],
        rotation: [event.Rotation!.X || 0, event.Rotation!.Y || 0, event.Rotation!.Z || 0],
        positionHistory: [
          ...((projectile && projectile.positionHistory) || []),
          [event.Position!.X || 0, event.Position!.Y || 0, event.Position!.Z || 0]
        ],
        previousPosition: /*(projectile && projectile.position) ||*/ [],
        previousRotation: /*(projectile && projectile.rotation) ||*/ [],
        updateTimestamp: window.performance.now()
      }
    ]
  };
};

const handleChatEvent = (event: ChatEvent, server: GameServer) => {
  const player = server.players.find(p => p.index === event.Message!.PlayerId);
  return {
    ...server,
    chat: [
      ...server.chat,
      {
        playerId: event.Message!.PlayerId || 0,
        name: player ? player.name : "?",
        channel: event.Message!.Channel || "",
        text: event.Message!.Text || "",
        time: new Date(event.Message!.Time || "")
      }
    ]
  };
};

const handleGameStateEvent = (event: GameStateEvent, server: GameServer) => {
  return {
    ...server,
    state: event.State
  };
};

const handleMapChangeEvent = (event: MapChangeEvent, server: GameServer) => {
  return {
    ...server,
    map: event.Map
  };
};
