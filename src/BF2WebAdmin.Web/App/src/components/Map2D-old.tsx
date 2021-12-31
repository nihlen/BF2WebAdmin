// import * as React from 'react';
// import { Player, Projectile } from '../store/socket';

// export interface MapProps {
//     map: string;
//     players: Player[];
//     projectiles: Projectile[];
// }

// // This should have a container so we dont update the parent the whole time
// class Map2D extends React.Component<MapProps, {}> {

//     private ctx: CanvasRenderingContext2D;
//     private width: number;
//     private height: number;
//     private background: HTMLImageElement;
//     private backgroundLoaded: boolean;

//     shouldComponentUpdate(nextProps: Readonly<MapProps>, nextState: Readonly<{}>) {

//         this.ctx.clearRect(0, 0, this.width, this.height);

//         if (this.backgroundLoaded) {
//             const p = getPosition(
//                 this.width / 2 - this.background.width / 2,
//                 this.height / 2 - this.background.height / 2
//             );
//             this.ctx.drawImage(this.background, p.x, p.y);
//         }

//         const players = nextProps.players.sort((a, b) => a.index - b.index);
//         const soldiers = players.filter(p => p.vehicleId === 0);
//         const vehicles = groupByVehicle(players.filter(p => p.vehicleId !== 0));

//         soldiers.forEach(player => {
//             const size = 4;
//             const p = getPosition(
//                 player.position[0] + this.width / 2,
//                 -player.position[1] + this.height / 2
//             );

//             this.ctx.fillStyle = 'green';
//             this.ctx.textAlign = 'center';
//             this.ctx.beginPath();
//             this.ctx.arc(p.x, p.y, size, 0, Math.PI * 2, true);
//             this.ctx.closePath();
//             this.ctx.fill();
//             this.ctx.fillText(player.name, p.x, p.y - (size + 3));
//         });

//         vehicles.forEach((vehiclePlayers: Player[], vehicleId: number) => {
//             const size = 6;
//             const p = getPosition(
//                 vehiclePlayers[0].position[0] + this.width / 2,
//                 -vehiclePlayers[0].position[1] + this.height / 2
//             );

//             this.ctx.fillStyle = 'white';
//             this.ctx.textAlign = 'center';
//             this.ctx.beginPath();
//             this.ctx.arc(p.x, p.y, size, 0, Math.PI * 2, true);
//             this.ctx.closePath();
//             this.ctx.fill();
//             this.ctx.fillText(vehiclePlayers.map(pl => pl.name).join(', '), p.x, p.y - (size + 3));
//         });

//         nextProps.projectiles.forEach((projectile: Projectile) => {
//             const size = 3;
//             const p = getPosition(
//                 projectile.position[0] + this.width / 2,
//                 -projectile.position[1] + this.height / 2
//             );

//             this.ctx.fillStyle = 'red';
//             this.ctx.textAlign = 'center';
//             this.ctx.beginPath();
//             this.ctx.arc(p.x, p.y, size, 0, Math.PI * 2, true);
//             this.ctx.closePath();
//             this.ctx.fill();
//             this.ctx.fillText('ID' + projectile.projectileId, p.x, p.y - (size + 3));
//         });

//         return false;
//     }

//     render() {
//         console.log('Re-render canvas :/');
//         return (
//             <div className="map">
//                 <canvas ref={canvas => this.setCanvas(canvas)} width="700" height="700" />
//             </div>
//         );
//     }

//     private setCanvas(canvas: HTMLCanvasElement | null) {
//         this.ctx = canvas!.getContext('2d')!;
//         this.ctx.scale(1, 1);
//         this.width = canvas!.width;
//         this.height = canvas!.height;
//         this.background = new Image();
//         this.background.src = '../assets/dalian_2_v_2.png';
//         this.background.onload = () => { this.backgroundLoaded = true; };
//     }
// }

// const getPosition = (x: number, y: number) => {
//     return {
//         x: x + 0,
//         y: y - 100
//     };
// };

// const groupByVehicle = (array: Player[]) => {
//     return array.reduce(
//         (entryMap, e) => entryMap.set(e.vehicleId, [...entryMap.get(e.vehicleId) || [], e]),
//         new Map()
//     );
// };

// export default Map2D;
