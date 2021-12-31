import { combineReducers } from 'redux';
import { WEBSOCKET_START, WEBSOCKET_STOP } from './';
export var reducer = combineReducers({
    isConnected: function (state, action) {
        if (state === void 0) { state = false; }
        switch (action.type) {
            case WEBSOCKET_START:
                return true;
            case WEBSOCKET_STOP:
                return false;
            default:
                return state;
        }
    },
});
//# sourceMappingURL=reducer.js.map