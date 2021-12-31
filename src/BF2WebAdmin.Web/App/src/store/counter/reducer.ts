import { combineReducers } from 'redux';
import { DECREMENT_COUNT, INCREMENT_COUNT } from './';

export interface State {
    readonly count: number;
}

export const reducer = combineReducers<State>({
    count: (state = 0, action) => {
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