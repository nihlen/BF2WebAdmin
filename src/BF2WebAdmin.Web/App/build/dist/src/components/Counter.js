import * as React from 'react';
var Counter = function (props) {
    return (React.createElement("div", { className: "counter" },
        React.createElement("div", null,
            "Counter: ",
            props.count),
        React.createElement("div", null,
            React.createElement("button", { onClick: props.onDecrement }, "-"),
            React.createElement("button", { onClick: props.onIncrement }, "+"))));
};
export default Counter;
//# sourceMappingURL=Counter.js.map