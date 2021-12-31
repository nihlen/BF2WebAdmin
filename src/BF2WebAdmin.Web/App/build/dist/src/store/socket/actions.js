export var WEBSOCKET_START = 'WEBSOCKET_START';
export var WEBSOCKET_STOP = 'WEBSOCKET_STOP';
export var WEBSOCKET_MESSAGE = 'WEBSOCKET_MESSAGE';
export var actionCreators = {
    websocketStart: function (message) { return ({
        type: WEBSOCKET_START
    }); },
    websocketStop: function (message) { return ({
        type: WEBSOCKET_STOP
    }); },
    websocketMessage: function (message) { return ({
        type: WEBSOCKET_MESSAGE, payload: message
    }); },
};
//# sourceMappingURL=actions.js.map