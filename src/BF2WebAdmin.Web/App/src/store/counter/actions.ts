export const INCREMENT_COUNT = 'INCREMENT_COUNT';
export const DECREMENT_COUNT = 'DECREMENT_COUNT';

export interface Actions {
    INCREMENT_COUNT: { type: typeof INCREMENT_COUNT, };
    DECREMENT_COUNT: { type: typeof DECREMENT_COUNT, };
}

export const actionCreators = {

    incrementCount: (): Actions[typeof INCREMENT_COUNT] => ({
        type: INCREMENT_COUNT,
    }),

    decrementCount: (): Actions[typeof DECREMENT_COUNT] => ({
        type: DECREMENT_COUNT,
    }),

};