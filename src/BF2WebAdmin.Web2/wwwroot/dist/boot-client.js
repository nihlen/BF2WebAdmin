"use strict";
require("app.css");
require("main.css");
// import 'bootstrap';
const React = require("react");
const ReactDOM = require("react-dom");
const react_router_1 = require("react-router");
const react_redux_1 = require("react-redux");
const react_router_redux_1 = require("react-router-redux");
const routes_1 = require("./routes");
const configureStore_1 = require("./configureStore");
// Get the application-wide store instance, prepopulating with state from the server where available.
const initialState = window.initialReduxState;
const store = configureStore_1.default(initialState);
const history = react_router_redux_1.syncHistoryWithStore(react_router_1.browserHistory, store);
// This code starts up the React app when it runs in a browser. It sets up the routing configuration
// and injects the app into a DOM element.
ReactDOM.render(React.createElement(react_redux_1.Provider, { store: store },
    React.createElement(react_router_1.Router, { history: history, children: routes_1.default })), document.getElementById('react-app'));
//# sourceMappingURL=boot-client.js.map