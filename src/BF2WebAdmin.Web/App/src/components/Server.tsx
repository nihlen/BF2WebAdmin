import * as React from 'react';
import { RouteComponentProps } from 'react-router';
import { GameServer, Player } from '../store/socket';
import Map2D from './Map2D';

export interface ServerProps extends RouteComponentProps<{ id: string }> {
    server: GameServer;
}

const Server: React.SFC<ServerProps> = (props) => {

    if (!props.server) {
        return (<div>Server not found</div>);
    }

    const chatlogs = props.server.chat.map(m => (
        <li key={m.time.valueOf()}>
            {getTime(m.time)} {m.channel} {(m.channel !== 'ServerMessage') && m.name}: {m.text}
        </li>
    ));

    return (
        <div className="server">
            Server {props.match.params.id}
            {getTeamTable(1, props.server.players)}
            {getTeamTable(2, props.server.players)}
            <Map2D map={props.server.map} players={props.server.players} projectiles={props.server.projectiles} />
            <ul>
                {chatlogs}
            </ul>
        </div>
    );
};

const getTeamTable = (teamId: number, players: Player[]) => {

    const teamPlayers = players
        .filter(p => p.team === teamId)
        .sort((a, b) => a.totalScore !== b.totalScore ? a.totalScore - b.totalScore : a.index - b.index)
        .map(p => (
            <tr key={p.index}>
                <td>{p.isAlive ? p.name : (<del>{p.name}</del>)}</td>
                <td>{p.teamScore}</td>
                <td>{p.kills}</td>
                <td>{p.deaths}</td>
                <td>{p.totalScore}</td>
                <td>{p.ping}</td>
                <td>[{p.position[0]}, {p.position[1]}, {p.position[2]}]</td>
            </tr>
        ));

    return (
        <table>
            <thead>
                <tr>
                    <th>Name</th>
                    <th>TeamScore</th>
                    <th>Kills</th>
                    <th>Deaths</th>
                    <th>TotalScore</th>
                    <th>Ping</th>
                    <th>Position</th>
                </tr>
            </thead>
            <tbody>
                {teamPlayers}
            </tbody>
        </table>
    );
};

const getTime = (date: Date) => {
    return date.toTimeString().split(' ')[0];
};

export default Server;
