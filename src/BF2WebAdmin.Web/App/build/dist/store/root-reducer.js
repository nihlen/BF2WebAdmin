import { combineReducers } from 'redux';
import { reducer as counter } from './counter';
import { reducer as socket } from './socket';
export var rootReducer = combineReducers({
    counter: counter,
    socket: socket,
});
//# sourceMappingURL=root-reducer.js.map