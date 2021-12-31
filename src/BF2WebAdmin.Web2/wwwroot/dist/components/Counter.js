"use strict";
const React = require("react");
const react_redux_1 = require("react-redux");
const CounterStore = require("../store/Counter");
class Counter extends React.Component {
    render() {
        return React.createElement("div", null,
            React.createElement("h1", null, "Counter"),
            React.createElement("p", null, "This is a simple example of a React component."),
            React.createElement("p", null,
                "Current count: ",
                React.createElement("strong", null, this.props.count)),
            React.createElement("button", { onClick: () => { this.props.increment(); } }, "Increment"));
    }
}
Object.defineProperty(exports, "__esModule", { value: true });
// Wire up the React component to the Redux store
exports.default = react_redux_1.connect((state) => state.counter, // Selects which state properties are merged into the component's props
CounterStore.actionCreators // Selects which action creators are merged into the component's props
)(Counter);
//# sourceMappingURL=Counter.js.map