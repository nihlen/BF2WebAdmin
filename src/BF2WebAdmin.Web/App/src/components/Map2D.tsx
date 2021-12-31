import * as React from 'react';
// import * as PIXI from 'pixi.js';
import { Player, Projectile } from '../store/socket';

export interface MapProps {
    map: string;
    players: Player[];
    projectiles: Projectile[];
}

const TIME_STEP = 250;

// This should have a container so we dont update the parent the whole time
class Map2D extends React.Component<MapProps, {}> {

    private game: Game;

    shouldComponentUpdate(nextProps: Readonly<MapProps>) {
        this.game.updateState(nextProps.map, nextProps.players, nextProps.projectiles);
        return false;
    }

    componentWillUnmount() {
        this.game.stop();
    }

    render() {
        return (
            <div className="map">
                <canvas ref={canvas => this.startGame(canvas)} width="700" height="700" />
            </div>
        );
    }

    private startGame(canvas: HTMLCanvasElement | null) {
        const renderer = new Renderer(canvas!);
        this.game = new Game(renderer);
        this.game.start();
    }
}

export default Map2D;

class Game {

    map: string;
    players: Player[];
    projectiles: Projectile[];

    private animationRequestId: number;
    private lastAnimationUpdate: number;
    private renderer: Renderer;

    private nextState: { map: string, players: Player[], projectiles: Projectile[] } | null;
    private currentUpdateTimestamp = 0;
    private previousUpdateTimestamp = 0;

    constructor(renderer: Renderer) {
        this.renderer = renderer;
        console.log(this.lastAnimationUpdate); // TODO: remove
    }

    start() {
        this.animationRequestId = requestAnimationFrame(timestamp => this.update(timestamp));
    }

    stop() {
        cancelAnimationFrame(this.animationRequestId);
    }

    updateState(map: string, players: Player[], projectiles: Projectile[]) {
        this.nextState = { map, players, projectiles };
        this.currentUpdateTimestamp = window.performance.now();
        // const player = players.find(p => p.name.includes('krische'));
        // console.log('== STATE UPDATE ==', player ? player.position[0] : '-');
    }

    updateEntities() {
        if (this.nextState) {

            // Set previous player positions
            this.nextState.players.forEach(p => {
                if (!this.players) {
                    return;
                }
                const currentPlayer = this.players.find(cp => cp.index === p.index);
                if (currentPlayer) {
                    p.previousPosition = currentPlayer.position;
                    p.previousRotation = currentPlayer.rotation;
                }
                // this.renderer.previousPositions[p.index] = currentPlayer.position;
            });

            // Set previous projectile positions
            this.nextState.projectiles.forEach(p => {
                if (!this.projectiles) {
                    return;
                }
                const currentProjectile = this.projectiles.find(cp => cp.projectileId === p.projectileId);
                if (currentProjectile) {
                    p.previousPosition = currentProjectile!.position;
                    p.previousRotation = currentProjectile!.rotation;
                }
                // this.renderer.previousPositions[p.index] = currentPlayer.position;
            });

            this.map = this.nextState.map;
            this.players = this.nextState.players;
            this.projectiles = this.nextState.projectiles;
            this.nextState = null;
        }
    }

    update(timestamp: number) {
        // Apparently it's best practice to request at the start of the loop
        // https://developer.mozilla.org/en-US/docs/Games/Anatomy
        // TODO: should be after update but before render? remove timeout after testing
        // setTimeout(() => {
        this.animationRequestId = requestAnimationFrame(t => this.update(t));
        // }, 5000);

        // Update local game entities
        // this.game.update();
        this.updateEntities();

        // Draw map
        // const dt = window.performance.now() - this.currentUpdateTimestamp;
        const dt = (window.performance.now() - this.currentUpdateTimestamp) / TIME_STEP;
        const hasNewState = this.currentUpdateTimestamp !== this.previousUpdateTimestamp;
        // if (hasNewState) {
        //     console.log('NEW STATE DETECTED');
        // }
        this.renderer.render(this.map, this.players, this.projectiles, dt, hasNewState);
        this.previousUpdateTimestamp = this.currentUpdateTimestamp;
        // this.renderer.render(this.map, this.players, this.projectiles, timestamp - this.lastAnimationUpdate);

        // Send actions to the server
        // this.sendGameActions();

        // Save timestamp for our delta
        this.lastAnimationUpdate = timestamp;
    }
}

