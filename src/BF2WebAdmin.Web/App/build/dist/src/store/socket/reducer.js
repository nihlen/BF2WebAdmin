var __assign = (this && this.__assign) || function () {
    __assign = Object.assign || function(t) {
        for (var s, i = 1, n = arguments.length; i < n; i++) {
            s = arguments[i];
            for (var p in s) if (Object.prototype.hasOwnProperty.call(s, p))
                t[p] = s[p];
        }
        return t;
    };
    return __assign.apply(this, arguments);
};
var __spreadArrays = (this && this.__spreadArrays) || function () {
    for (var s = 0, i = 0, il = arguments.length; i < il; i++) s += arguments[i].length;
    for (var r = Array(s), k = 0, i = 0; i < il; i++)
        for (var a = arguments[i], j = 0, jl = a.length; j < jl; j++, k++)
            r[k] = a[j];
    return r;
};
import { combineReducers } from 'redux';
import { WEBSOCKET_MESSAGE, WEBSOCKET_START, WEBSOCKET_STOP } from './';
import { JsonSerializer } from '../../serialization';
var serializer = new JsonSerializer();
export var reducer = combineReducers({
    isConnected: function (state, action) {
        if (state === void 0) { state = false; }
        switch (action.type) {
            case WEBSOCKET_START:
                return true;
            case WEBSOCKET_STOP:
                return false;
            default:
                return state;
        }
    },
    servers: function (state, action) {
        if (state === void 0) { state = []; }
        switch (action.type) {
            case WEBSOCKET_MESSAGE:
                return parseGameEvent(state, action);
            default:
                return state;
        }
    },
});
var parseGameEvent = function (state, action) {
    var servers = __spreadArrays(state);
    var message = serializer.deserialize(action.payload);
    var otherServers = servers.filter(function (s) { return s.id !== message.ServerId; });
    var server = servers.find(function (s) { return s.id === message.ServerId; });
    // console.log('action', action);
    // console.log('message', message);
    if (!server) {
        server = {
            id: message.ServerId,
            ipAddress: '',
            map: '',
            state: '',
            name: '',
            gamePort: 0,
            queryPort: 0,
            players: [],
            projectiles: [],
            chat: []
        };
        servers.push(server);
    }
    switch (message.Type) {
        case 'ServerUpdateEvent':
            return __spreadArrays(otherServers, [handleServerUpdateEvent(message.Payload, server)]);
        case 'PlayerJoinEvent':
            return __spreadArrays(otherServers, [handlePlayerJoinEvent(message.Payload, server)]);
        case 'PlayerLeftEvent':
            return __spreadArrays(otherServers, [handlePlayerLeftEvent(message.Payload, server)]);
        case 'PlayerPositionEvent':
            return __spreadArrays(otherServers, [handlePlayerPositionEvent(message.Payload, server)]);
        case 'PlayerVehicleEvent':
            return __spreadArrays(otherServers, [handlePlayerVehicleEvent(message.Payload, server)]);
        case 'PlayerKillEvent':
            return __spreadArrays(otherServers, [handlePlayerKillEvent(message.Payload, server)]);
        case 'PlayerDeathEvent':
            return __spreadArrays(otherServers, [handlePlayerDeathEvent(message.Payload, server)]);
        case 'PlayerSpawnEvent':
            return __spreadArrays(otherServers, [handlePlayerSpawnEvent(message.Payload, server)]);
        case 'PlayerTeamEvent':
            return __spreadArrays(otherServers, [handlePlayerTeamEvent(message.Payload, server)]);
        case 'ProjectilePositionEvent':
            return __spreadArrays(otherServers, [handleProjectilePositionEvent(message.Payload, server)]);
        case 'ChatEvent':
            return __spreadArrays(otherServers, [handleChatEvent(message.Payload, server)]);
        case 'GameStateEvent':
            return __spreadArrays(otherServers, [handleGameStateEvent(message.Payload, server)]);
        case 'MapChangeEvent':
            return __spreadArrays(otherServers, [handleMapChangeEvent(message.Payload, server)]);
        default:
            console.log('Unknown game event type: ', message.Type);
    }
    return servers;
};
var handleServerUpdateEvent = function (event, server) {
    return __assign(__assign({}, server), { id: event.Id, ipAddress: event.IpAddress, gamePort: event.GamePort, queryPort: event.QueryPort, name: event.Name, map: event.Map });
};
var handlePlayerJoinEvent = function (event, server) {
    // const player = server.players.find(p => p.index === event.Player!.Index);
    return __assign(__assign({}, server), { players: __spreadArrays(server.players, [
            {
                index: event.Player.Index || 0,
                name: event.Player.Name || '',
                team: event.Player.Team || 0,
                isAlive: event.Player.IsAlive || false,
                position: [0, 0, 0],
                rotation: [0, 0, 0],
                teamScore: event.Player.Score.Team || 0,
                kills: event.Player.Score.Kills || 0,
                deaths: event.Player.Score.Deaths || 0,
                totalScore: event.Player.Score.Total || 0,
                ping: event.Player.Score.Ping || 0,
                vehicleTemplate: '',
                vehicleId: 0,
                previousPosition: /*(player && player.position) ||*/ [],
                previousRotation: /*(player && player.rotation) ||*/ [],
                updateTimestamp: 0
            }
        ]) });
};
var handlePlayerLeftEvent = function (event, server) {
    return __assign(__assign({}, server), { players: __spreadArrays(server.players.filter(function (p) { return p.index !== event.PlayerId; })) });
};
var handlePlayerPositionEvent = function (event, server) {
    var player = server.players.find(function (p) { return p.index === event.PlayerId; });
    return __assign(__assign({}, server), { players: __spreadArrays(server.players.filter(function (p) { return p.index !== event.PlayerId; }), [
            __assign(__assign({}, player), { position: [event.Position.X || 0, event.Position.Y || 0, event.Position.Z || 0], rotation: [event.Rotation.X || 0, event.Rotation.Y || 0, event.Rotation.Z || 0], ping: event.Ping, updateTimestamp: window.performance.now() })
        ]) });
};
var handlePlayerVehicleEvent = function (event, server) {
    var player = server.players.find(function (p) { return p.index === event.PlayerId; });
    return __assign(__assign({}, server), { players: __spreadArrays(server.players.filter(function (p) { return p.index !== event.PlayerId; }), [
            __assign(__assign({}, player), { vehicleId: event.Vehicle.RootVehicleId || 0, vehicleTemplate: event.Vehicle.RootVehicleTemplate || '' })
        ]) });
};
var handlePlayerKillEvent = function (event, server) {
    var attacker = server.players.find(function (p) { return p.index === event.AttackerId; });
    var victim = server.players.find(function (p) { return p.index === event.VictimId; });
    console.log(attacker.name + " killed " + victim.name);
    return __assign(__assign({}, server), { players: __spreadArrays(server.players.filter(function (p) { return p.index !== event.VictimId; }), [
            __assign(__assign({}, victim), { isAlive: false })
        ]) });
};
var handlePlayerDeathEvent = function (event, server) {
    var player = server.players.find(function (p) { return p.index === event.PlayerId; });
    console.log(player.name + " died");
    return __assign(__assign({}, server), { players: __spreadArrays(server.players.filter(function (p) { return p.index !== event.PlayerId; }), [
            __assign(__assign({}, player), { isAlive: false })
        ]), 
        // TODO: Find a better way to clear projectiles
        projectiles: [] });
};
var handlePlayerSpawnEvent = function (event, server) {
    var player = server.players.find(function (p) { return p.index === event.PlayerId; });
    console.log(player.name + " spawned");
    return __assign(__assign({}, server), { players: __spreadArrays(server.players.filter(function (p) { return p.index !== event.PlayerId; }), [
            __assign(__assign({}, player), { isAlive: true })
        ]) });
};
var handlePlayerTeamEvent = function (event, server) {
    var player = server.players.find(function (p) { return p.index === event.PlayerId; });
    console.log(player.name + " joined team " + event.TeamId);
    return __assign(__assign({}, server), { players: __spreadArrays(server.players.filter(function (p) { return p.index !== event.PlayerId; }), [
            __assign(__assign({}, player), { team: event.TeamId })
        ]) });
};
var handleProjectilePositionEvent = function (event, server) {
    var projectile = server.projectiles.find(function (p) { return p.projectileId === event.ProjectileId; });
    return __assign(__assign({}, server), { projectiles: __spreadArrays(server.projectiles.filter(function (p) { return p.projectileId !== event.ProjectileId; }), [
            {
                projectileId: event.ProjectileId,
                template: event.Template,
                position: [event.Position.X || 0, event.Position.Y || 0, event.Position.Z || 0],
                rotation: [event.Rotation.X || 0, event.Rotation.Y || 0, event.Rotation.Z || 0],
                positionHistory: __spreadArrays((projectile && projectile.positionHistory) || [], [
                    [event.Position.X || 0, event.Position.Y || 0, event.Position.Z || 0]
                ]),
                previousPosition: /*(projectile && projectile.position) ||*/ [],
                previousRotation: /*(projectile && projectile.rotation) ||*/ [],
                updateTimestamp: window.performance.now()
            }
        ]) });
};
var handleChatEvent = function (event, server) {
    var player = server.players.find(function (p) { return p.index === event.Message.PlayerId; });
    return __assign(__assign({}, server), { chat: __spreadArrays(server.chat, [
            {
                playerId: event.Message.PlayerId || 0,
                name: player ? player.name : '?',
                channel: event.Message.Channel || '',
                text: event.Message.Text || '',
                time: new Date(event.Message.Time || ''),
            }
        ]) });
};
var handleGameStateEvent = function (event, server) {
    return __assign(__assign({}, server), { state: event.State });
};
var handleMapChangeEvent = function (event, server) {
    return __assign(__assign({}, server), { map: event.Map });
};
//# sourceMappingURL=reducer.js.map