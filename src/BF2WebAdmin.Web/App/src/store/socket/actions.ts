export const WEBSOCKET_START = 'WEBSOCKET_START';
export const WEBSOCKET_STOP = 'WEBSOCKET_STOP';
export const WEBSOCKET_MESSAGE = 'WEBSOCKET_MESSAGE';

export interface Actions {
    WEBSOCKET_START: { type: typeof WEBSOCKET_START };
    WEBSOCKET_STOP: { type: typeof WEBSOCKET_STOP };
    WEBSOCKET_MESSAGE: { type: typeof WEBSOCKET_MESSAGE, payload: string };
}

export const actionCreators = {

    websocketStart: (message: string): Actions[typeof WEBSOCKET_START] => ({
        type: WEBSOCKET_START
    }),

    websocketStop: (message: string): Actions[typeof WEBSOCKET_STOP] => ({
        type: WEBSOCKET_STOP
    }),

    websocketMessage: (message: string): Actions[typeof WEBSOCKET_MESSAGE] => ({
        type: WEBSOCKET_MESSAGE, payload: message
    }),

};