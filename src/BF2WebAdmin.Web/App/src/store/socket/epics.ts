import { combineEpics, Epic, ofType } from 'redux-observable';
import { map, ObservableWebSocket, switchMap, takeUntil } from '../rxjs.imports';
import { RootAction, RootState } from '../';
import { actionCreators, WEBSOCKET_START, WEBSOCKET_STOP } from './';

const webSocketUrl = 'ws://localhost:56920/';
const socket$ = ObservableWebSocket<any>(webSocketUrl);

const webSocketEpic: Epic<RootAction, RootState> =
    (action$, store) => action$.pipe(
        ofType(WEBSOCKET_START),
        switchMap(action =>
            socket$.pipe(
                map(payload => {
                    // const message = serializer.deserialize(payload);
                    return actionCreators.websocketMessage(payload);
                }),
                takeUntil(action$.ofType(WEBSOCKET_STOP))
            )
        )
    );

export const epics = combineEpics(webSocketEpic);
