// Epics - handling side effects of actions
// debounceTime - wait a while for new input before executing
// distinctUntilChanged - only emit when the current value is different than the last
// switchMap - cancelling previous request
import { combineEpics, ofType } from 'redux-observable';
import { map, ObservableWebSocket, switchMap, takeUntil } from '../rxjs.imports';
import { actionCreators, WEBSOCKET_START, WEBSOCKET_STOP } from './';
var webSocketUrl = 'ws://localhost:56920/';
var socket$ = ObservableWebSocket(webSocketUrl);
var webSocketEpic = function (action$, store) { return action$.pipe(ofType(WEBSOCKET_START), switchMap(function (action) {
    return socket$.pipe(map(function (payload) { return actionCreators.websocketMessage(payload); }), takeUntil(action$.ofType(WEBSOCKET_STOP)));
})); };
export var epics = combineEpics(webSocketEpic);
//# sourceMappingURL=epics.js.map