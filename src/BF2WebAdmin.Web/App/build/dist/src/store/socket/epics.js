import { combineEpics, ofType } from 'redux-observable';
import { map, ObservableWebSocket, switchMap, takeUntil } from '../rxjs.imports';
import { actionCreators, WEBSOCKET_START, WEBSOCKET_STOP } from './';
var webSocketUrl = 'ws://localhost:56920/';
var socket$ = ObservableWebSocket(webSocketUrl);
var webSocketEpic = function (action$, store) { return action$.pipe(ofType(WEBSOCKET_START), switchMap(function (action) {
    return socket$.pipe(map(function (payload) {
        // const message = serializer.deserialize(payload);
        return actionCreators.websocketMessage(payload);
    }), takeUntil(action$.ofType(WEBSOCKET_STOP)));
})); };
export var epics = combineEpics(webSocketEpic);
//# sourceMappingURL=epics.js.map