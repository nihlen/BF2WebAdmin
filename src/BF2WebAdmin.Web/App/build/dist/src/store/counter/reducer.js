import { combineReducers } from 'redux';
import { DECREMENT_COUNT, INCREMENT_COUNT } from './';
export var reducer = combineReducers({
    count: function (state, action) {
        if (state === void 0) { state = 0; }
        switch (action.type) {
            case INCREMENT_COUNT:
                return state + 1;
            case DECREMENT_COUNT:
                return state - 1;
            default:
                return state;
        }
    },
});
//# sourceMappingURL=reducer.js.map