// interface EntityPositions {
//     [id: number]: number[];
// }

class Renderer {

    private ctx: CanvasRenderingContext2D;
    private width: number;
    private height: number;
    private background: HTMLImageElement;
    private backgroundLoaded: boolean;
    private heliIcon: HTMLImageElement;
    private heliIconLoaded: boolean;
    // public previousPositions: EntityPositions;

    constructor(canvas: HTMLCanvasElement) {
        this.ctx = canvas.getContext('2d')!;
        this.ctx.scale(1, 1);
        this.width = canvas!.width;
        this.height = canvas!.height;
        this.background = new Image();
        this.background.src = '../assets/dalian_2_v_2.png';
        this.background.onload = () => { this.backgroundLoaded = true; };
        this.heliIcon = new Image();
        this.heliIcon.src = '../assets/ahe_icon.png';
        this.heliIcon.onload = () => { this.heliIconLoaded = true; };
    }

    render(map: string, players: Player[], projectiles: Projectile[], dt: number, newState: boolean) {
        this.ctx.clearRect(0, 0, this.width, this.height);
        this.renderMap(map);

        if (!players) {
            return;
        }

        const playersSorted = players.sort((a, b) => a.index - b.index);
        const soldiers = playersSorted.filter(p => p.vehicleId === 0);
        this.renderSoldiers(soldiers);

        const vehicles = groupByVehicle(playersSorted.filter(p => p.vehicleId !== 0));
        this.renderVehicles(vehicles, dt);
        this.renderProjectiles(projectiles, dt);

        // if (dt > 0.9) {
        //     this.previousPositions = {};
        //     players.forEach(p => this.previousPositions[p.index] = p.position);
        //     projectiles.forEach(p => this.previousPositions[p.projectileId] = p.position);
        // }
    }

    renderMap(map: string) {
        if (this.backgroundLoaded) {
            const p = getPosition(
                this.width / 2 - this.background.width / 2,
                this.height / 2 - this.background.height / 2
            );
            this.ctx.drawImage(this.background, p.x, p.y);
        }
    }

    renderSoldiers(soldiers: Player[]) {
        soldiers.forEach(player => {
            const size = 4;
            const p = getPosition(
                player.position[0] + this.width / 2,
                -player.position[1] + this.height / 2
            );

            this.ctx.fillStyle = 'green';
            this.ctx.textAlign = 'center';
            this.ctx.beginPath();
            this.ctx.arc(p.x, p.y, size, 0, Math.PI * 2, true);
            this.ctx.closePath();
            this.ctx.fill();
            this.ctx.fillText(player.name, p.x, p.y - (size + 3));
        });
    }

