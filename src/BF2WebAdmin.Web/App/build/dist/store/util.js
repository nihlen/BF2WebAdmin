export function getType(actionCreator) {
    if (actionCreator == null || actionCreator.getType == null) {
        throw new Error('first argument is not a "ts-action-creator" instance');
    }
    return actionCreator.getType();
}
export function createAction(typeString, creatorFunction) {
    var actionCreator;
    if (creatorFunction != null) {
        if (typeof creatorFunction !== 'function') {
            throw new Error('second argument is not a function');
        }
        actionCreator = creatorFunction;
    }
    else {
        actionCreator = function () { return ({ type: typeString }); };
    }
    if (typeString != null) {
        if (typeof typeString !== 'string') {
            throw new Error('first argument is not a type string');
        }
        actionCreator.getType = function () { return typeString; };
    }
    else {
        throw new Error('first argument is missing');
    }
    return actionCreator;
}
//# sourceMappingURL=util.js.map