var __extends = (this && this.__extends) || (function () {
    var extendStatics = function (d, b) {
        extendStatics = Object.setPrototypeOf ||
            ({ __proto__: [] } instanceof Array && function (d, b) { d.__proto__ = b; }) ||
            function (d, b) { for (var p in b) if (b.hasOwnProperty(p)) d[p] = b[p]; };
        return extendStatics(d, b);
    };
    return function (d, b) {
        extendStatics(d, b);
        function __() { this.constructor = d; }
        d.prototype = b === null ? Object.create(b) : (__.prototype = b.prototype, new __());
    };
})();
var __spreadArrays = (this && this.__spreadArrays) || function () {
    for (var s = 0, i = 0, il = arguments.length; i < il; i++) s += arguments[i].length;
    for (var r = Array(s), k = 0, i = 0; i < il; i++)
        for (var a = arguments[i], j = 0, jl = a.length; j < jl; j++, k++)
            r[k] = a[j];
    return r;
};
import * as React from 'react';
var TIME_STEP = 250;
// This should have a container so we dont update the parent the whole time
var Map2D = /** @class */ (function (_super) {
    __extends(Map2D, _super);
    function Map2D() {
        return _super !== null && _super.apply(this, arguments) || this;
    }
    Map2D.prototype.shouldComponentUpdate = function (nextProps) {
        this.game.updateState(nextProps.map, nextProps.players, nextProps.projectiles);
        return false;
    };
    Map2D.prototype.componentWillUnmount = function () {
        this.game.stop();
    };
    Map2D.prototype.render = function () {
        var _this = this;
        return (React.createElement("div", { className: "map" },
            React.createElement("canvas", { ref: function (canvas) { return _this.startGame(canvas); }, width: "700", height: "700" })));
    };
    Map2D.prototype.startGame = function (canvas) {
        var renderer = new Renderer(canvas);
        this.game = new Game(renderer);
        this.game.start();
    };
    return Map2D;
}(React.Component));
export default Map2D;
var Game = /** @class */ (function () {
    function Game(renderer) {
        this.currentUpdateTimestamp = 0;
        this.previousUpdateTimestamp = 0;
        this.renderer = renderer;
        console.log(this.lastAnimationUpdate); // TODO: remove
    }
    Game.prototype.start = function () {
        var _this = this;
        this.animationRequestId = requestAnimationFrame(function (timestamp) { return _this.update(timestamp); });
    };
    Game.prototype.stop = function () {
        cancelAnimationFrame(this.animationRequestId);
    };
    Game.prototype.updateState = function (map, players, projectiles) {
        this.nextState = { map: map, players: players, projectiles: projectiles };
        this.currentUpdateTimestamp = window.performance.now();
        // const player = players.find(p => p.name.includes('krische'));
        // console.log('== STATE UPDATE ==', player ? player.position[0] : '-');
    };
    Game.prototype.updateEntities = function () {
        var _this = this;
        if (this.nextState) {
            // Set previous player positions
            this.nextState.players.forEach(function (p) {
                if (!_this.players) {
                    return;
                }
                var currentPlayer = _this.players.find(function (cp) { return cp.index === p.index; });
                if (currentPlayer) {
                    p.previousPosition = currentPlayer.position;
                    p.previousRotation = currentPlayer.rotation;
                }
                // this.renderer.previousPositions[p.index] = currentPlayer.position;
            });
            // Set previous projectile positions
            this.nextState.projectiles.forEach(function (p) {
                if (!_this.projectiles) {
                    return;
                }
                var currentProjectile = _this.projectiles.find(function (cp) { return cp.projectileId === p.projectileId; });
                if (currentProjectile) {
                    p.previousPosition = currentProjectile.position;
                    p.previousRotation = currentProjectile.rotation;
                }
                // this.renderer.previousPositions[p.index] = currentPlayer.position;
            });
            this.map = this.nextState.map;
            this.players = this.nextState.players;
            this.projectiles = this.nextState.projectiles;
            this.nextState = null;
        }
    };
    Game.prototype.update = function (timestamp) {
        var _this = this;
        // Apparently it's best practice to request at the start of the loop
        // https://developer.mozilla.org/en-US/docs/Games/Anatomy
        // TODO: should be after update but before render? remove timeout after testing
        // setTimeout(() => {
        this.animationRequestId = requestAnimationFrame(function (t) { return _this.update(t); });
        // }, 5000);
        // Update local game entities
        // this.game.update();
        this.updateEntities();
        // Draw map
        // const dt = window.performance.now() - this.currentUpdateTimestamp;
        var dt = (window.performance.now() - this.currentUpdateTimestamp) / TIME_STEP;
        var hasNewState = this.currentUpdateTimestamp !== this.previousUpdateTimestamp;
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
    };
    return Game;
}());
// interface EntityPositions {
//     [id: number]: number[];
// }
var Renderer = /** @class */ (function () {
    // public previousPositions: EntityPositions;
    function Renderer(canvas) {
        var _this = this;
        this.ctx = canvas.getContext('2d');
        this.ctx.scale(1, 1);
        this.width = canvas.width;
        this.height = canvas.height;
        this.background = new Image();
        this.background.src = '../assets/dalian_2_v_2.png';
        this.background.onload = function () { _this.backgroundLoaded = true; };
        this.heliIcon = new Image();
        this.heliIcon.src = '../assets/ahe_icon.png';
        this.heliIcon.onload = function () { _this.heliIconLoaded = true; };
    }
    Renderer.prototype.render = function (map, players, projectiles, dt, newState) {
        this.ctx.clearRect(0, 0, this.width, this.height);
        this.renderMap(map);
        if (!players) {
            return;
        }
        var playersSorted = players.sort(function (a, b) { return a.index - b.index; });
        var soldiers = playersSorted.filter(function (p) { return p.vehicleId === 0; });
        this.renderSoldiers(soldiers);
        var vehicles = groupByVehicle(playersSorted.filter(function (p) { return p.vehicleId !== 0; }));
        this.renderVehicles(vehicles, dt);
        this.renderProjectiles(projectiles, dt);
        // if (dt > 0.9) {
        //     this.previousPositions = {};
        //     players.forEach(p => this.previousPositions[p.index] = p.position);
        //     projectiles.forEach(p => this.previousPositions[p.projectileId] = p.position);
        // }
    };
    Renderer.prototype.renderMap = function (map) {
        if (this.backgroundLoaded) {
            var p = getPosition(this.width / 2 - this.background.width / 2, this.height / 2 - this.background.height / 2);
            this.ctx.drawImage(this.background, p.x, p.y);
        }
    };
    Renderer.prototype.renderSoldiers = function (soldiers) {
        var _this = this;
        soldiers.forEach(function (player) {
            var size = 4;
            var p = getPosition(player.position[0] + _this.width / 2, -player.position[1] + _this.height / 2);
            _this.ctx.fillStyle = 'green';
            _this.ctx.textAlign = 'center';
            _this.ctx.beginPath();
            _this.ctx.arc(p.x, p.y, size, 0, Math.PI * 2, true);
            _this.ctx.closePath();
            _this.ctx.fill();
            _this.ctx.fillText(player.name, p.x, p.y - (size + 3));
        });
    };
    Renderer.prototype.renderVehicles = function (vehicles, dt) {
        var _this = this;
        vehicles.forEach(function (vehiclePlayers, vehicleId) {
            if (!vehiclePlayers) {
                console.log('no vehiclePlayers');
                return;
            }
            if (!_this.heliIconLoaded) {
                console.log('no heliIcon');
                return;
            }
            // const size = 6;
            // const previousPosition = this.previousPositions[vehiclePlayers[0].index];
            var player = vehiclePlayers[0];
            var pdt = (window.performance.now() - player.updateTimestamp) / TIME_STEP;
            var interpolatedPosition = lerp(player.previousPosition, player.position, pdt);
            // const interpolated = [
            //     lerp(player.previousPosition[0], player.position[0], dt),
            //     lerp(player.previousPosition[1], player.position[1], dt),
            //     lerp(player.previousPosition[2], player.position[2], dt),
            // ];
            var p = getPosition(interpolatedPosition[0] + _this.width / 2, -interpolatedPosition[1] + _this.height / 2);
            // console.log(`${player.previousPosition[0]} => ${vehiclePlayers[0].position[0]} @ ${dt} = ${interpolated[0]}`);
            // const p = getPosition(
            //     vehiclePlayers[0].position[0] + this.width / 2,
            //     -vehiclePlayers[0].position[1] + this.height / 2
            // );
            var scale = 2 * (interpolatedPosition[2] / 170);
            var interpolatedYaw = angleLerp(player.previousRotation[0], player.rotation[0], pdt);
            _this.ctx.save();
            _this.ctx.translate(p.x, p.y);
            _this.ctx.rotate((interpolatedYaw * Math.PI / 180));
            _this.ctx.drawImage(_this.heliIcon, -(_this.heliIcon.width * scale / 2), -(_this.heliIcon.height * scale / 2), _this.heliIcon.width * scale, _this.heliIcon.height * scale);
            _this.ctx.restore();
            _this.ctx.fillStyle = 'white';
            _this.ctx.textAlign = 'center';
            _this.ctx.fillText(vehiclePlayers.map(function (pl) { return pl.name; }).join(', '), p.x, p.y - (scale * _this.heliIcon.height));
        });
    };
    Renderer.prototype.renderProjectiles = function (projectiles, dt) {
        var _this = this;
        projectiles.forEach(function (projectile) {
            var size = 3;
            // const p = getPosition(
            //     projectile.position[0] + this.width / 2,
            //     -projectile.position[1] + this.height / 2
            // );
            var pdt = (window.performance.now() - projectile.updateTimestamp) / TIME_STEP;
            var interpolated = lerp(projectile.previousPosition, projectile.position, pdt);
            var p = getPosition(interpolated[0] + _this.width / 2, -interpolated[1] + _this.height / 2);
            _this.ctx.fillStyle = 'white';
            _this.ctx.textAlign = 'center';
            _this.ctx.beginPath();
            _this.ctx.arc(p.x, p.y, size, 0, Math.PI * 2, true);
            _this.ctx.closePath();
            _this.ctx.fill();
            // this.ctx.fillText('ID' + projectile.projectileId, p.x, p.y - (size + 3));
            // Trail
            var p1 = getPosition(0, 0);
            var p2 = null;
            projectile.positionHistory.forEach(function (position) {
                p1 = getPosition(position[0] + _this.width / 2, -position[1] + _this.height / 2);
                if (p2) {
                    _this.ctx.strokeStyle = 'rgba(255, 255, 255, 0.5)';
                    _this.ctx.beginPath();
                    _this.ctx.moveTo(p1.x, p1.y);
                    _this.ctx.lineTo(p2.x, p2.y);
                    _this.ctx.stroke();
                }
                p2 = p1;
            });
        });
    };
    return Renderer;
}());
var getPosition = function (x, y) {
    return {
        x: x + 0,
        y: y - 100
    };
};
var groupByVehicle = function (array) {
    return array.reduce(function (entryMap, e) { return entryMap.set(e.vehicleId, __spreadArrays(entryMap.get(e.vehicleId) || [], [e])); }, new Map());
};
var lerp = function (v0, v1, dt) {
    return [
        lerpValue(v0[0], v1[0], dt),
        lerpValue(v0[1], v1[1], dt),
        lerpValue(v0[2], v1[2], dt),
    ];
};
var lerpValue = function (v0, v1, t) {
    // return v0 * (1 - t) + v1 * t;
    return v0 + ((v1 - v0) * t);
    // return Mathf.Lerp(current /* (previous) */, final, Time.deltaTime*adaptionSpeed);
    // return x_start + ((x_final - x_start) * time) 
};
var angleLerp = function (a0, a1, t) {
    // a0 = a0 * Math.PI / 180;
    // a1 = a1 * Math.PI / 180;
    return a0 + shortAngleDist(a0, a1) * t;
};
var shortAngleDist = function (a0, a1) {
    // const max = Math.PI * 2;
    var max = 360;
    var da = (a1 - a0) % max;
    return 2 * da % max - da;
};
//# sourceMappingURL=Map2D.js.map