    renderVehicles(vehicles: Map<number, Player[]>, dt: number) {
        vehicles.forEach((vehiclePlayers: Player[], vehicleId: number) => {
            if (!vehiclePlayers) {
                console.log('no vehiclePlayers');
                return;
            }
            if (!this.heliIconLoaded) {
                console.log('no heliIcon');
                return;
            }

            // const size = 6;
            // const previousPosition = this.previousPositions[vehiclePlayers[0].index];
            const player = vehiclePlayers[0];
            const pdt = (window.performance.now() - player.updateTimestamp) / TIME_STEP;
            const interpolatedPosition = lerp(player.previousPosition, player.position, pdt);
            // const interpolated = [
            //     lerp(player.previousPosition[0], player.position[0], dt),
            //     lerp(player.previousPosition[1], player.position[1], dt),
            //     lerp(player.previousPosition[2], player.position[2], dt),
            // ];
            const p = getPosition(
                interpolatedPosition[0] + this.width / 2,
                -interpolatedPosition[1] + this.height / 2
            );
            // console.log(`${player.previousPosition[0]} => ${vehiclePlayers[0].position[0]} @ ${dt} = ${interpolated[0]}`);
            // const p = getPosition(
            //     vehiclePlayers[0].position[0] + this.width / 2,
            //     -vehiclePlayers[0].position[1] + this.height / 2
            // );

            const scale = 2 * (interpolatedPosition[2] / 170);
            const interpolatedYaw = angleLerp(player.previousRotation[0], player.rotation[0], pdt);
            this.ctx.save();
            this.ctx.translate(p.x, p.y);
            this.ctx.rotate((interpolatedYaw * Math.PI / 180));
            this.ctx.drawImage(
                this.heliIcon,
                -(this.heliIcon.width * scale / 2),
                -(this.heliIcon.height * scale / 2),
                this.heliIcon.width * scale,
                this.heliIcon.height * scale
            );
            this.ctx.restore();
            this.ctx.fillStyle = 'white';
            this.ctx.textAlign = 'center';
            this.ctx.fillText(vehiclePlayers.map(pl => pl.name).join(', '), p.x, p.y - (scale * this.heliIcon.height));
        });
    }

    renderProjectiles(projectiles: Projectile[], dt: number) {
        projectiles.forEach((projectile: Projectile) => {
            const size = 3;
            // const p = getPosition(
            //     projectile.position[0] + this.width / 2,
            //     -projectile.position[1] + this.height / 2
            // );

            const pdt = (window.performance.now() - projectile.updateTimestamp) / TIME_STEP;
            const interpolated = lerp(projectile.previousPosition, projectile.position, pdt);
            const p = getPosition(
                interpolated[0] + this.width / 2,
                -interpolated[1] + this.height / 2
            );

            this.ctx.fillStyle = 'white';
            this.ctx.textAlign = 'center';
            this.ctx.beginPath();
            this.ctx.arc(p.x, p.y, size, 0, Math.PI * 2, true);
            this.ctx.closePath();
            this.ctx.fill();
            // this.ctx.fillText('ID' + projectile.projectileId, p.x, p.y - (size + 3));

            // Trail
            let p1 = getPosition(0, 0);
            let p2: { x: number, y: number } | null = null;
            projectile.positionHistory.forEach(position => {
                p1 = getPosition(
                    position[0] + this.width / 2,
                    -position[1] + this.height / 2
                );
                if (p2) {
                    this.ctx.strokeStyle = 'rgba(255, 255, 255, 0.5)';
                    this.ctx.beginPath();
                    this.ctx.moveTo(p1.x, p1.y);
                    this.ctx.lineTo(p2.x, p2.y);
                    this.ctx.stroke();
                }
                p2 = p1;
            });
        });
    }
}

const getPosition = (x: number, y: number) => {
    return {
        x: x + 0,
        y: y - 100
    };
};

const groupByVehicle = (array: Player[]) => {
    return array.reduce(
        (entryMap, e) => entryMap.set(e.vehicleId, [...entryMap.get(e.vehicleId) || [], e]),
        new Map()
    );
};

const lerp = (v0: number[], v1: number[], dt: number) => {
    return [
        lerpValue(v0[0], v1[0], dt),
        lerpValue(v0[1], v1[1], dt),
        lerpValue(v0[2], v1[2], dt),
    ];
};

const lerpValue = (v0: number, v1: number, t: number) => {
    // return v0 * (1 - t) + v1 * t;
    return v0 + ((v1 - v0) * t);
    // return Mathf.Lerp(current /* (previous) */, final, Time.deltaTime*adaptionSpeed);
    // return x_start + ((x_final - x_start) * time) 
};

const angleLerp = (a0: number, a1: number, t: number) => {
    // a0 = a0 * Math.PI / 180;
    // a1 = a1 * Math.PI / 180;
    return a0 + shortAngleDist(a0, a1) * t;
}

const shortAngleDist = (a0: number, a1: number) => {
    // const max = Math.PI * 2;
    const max = 360;
    const da = (a1 - a0) % max;
    return 2 * da % max - da;
}
