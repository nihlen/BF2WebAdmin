"use strict";
var __assign = (this && this.__assign) || Object.assign || function(t) {
    for (var s, i = 1, n = arguments.length; i < n; i++) {
        s = arguments[i];
        for (var p in s) if (Object.prototype.hasOwnProperty.call(s, p))
            t[p] = s[p];
    }
    return t;
};
require("app.css");
require("main.css");
const React = require("react");
const react_redux_1 = require("react-redux");
const server_1 = require("react-dom/server");
const react_router_1 = require("react-router");
const aspnet_prerendering_1 = require("aspnet-prerendering");
const routes_1 = require("./routes");
const configureStore_1 = require("./configureStore");
Object.defineProperty(exports, "__esModule", { value: true });
exports.default = aspnet_prerendering_1.createServerRenderer(params => {
    return new Promise((resolve, reject) => {
        // Match the incoming request against the list of client-side routes
        react_router_1.match({ routes: routes_1.default, location: params.location }, (error, redirectLocation, renderProps) => {
            if (error) {
                throw error;
            }
            // If there's a redirection, just send this information back to the host application
            if (redirectLocation) {
                resolve({ redirectUrl: redirectLocation.pathname });
                return;
            }
            // If it didn't match any route, renderProps will be undefined
            if (!renderProps) {
                throw new Error(`The location '${params.url}' doesn't match any route configured in react-router.`);
            }
            // Build an instance of the application
            const store = configureStore_1.default();
            const app = (React.createElement(react_redux_1.Provider, { store: store },
                React.createElement(react_router_1.RouterContext, __assign({}, renderProps))));
            // Perform an initial render that will cause any async tasks (e.g., data access) to begin
            server_1.renderToString(app);
            // Once the tasks are done, we can perform the final render
            // We also send the redux store state, so the client can continue execution where the server left off
            params.domainTasks.then(() => {
                resolve({
                    html: server_1.renderToString(app),
                    globals: { initialReduxState: store.getState() }
                });
            }, reject); // Also propagate any errors back into the host application
        });
    });
});
//# sourceMappingURL=boot-server.js.map