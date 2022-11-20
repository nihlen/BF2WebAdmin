function gameLoop(timeStamp) {
    if (!window.game?.canvas)
        return;

    window.requestAnimationFrame(gameLoop);
    game.instance.invokeMethodAsync('GameLoop', timeStamp, game.canvas.width, game.canvas.height);
}

function onResize() {
    if (!window.game?.canvas)
        return;

    var container = document.getElementById('canvas-container');
    game.canvas.width = container.clientWidth;
    game.canvas.height = container.clientHeight;
}

function onScroll(e) {
    if (!window.game?.canvas)
        return;

    game.instance.invokeMethodAsync('MapZoom', e.deltaY);
    e.preventDefault();
}

export function initRender(instance) {
    var canvas = document.querySelector('#canvas-container canvas');
    
    window.game = {
        instance: instance,
        canvas: canvas,
        isDragging: false
    };

    canvas.addEventListener("wheel", onScroll);
    window.addEventListener("resize", onResize);
    onResize();

    window.requestAnimationFrame(gameLoop);
}

export function stopRender() {
    if (window.game) {
        window.game.canvas.addEventListener("wheel", onScroll)
        window.game = null;
    }

    window.removeEventListener("resize", onResize)
}
