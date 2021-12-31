import { combineReducers } from 'redux';
import { reducer as counter, State as CounterState } from './counter';
import { reducer as socket, State as SocketState } from './socket';

// interface StoreEnhancerState { }

export interface RootState {
    counter: CounterState;
    socket: SocketState;
}

export const rootReducer = combineReducers<RootState>({
    counter,
    socket,
});