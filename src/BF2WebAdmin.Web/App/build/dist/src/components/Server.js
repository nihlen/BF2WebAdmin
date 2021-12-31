import * as React from 'react';
import Map2D from './Map2D';
var Server = function (props) {
    if (!props.server) {
        return (React.createElement("div", null, "Server not found"));
    }
    var chatlogs = props.server.chat.map(function (m) { return (React.createElement("li", { key: m.time.valueOf() },
        getTime(m.time),
        " ",
        m.channel,
        " ",
        (m.channel !== 'ServerMessage') && m.name,
        ": ",
        m.text)); });
    return (React.createElement("div", { className: "server" },
        "Server ",
        props.match.params.id,
        getTeamTable(1, props.server.players),
        getTeamTable(2, props.server.players),
        React.createElement(Map2D, { map: props.server.map, players: props.server.players, projectiles: props.server.projectiles }),
        React.createElement("ul", null, chatlogs)));
};
var getTeamTable = function (teamId, players) {
    var teamPlayers = players
        .filter(function (p) { return p.team === teamId; })
        .sort(function (a, b) { return a.totalScore !== b.totalScore ? a.totalScore - b.totalScore : a.index - b.index; })
        .map(function (p) { return (React.createElement("tr", { key: p.index },
        React.createElement("td", null, p.isAlive ? p.name : (React.createElement("del", null, p.name))),
        React.createElement("td", null, p.teamScore),
        React.createElement("td", null, p.kills),
        React.createElement("td", null, p.deaths),
        React.createElement("td", null, p.totalScore),
        React.createElement("td", null, p.ping),
        React.createElement("td", null,
            "[",
            p.position[0],
            ", ",
            p.position[1],
            ", ",
            p.position[2],
            "]"))); });
    return (React.createElement("table", null,
        React.createElement("thead", null,
            React.createElement("tr", null,
                React.createElement("th", null, "Name"),
                React.createElement("th", null, "TeamScore"),
                React.createElement("th", null, "Kills"),
                React.createElement("th", null, "Deaths"),
                React.createElement("th", null, "TotalScore"),
                React.createElement("th", null, "Ping"),
                React.createElement("th", null, "Position"))),
        React.createElement("tbody", null, teamPlayers)));
};
var getTime = function (date) {
    return date.toTimeString().split(' ')[0];
};
export default Server;
//# sourceMappingURL=Server.js.map