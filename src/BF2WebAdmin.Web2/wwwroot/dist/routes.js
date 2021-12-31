"use strict";
const React = require("react");
const react_router_1 = require("react-router");
const Layout_1 = require("./components/Layout");
const Home_1 = require("./components/Home");
const FetchData_1 = require("./components/FetchData");
const Counter_1 = require("./components/Counter");
Object.defineProperty(exports, "__esModule", { value: true });
exports.default = React.createElement(react_router_1.Route, { component: Layout_1.Layout },
    React.createElement(react_router_1.Route, { path: '/', components: { body: Home_1.default } }),
    React.createElement(react_router_1.Route, { path: '/counter', components: { body: Counter_1.default } }),
    React.createElement(react_router_1.Route, { path: '/fetchdata', components: { body: FetchData_1.default } },
        React.createElement(react_router_1.Route, { path: '(:startDateIndex)' }),
        " "));
// Enable Hot Module Replacement (HMR)
if (module.hot) {
    module.hot.accept();
}
//# sourceMappingURL=routes.js.map