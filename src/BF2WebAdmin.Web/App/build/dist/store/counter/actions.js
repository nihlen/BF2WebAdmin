export var INCREMENT_COUNT = 'INCREMENT_COUNT';
export var DECREMENT_COUNT = 'DECREMENT_COUNT';
export var actionCreators = {
    incrementCount: function () { return ({
        type: INCREMENT_COUNT,
    }); },
    decrementCount: function () { return ({
        type: DECREMENT_COUNT,
    }); },
};
//# sourceMappingURL=actions.js.map