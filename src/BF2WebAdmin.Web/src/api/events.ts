import type { GameState, Message, Player, Server, SocketState, Team, Vector3 } from './client'

// The events of the server hub (/hubs/server), as sent by BF2WebAdmin.Server's IServerHubClient
// (src/BF2WebAdmin.Server/Hubs/ServerHub.cs; the classes are in src/BF2WebAdmin.Shared/Communication/Events).
// They are not in the OpenAPI document, so keep them in step by hand.

type Timed = { timeStamp: string }

// VehicleDto is only sent by the hub
type Vehicle = { rootVehicleId: number; rootVehicleTemplate: string; subVehicleTemplate: string }

export type HubEvents = {
  // Sent to all clients
  ServerUpdateEvent: Server & Timed
  ServerRemoveEvent: { serverId: string }

  // Sent to the clients subscribed to the server
  ChatEvent: { message: Message } & Timed
  GameStateEvent: { state: GameState } & Timed
  MapChangeEvent: { map: string; size: number; index: number; teams: Team[] } & Timed
  PlayerDeathEvent: { playerId: number; position: Vector3 } & Timed
  PlayerJoinEvent: { player: Player } & Timed
  PlayerKillEvent: { attackerId: number; attackerPosition: Vector3; victimId: number; victimPosition: Vector3; weapon: string } & Timed
  PlayerLeftEvent: { playerId: number } & Timed
  PlayerPositionEvent: { playerId: number; position: Vector3; rotation: Vector3; ping: number } & Timed
  PlayerScoreEvent: { playerId: number; teamScore: number; kills: number; deaths: number; totalScore: number } & Timed
  PlayerSpawnEvent: { playerId: number; position: Vector3; rotation: Vector3 } & Timed
  PlayerTeamEvent: { playerId: number; teamId: number } & Timed
  PlayerVehicleEvent: { playerId: number; vehicle: Vehicle | null } & Timed
  ProjectilePositionEvent: { projectileId: number; template: string; position: Vector3; rotation: Vector3 } & Timed
  SocketStateEvent: { state: SocketState } & Timed
}

export type HubEventName = keyof HubEvents
