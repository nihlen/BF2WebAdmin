import type { CommandLogEntry, GameState, Message, MissileSummary, Player, Server, SocketState, Team, Vector3 } from './client'

// The events of the server hub (/hubs/server), as sent by BF2WebAdmin.Server's IServerHubClient
// (src/BF2WebAdmin.Server/Hubs/ServerHub.cs; the classes are in src/BF2WebAdmin.Shared/Communication/Events).
// They are not in the OpenAPI document, so keep them in step by hand.

type Timed = { timeStamp: string }

// VehicleDto is only sent by the hub
type Vehicle = { rootVehicleId: number; rootVehicleTemplate: string; subVehicleTemplate: string }

// The 3D viewer's frames (ViewerFrameDto in src/BF2WebAdmin.Server/AdminChannel/ViewerFeed.cs). Vectors are [x, y, z] in
// BF2 coordinates (y up), rotations [yaw, pitch, roll] in degrees
type Vec = number[]
export type ViewerPlayer = { id: number; team: number; life: number; flags: number; root: number; seat: number; view: number; lag: number; ping: number; p: Vec; r: Vec; v: Vec }
export type ViewerVehicle = { id: number; template: string; team: number; health: number; p: Vec; r: Vec }
export type ViewerAim = { id: number; flags: number; cp: Vec; cr: Vec; wp: Vec; wr: Vec }
export type ViewerProjectile = { id: number; template: string; owner: number; p: Vec; r: Vec; v: Vec }
/** One admin channel stream frame: one of the lists is set. Vehicles only has the ones that changed, unless keyframe */
export type ViewerFrame = {
  tick: number
  time: number
  keyframe: boolean
  players: ViewerPlayer[] | null
  vehicles: ViewerVehicle[] | null
  aims: ViewerAim[] | null
  projectiles: ViewerProjectile[] | null
}

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
  // A web user sent a command (CommandLog in src/BF2WebAdmin.Server/Api/CommandLog.cs); the type is in the OpenAPI document
  CommandLogEvent: CommandLogEntry

  // Sent to the clients with the 3D viewer open on the server (SubscribeViewer)
  ViewerFrame: ViewerFrame
  MissileEvent: MissileSummary
}

export type HubEventName = keyof HubEvents
