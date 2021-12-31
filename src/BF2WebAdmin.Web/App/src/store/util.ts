export type EmptyAction<T extends string> = {
    type: T;
};

export type FluxStandardAction<T extends string, P = any, M = any> = {
    type: T;
    payload?: P;
    meta?: M,
    error?: boolean;
};

export interface TypeGetter<T> { getType?: () => T; }

export function getType<T extends string>(
    actionCreator: ((...args: any[]) => { type: T }) & TypeGetter<T>,
): T {
    if (actionCreator == null || actionCreator.getType == null) {
        throw new Error('first argument is not a "ts-action-creator" instance');
    }

    return actionCreator.getType();
}

export function createAction<T extends string,
    AC extends (...args: any[]) => FluxStandardAction<T>
    >(
    typeString: T,
    creatorFunction: AC,
): AC & TypeGetter<T>;

export function createAction<T extends string,
    AC extends () => { type: T }
    >(
    typeString: T,
): AC & TypeGetter<T>;

export function createAction<T extends string,
    AC extends (...args: any[]) => FluxStandardAction<T>
    >(
    typeString: T,
    creatorFunction?: AC,
): AC & TypeGetter<T> {
    let actionCreator: any;

    if (creatorFunction != null) {
        if (typeof creatorFunction !== 'function') {
            throw new Error('second argument is not a function');
        }

        actionCreator = creatorFunction;
    } else {
        actionCreator = () => ({ type: typeString });
    }

    if (typeString != null) {
        if (typeof typeString !== 'string') {
            throw new Error('first argument is not a type string');
        }

        actionCreator.getType = () => typeString;
    } else {
        throw new Error('first argument is missing');
    }

    return actionCreator;
}
